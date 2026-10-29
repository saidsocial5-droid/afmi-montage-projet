using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AFMI_Montage_projet_2.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>Modification locale d'un membre, prête à devenir une proposition.</summary>
    public class DetectedChange
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string? BaseValue { get; set; }
        public string? NewValue { get; set; }
        public int BaseVersion { get; set; } = 1;
        public string EntityLabel { get; set; } = string.Empty;
        public string? ProjectGlobalId { get; set; }

        public bool IsCreation => FieldName == ProposalFields.Create;
        public string FieldLabel => IsCreation ? "Nouvel élément" : FieldName;
        public string Summary => IsCreation ? $"{EntityLabel} (ajout)" : $"{EntityLabel} — {FieldName}";
    }

    /// <summary>Contenu d'une proposition de création (sérialisé dans ProposedValue).</summary>
    public class CreationPayload
    {
        /// <summary>Colonnes de l'élément (représentation brute invariante), hors clé et clés étrangères.</summary>
        public Dictionary<string, string?> Columns { get; set; } = new();

        /// <summary>Clés étrangères exprimées par le GlobalId de l'élément parent (ex. ResultId → GlobalId du résultat).</summary>
        public Dictionary<string, string?> ParentGlobalIds { get; set; } = new();
    }

    /// <summary>
    /// Compare la base locale d'un membre à la dernière version officielle reçue (fichier de référence)
    /// et produit la liste des modifications et ajouts (ADR 23). Les suppressions ne sont pas proposées
    /// automatiquement (sauf suppression logique IsDeleted des entités stratégiques, qui est un champ).
    /// </summary>
    public static class TeamChangeDetector
    {
        private sealed class Row
        {
            public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
            public string? Get(string column) => Values.TryGetValue(column, out var v) ? v : null;
        }

        public static List<DetectedChange> Detect(string baseDbPath, string localDbPath)
        {
            var changes = new List<DetectedChange>();
            using var baseConn = OfficialSnapshotService.Open(baseDbPath);
            using var localConn = OfficialSnapshotService.Open(localDbPath);

            var baseTables = new HashSet<string>(OfficialSnapshotService.ListTables(baseConn), StringComparer.OrdinalIgnoreCase);
            var localTables = new HashSet<string>(OfficialSnapshotService.ListTables(localConn), StringComparer.OrdinalIgnoreCase);

            // GlobalId des projets locaux, pour rattacher chaque modification à son projet
            var localProjectGlobalIds = localTables.Contains("Projects")
                ? LoadRows(localConn, "Projects").Values.ToDictionary(r => r.Get("Id") ?? "", r => r.Get("GlobalId"))
                : new Dictionary<string, string?>();

            var localIdToGlobal = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entityType in TeamDataModel.ProposableEntityTypes)
            {
                var table = entityType.GetTableName()!;
                if (!baseTables.Contains(table) || !localTables.Contains(table)) continue;

                var baseRows = LoadRows(baseConn, table);
                var localRows = LoadRows(localConn, table);
                var editable = TeamDataModel.EditableProperties(entityType).ToList();
                string typeName = entityType.ClrType.Name;

                foreach (var (globalId, local) in localRows)
                {
                    string label = BuildLabel(typeName, local);
                    string? projectGid = ResolveProjectGlobalId(typeName, local, localProjectGlobalIds);

                    if (!baseRows.TryGetValue(globalId, out var baseRow))
                    {
                        if (IsSoftDeleted(local)) continue;
                        changes.Add(new DetectedChange
                        {
                            EntityType = typeName,
                            EntityId = globalId,
                            FieldName = ProposalFields.Create,
                            NewValue = JsonSerializer.Serialize(BuildCreationPayload(entityType, local, localConn, localIdToGlobal)),
                            BaseVersion = 0,
                            EntityLabel = label,
                            ProjectGlobalId = projectGid
                        });
                        continue;
                    }

                    foreach (var property in editable)
                    {
                        string column = property.GetColumnName();
                        if (!local.Values.ContainsKey(column) || !baseRow.Values.ContainsKey(column)) continue;

                        string? before = baseRow.Get(column);
                        string? after = local.Get(column);
                        if (EntityValueCodec.AreEquivalent(property, before, after)) continue;

                        changes.Add(new DetectedChange
                        {
                            EntityType = typeName,
                            EntityId = globalId,
                            FieldName = property.Name,
                            BaseValue = before,
                            NewValue = after,
                            BaseVersion = int.TryParse(baseRow.Get("Version"), out var v) ? v : 1,
                            EntityLabel = label,
                            ProjectGlobalId = projectGid
                        });
                    }
                }
            }

            return changes;
        }

        private static CreationPayload BuildCreationPayload(IEntityType entityType, Row local, SqliteConnection localConn,
            Dictionary<string, Dictionary<string, string?>> idToGlobalCache)
        {
            var payload = new CreationPayload();
            foreach (var property in entityType.GetProperties().Where(p => !p.IsPrimaryKey() && !p.IsShadowProperty() && p.ClrType != typeof(byte[])))
            {
                string column = property.GetColumnName();
                if (!local.Values.ContainsKey(column)) continue;
                if (property.IsForeignKey()) continue;
                if (property.Name is "GlobalId" or "DriveFileId") continue;
                payload.Columns[property.Name] = local.Get(column);
            }

            foreach (var fk in entityType.GetForeignKeys().Where(fk => fk.Properties.Count == 1))
            {
                var fkProperty = fk.Properties[0];
                string? rawId = local.Get(fkProperty.GetColumnName());
                if (rawId == null) continue;
                if (!typeof(BaseEntity).IsAssignableFrom(fk.PrincipalEntityType.ClrType)) continue;

                var principalTable = fk.PrincipalEntityType.GetTableName()!;
                if (!idToGlobalCache.TryGetValue(principalTable, out var map))
                {
                    map = LoadRows(localConn, principalTable).Values
                        .Where(r => r.Get("Id") != null)
                        .GroupBy(r => r.Get("Id")!)
                        .ToDictionary(g => g.Key, g => g.First().Get("GlobalId"));
                    idToGlobalCache[principalTable] = map;
                }
                if (map.TryGetValue(rawId, out var parentGid) && !string.IsNullOrEmpty(parentGid))
                    payload.ParentGlobalIds[fkProperty.Name] = parentGid;
            }
            return payload;
        }

        private static bool IsSoftDeleted(Row row) => row.Get("IsDeleted") is "1" or "True" or "true";

        private static string BuildLabel(string typeName, Row row)
        {
            string prefix = TeamDataModel.EntityTypeLabel(typeName);
            foreach (var col in TeamDataModel.LabelColumns)
            {
                var text = row.Get(col);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
                    if (text.Length > 80) text = text[..77] + "…";
                    return $"{prefix} : {text}";
                }
            }
            return prefix;
        }

        private static string? ResolveProjectGlobalId(string typeName, Row row, Dictionary<string, string?> projectGlobalIds)
        {
            if (typeName == "Project") return row.Get("GlobalId");
            var projectId = row.Get("ProjectId");
            return projectId != null && projectGlobalIds.TryGetValue(projectId, out var gid) ? gid : null;
        }

        /// <summary>Lignes d'une table indexées par GlobalId (les lignes sans GlobalId sont ignorées).</summary>
        private static Dictionary<string, Row> LoadRows(SqliteConnection conn, string table)
        {
            var rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{table}\";";
            using var reader = cmd.ExecuteReader();
            int gidOrdinal = -1;
            for (int i = 0; i < reader.FieldCount; i++)
                if (string.Equals(reader.GetName(i), "GlobalId", StringComparison.OrdinalIgnoreCase)) gidOrdinal = i;
            if (gidOrdinal < 0) return rows;

            while (reader.Read())
            {
                if (reader.IsDBNull(gidOrdinal)) continue;
                var gid = reader.GetValue(gidOrdinal)?.ToString();
                if (string.IsNullOrEmpty(gid)) continue;

                var row = new Row();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var raw = reader.GetValue(i);
                    if (raw is byte[]) continue;
                    row.Values[reader.GetName(i)] = EntityValueCodec.RawToString(raw);
                }
                rows[gid] = row;
            }
            return rows;
        }
    }
}
