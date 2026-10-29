using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.Services.Collab
{
    public class SnapshotApplyResult
    {
        public int TablesCopied { get; set; }
        public int ProjectCount { get; set; }
        public string? BackupPath { get; set; }
    }

    /// <summary>
    /// Version officielle des projets sous forme de fichier SQLite (ADR 23).
    /// - Poste officiel : <see cref="CreateSanitizedSnapshot"/> copie la base locale puis vide toutes les tables propres au poste
    ///   (comptes, mots de passe, réglages, clés, journaux, propositions) avant publication sur Drive.
    /// - Poste membre : <see cref="ApplySnapshotToLocal"/> remplace uniquement les tables de contenu de la base locale,
    ///   après sauvegarde ; comptes et réglages du poste sont conservés.
    /// </summary>
    public static class OfficialSnapshotService
    {
        private const int TeamSyncBackupRetention = 5;

        /// <summary>Crée à <paramref name="destPath"/> une copie cohérente et assainie de la base SQLite <paramref name="sourceDbPath"/>.</summary>
        public static int CreateSanitizedSnapshot(string sourceDbPath, string destPath)
        {
            if (!File.Exists(sourceDbPath)) throw new FileNotFoundException("Base locale introuvable.", sourceDbPath);
            if (File.Exists(destPath)) File.Delete(destPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destPath))!);

            // VACUUM INTO produit une copie transactionnellement cohérente même si l'application écrit en parallèle
            using (var source = Open(sourceDbPath))
            using (var cmd = source.CreateCommand())
            {
                cmd.CommandText = "VACUUM INTO @dest;";
                cmd.Parameters.AddWithValue("@dest", destPath);
                cmd.ExecuteNonQuery();
            }

            var shared = TeamDataModel.SharedTables;
            int projectCount;
            using (var snap = Open(destPath))
            {
                foreach (var table in ListTables(snap).Where(t => !shared.Contains(t)))
                {
                    Execute(snap, $"DELETE FROM \"{table}\";");
                }
                projectCount = CountRows(snap, "Projects");
                Execute(snap, "VACUUM;");
            }
            SqliteConnection.ClearAllPools();
            return projectCount;
        }

        /// <summary>
        /// Remplace le contenu partagé de la base locale par celui de la version officielle.
        /// Une sauvegarde de la base locale est faite au préalable dans Backups\ (5 dernières conservées).
        /// </summary>
        public static SnapshotApplyResult ApplySnapshotToLocal(string snapshotPath, string localDbPath, bool backup = true)
        {
            if (!File.Exists(snapshotPath)) throw new FileNotFoundException("Version officielle introuvable.", snapshotPath);
            if (!File.Exists(localDbPath)) throw new FileNotFoundException("Base locale introuvable.", localDbPath);

            var result = new SnapshotApplyResult();
            if (backup) result.BackupPath = BackupLocalDatabase(localDbPath);

            var shared = TeamDataModel.SharedTables;
            using var conn = Open(localDbPath);
            Execute(conn, "PRAGMA foreign_keys = OFF;"); // hors transaction, sinon sans effet

            using (var attach = conn.CreateCommand())
            {
                attach.CommandText = "ATTACH DATABASE @snap AS snap;";
                attach.Parameters.AddWithValue("@snap", snapshotPath);
                attach.ExecuteNonQuery();
            }

            try
            {
                var mainTables = new HashSet<string>(ListTables(conn, "main"), StringComparer.OrdinalIgnoreCase);
                var snapTables = ListTables(conn, "snap").Where(t => shared.Contains(t) && mainTables.Contains(t)).ToList();

                using var tx = conn.BeginTransaction();
                foreach (var table in snapTables)
                {
                    var mainCols = ListColumns(conn, "main", table);
                    var snapCols = new HashSet<string>(ListColumns(conn, "snap", table), StringComparer.OrdinalIgnoreCase);
                    var cols = mainCols.Where(snapCols.Contains).Select(c => $"\"{c}\"").ToList();
                    if (cols.Count == 0) continue;

                    string colList = string.Join(", ", cols);
                    Execute(conn, $"DELETE FROM main.\"{table}\";", tx);
                    Execute(conn, $"INSERT INTO main.\"{table}\" ({colList}) SELECT {colList} FROM snap.\"{table}\";", tx);
                    result.TablesCopied++;
                }

                // Références vers des comptes du poste officiel (inexistants ici) : remises à NULL quand c'est permis
                foreach (var entityType in TeamDataModel.SharedEntityTypes)
                {
                    var table = entityType.GetTableName();
                    if (table == null || !mainTables.Contains(table)) continue;
                    foreach (var fk in entityType.GetForeignKeys().Where(fk => TeamDataModel.IsStationOnly(fk.PrincipalEntityType)))
                    {
                        if (fk.Properties.Count != 1 || !fk.Properties[0].IsNullable) continue;
                        var column = fk.Properties[0].GetColumnName();
                        var principalTable = fk.PrincipalEntityType.GetTableName();
                        if (principalTable == null || !mainTables.Contains(principalTable)) continue;
                        Execute(conn, $"UPDATE main.\"{table}\" SET \"{column}\" = NULL WHERE \"{column}\" IS NOT NULL AND \"{column}\" NOT IN (SELECT \"Id\" FROM main.\"{principalTable}\");", tx);
                    }
                }

                tx.Commit();
                result.ProjectCount = CountRows(conn, "Projects");
            }
            finally
            {
                Execute(conn, "DETACH DATABASE snap;");
                Execute(conn, "PRAGMA foreign_keys = ON;");
            }

            SqliteConnection.ClearAllPools();
            AppLogger.Info("TeamSync", $"Version officielle appliquée : {result.TablesCopied} table(s), {result.ProjectCount} projet(s).");
            return result;
        }

        public static string BackupLocalDatabase(string localDbPath)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(localDbPath))!, "Backups");
            Directory.CreateDirectory(folder);
            string backupPath = Path.Combine(folder, $"projects_before_team_sync_{DateTime.Now:yyyyMMdd_HHmmss_fff}.db");
            for (int i = 2; File.Exists(backupPath); i++)
                backupPath = Path.Combine(folder, $"projects_before_team_sync_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{i}.db");

            using (var conn = Open(localDbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "VACUUM INTO @dest;";
                cmd.Parameters.AddWithValue("@dest", backupPath);
                cmd.ExecuteNonQuery();
            }

            foreach (var old in new DirectoryInfo(folder).GetFiles("projects_before_team_sync_*.db")
                         .OrderByDescending(f => f.CreationTimeUtc)
                         .Skip(TeamSyncBackupRetention))
            {
                try { old.Delete(); } catch (Exception ex) { AppLogger.Warn("TeamSync", $"Suppression de l'ancienne sauvegarde impossible : {old.Name}", ex); }
            }
            return backupPath;
        }

        internal static SqliteConnection Open(string path)
        {
            var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                DefaultTimeout = 30,
                Pooling = false
            }.ToString());
            conn.Open();
            return conn;
        }

        internal static List<string> ListTables(SqliteConnection conn, string schema = "main")
        {
            var tables = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT name FROM {schema}.sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));
            return tables;
        }

        internal static List<string> ListColumns(SqliteConnection conn, string schema, string table)
        {
            var columns = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT name FROM pragma_table_info(@t, @s);";
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@s", schema);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(0));
            return columns;
        }

        private static int CountRows(SqliteConnection conn, string table)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (SqliteException)
            {
                return 0;
            }
        }

        private static void Execute(SqliteConnection conn, string sql, SqliteTransaction? tx = null)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
