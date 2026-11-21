using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.Models
{
    /// <summary>
    /// Outils de mise à niveau du schéma (ADR 3 : pas de migrations EF, ALTER TABLE idempotents).
    /// Remplace les "try { ALTER TABLE } catch { }" qui provoquaient des centaines d'exceptions
    /// silencieuses à chaque démarrage et masquaient les vraies erreurs.
    /// </summary>
    public partial class AppDbContext
    {
        private const int BackupRetentionCount = 10;
        private static readonly object _backupLock = new();
        private static readonly HashSet<string> _backedUpDatabases = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, HashSet<string>?>? _columnCache;
        private readonly List<string> _addedColumns = new();

        /// <summary>Une seule ligne de journal par mise à niveau (et non une par colonne).</summary>
        private void LogSchemaUpgradeSummary()
        {
            if (_addedColumns.Count == 0) return;
            AppLogger.Info("Schema", $"{_addedColumns.Count} colonne(s) ajoutée(s) : {string.Join(", ", _addedColumns.Take(20))}{(_addedColumns.Count > 20 ? ", …" : "")}");
            _addedColumns.Clear();
        }

        private bool IsSqliteProvider => Database.IsSqlite();

        /// <summary>
        /// Ajoute toute colonne du modèle EF absente d'une table existante (ADR 34). Les anciennes bases avaient des tables
        /// créées par les blocs « CREATE TABLE » historiques, avant que le modèle ne gagne des colonnes (ex. Users.Status/Role) :
        /// l'insertion échouait alors avec « An error occurred while saving the entity changes ». Les colonnes obligatoires
        /// reçoivent une valeur par défaut neutre (texte vide, 0, faux, 2000-01-01) pour les lignes déjà présentes.
        /// Renvoie les colonnes ajoutées (« Table.Colonne »).
        /// </summary>
        private List<string> EnsureModelColumnsExist()
        {
            var added = new List<string>();
            _columnCache = null; // des étapes précédentes ont pu créer ou modifier des tables
            foreach (var entityType in Model.GetEntityTypes())
            {
                string? table = entityType.GetTableName();
                if (table == null || entityType.IsOwned()) continue;
                var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entityType.GetSchema());
                var existing = GetExistingColumns(table);
                if (existing == null) continue; // table absente : créée par EnsureModelTablesExist

                foreach (var property in entityType.GetProperties())
                {
                    string? column = property.GetColumnName(store);
                    if (column == null || existing.Contains(column) || property.IsPrimaryKey()) continue;
                    string? definition = ColumnDefinition(property);
                    if (definition == null) continue;
                    int before = _addedColumns.Count;
                    AddColumnIfMissing(table, column, definition);
                    if (_addedColumns.Count > before) added.Add($"{table}.{column}");
                }
            }
            return added;
        }

        /// <summary>Définition SQL d'une colonne ajoutée a posteriori (type du fournisseur + défaut si obligatoire).</summary>
        private string? ColumnDefinition(Microsoft.EntityFrameworkCore.Metadata.IProperty property)
        {
            string? type = property.GetColumnType();
            if (string.IsNullOrWhiteSpace(type)) return null;
            if (property.IsNullable) return type;

            var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            string? literal =
                clr == typeof(string) ? "''" :
                clr == typeof(bool) ? (IsSqliteProvider ? "0" : "FALSE") :
                clr.IsEnum || clr == typeof(int) || clr == typeof(long) || clr == typeof(short) || clr == typeof(byte) ||
                clr == typeof(decimal) || clr == typeof(double) || clr == typeof(float) ? "0" :
                clr == typeof(DateTime) || clr == typeof(DateTimeOffset) ? "'2000-01-01 00:00:00'" :
                clr == typeof(Guid) ? "'00000000-0000-0000-0000-000000000000'" :
                null;
            // Type sans valeur neutre connue (ex. tableau d'octets) : colonne facultative plutôt qu'un échec
            return literal == null ? type : $"{type} NOT NULL DEFAULT {literal}";
        }

        /// <summary>Colonnes existantes d'une table (null si la table n'existe pas). Mis en cache pour la durée du contexte.</summary>
        private HashSet<string>? GetExistingColumns(string table)
        {
            _columnCache ??= new Dictionary<string, HashSet<string>?>(StringComparer.Ordinal);
            if (_columnCache.TryGetValue(table, out var cached)) return cached;

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = Database.GetDbConnection();
            bool openedHere = connection.State != ConnectionState.Open;
            try
            {
                if (openedHere) Database.OpenConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = IsSqliteProvider
                    ? "SELECT name FROM pragma_table_info(@t);"
                    : "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @t;";
                var p = cmd.CreateParameter();
                p.ParameterName = "@t";
                p.Value = table;
                cmd.Parameters.Add(p);

                using var reader = cmd.ExecuteReader();
                while (reader.Read()) columns.Add(reader.GetString(0));
            }
            finally
            {
                if (openedHere) Database.CloseConnection();
            }

            var result = columns.Count == 0 ? null : columns;
            _columnCache[table] = result;
            return result;
        }

        /// <summary>
        /// Ajoute une colonne si (et seulement si) la table existe et que la colonne est absente.
        /// Les erreurs réelles sont journalisées au lieu d'être ignorées.
        /// </summary>
        private void AddColumnIfMissing(string table, string column, string definition)
        {
            try
            {
                var columns = GetExistingColumns(table);
                if (columns == null || columns.Contains(column)) return;

#pragma warning disable EF1002 // Identifiants issus du modèle EF ou de constantes du code, jamais d'une saisie utilisateur.
                Database.ExecuteSqlRaw($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};");
#pragma warning restore EF1002
                columns.Add(column);
                _addedColumns.Add($"{table}.{column}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Schema", $"Impossible d'ajouter la colonne {table}.{column} ({definition}).", ex);
            }
        }

        /// <summary>
        /// Crée les tables du modèle EF absentes de la base (EnsureCreated n'agit que sur une base vide).
        /// Le DDL vient du script généré par le fournisseur actif : valable pour SQLite et PostgreSQL.
        /// Renvoie les tables créées.
        /// </summary>
        private List<string> EnsureModelTablesExist()
        {
            var created = new List<string>();
            try
            {
                var existing = ListExistingTables();
                string script = Database.GenerateCreateScript();
                var statements = System.Text.RegularExpressions.Regex.Split(script, @";\s*(?:\r?\n|$)")
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();

                var tableRegex = new System.Text.RegularExpressions.Regex(@"^CREATE TABLE\s+(?:""[^""]+""\.)?""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var indexRegex = new System.Text.RegularExpressions.Regex(@"^CREATE\s+(?:UNIQUE\s+)?INDEX\s+""[^""]+""\s+ON\s+(?:""[^""]+""\.)?""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (var statement in statements)
                {
                    var match = tableRegex.Match(statement);
                    if (!match.Success || existing.Contains(match.Groups[1].Value)) continue;
                    string table = match.Groups[1].Value;
                    RunSchemaStep($"Création de la table {table}", () =>
                    {
#pragma warning disable EF1002 // DDL généré par EF Core à partir du modèle, sans saisie utilisateur.
                        Database.ExecuteSqlRaw(statement);
#pragma warning restore EF1002
                        created.Add(table);
                    });
                }

                foreach (var statement in statements)
                {
                    var match = indexRegex.Match(statement);
                    if (!match.Success || !created.Contains(match.Groups[1].Value)) continue;
                    RunSchemaStep($"Index de la table {match.Groups[1].Value}", () =>
                    {
#pragma warning disable EF1002
                        Database.ExecuteSqlRaw(statement);
#pragma warning restore EF1002
                    });
                }

                if (created.Count > 0)
                    AppLogger.Info("Schema", $"Table(s) créée(s) : {string.Join(", ", created)}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Schema", "Vérification des tables du modèle impossible.", ex);
            }
            return created;
        }

        private HashSet<string> ListExistingTables()
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = Database.GetDbConnection();
            bool openedHere = connection.State != ConnectionState.Open;
            try
            {
                if (openedHere) Database.OpenConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = IsSqliteProvider
                    ? "SELECT name FROM sqlite_master WHERE type = 'table';"
                    : "SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema();";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) tables.Add(reader.GetString(0));
            }
            finally
            {
                if (openedHere) Database.CloseConnection();
            }
            return tables;
        }

        /// <summary>Type SQL d'une colonne date/heure nullable selon le fournisseur.</summary>
        private string NullableDateTimeColumn => IsSqliteProvider ? "TEXT NULL" : "timestamp with time zone NULL";

        /// <summary>Exécute un bloc de création de schéma en journalisant l'éventuelle erreur.</summary>
        private void RunSchemaStep(string description, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Schema", $"Échec de l'étape de schéma : {description}", ex);
            }
            finally
            {
                _columnCache = null; // une étape peut avoir créé des tables
            }
        }

        /// <summary>
        /// Sauvegarde le fichier SQLite effectivement utilisé par ce contexte, une seule fois par processus,
        /// en ne conservant que les <see cref="BackupRetentionCount"/> sauvegardes les plus récentes.
        /// Sans effet pour PostgreSQL ou une base en mémoire (tests).
        /// </summary>
        public void BackupDatabaseBeforeMigration()
        {
            try
            {
                if (!IsSqliteProvider) return;

                string dbPath = Database.GetDbConnection().DataSource;
                if (string.IsNullOrWhiteSpace(dbPath) || dbPath == ":memory:" || !File.Exists(dbPath)) return;

                lock (_backupLock)
                {
                    if (!_backedUpDatabases.Add(Path.GetFullPath(dbPath))) return;

                    string backupFolder = Path.Combine(Path.GetDirectoryName(dbPath)!, "Backups");
                    Directory.CreateDirectory(backupFolder);
                    string backupPath = Path.Combine(backupFolder, $"projects_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");
                    File.Copy(dbPath, backupPath, true);

                    foreach (var old in new DirectoryInfo(backupFolder).GetFiles("projects_backup_*.db")
                                 .OrderByDescending(f => f.CreationTimeUtc)
                                 .Skip(BackupRetentionCount))
                    {
                        try { old.Delete(); } catch (Exception ex) { AppLogger.Warn("Schema", $"Suppression de l'ancienne sauvegarde impossible : {old.Name}", ex); }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Schema", "Sauvegarde de la base avant mise à niveau impossible.", ex);
            }
        }
    }
}
