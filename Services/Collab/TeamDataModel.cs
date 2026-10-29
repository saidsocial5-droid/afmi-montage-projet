using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Models.Strategic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>
    /// Décrit, à partir du modèle EF, quelles tables constituent le « contenu » partagé par l'équipe
    /// (projets et tout ce qui en dépend) et lesquelles restent propres au poste (comptes, réglages, journaux, secrets).
    /// Liste blanche : une nouvelle table n'est partagée que si elle appartient au modèle et n'est pas exclue ici (ADR 23).
    /// </summary>
    public static class TeamDataModel
    {
        /// <summary>Entités propres au poste : jamais publiées, jamais écrasées par la version officielle.</summary>
        private static readonly Type[] StationOnlyTypes =
        {
            typeof(User), typeof(ProjectRole), typeof(AppSetting), typeof(AppConfig), typeof(AiLog),
            typeof(AuditLog), typeof(AuditLogEntry), typeof(LinkedDevice), typeof(Notification),
            typeof(Contribution), typeof(Vote), typeof(TextVersionHistory), typeof(TextAssistAuditLog),
            typeof(TextAssistSettings), typeof(RssItemCache)
        };

        /// <summary>Entités partagées mais calculées par l'application : pas de proposition sur leurs modifications.</summary>
        private static readonly Type[] DerivedTypes = { typeof(StrategicLink) };

        /// <summary>Propriétés techniques ignorées lors de la détection des modifications.</summary>
        private static readonly HashSet<string> TechnicalProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "GlobalId", "DriveFileId", "SyncVersion", "Version", "UpdatedByEmail", "ContributionStatus",
            "AuthorEmail", "AuthorName", "AuthorColor", "ApprovedBy", "ApprovedAt", "LastModifiedAt",
            "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "ModifiedAt", "ModifiedBy", "DeletedAt", "DeletedBy"
        };

        private static IModel? _model;

        public static IModel Model
        {
            get
            {
                if (_model != null) return _model;
                var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
                using var ctx = new AppDbContext(options);
                _model = ctx.Model;
                return _model;
            }
        }

        public static bool IsStationOnly(IEntityType entityType) =>
            StationOnlyTypes.Contains(entityType.ClrType);

        /// <summary>Types d'entités partagés (une table chacun).</summary>
        public static IReadOnlyList<IEntityType> SharedEntityTypes =>
            Model.GetEntityTypes()
                .Where(e => !e.IsOwned() && e.GetTableName() != null && !IsStationOnly(e))
                .ToList();

        /// <summary>Noms des tables partagées (contenu publié par le poste officiel).</summary>
        public static HashSet<string> SharedTables =>
            new(SharedEntityTypes.Select(e => e.GetTableName()!), StringComparer.OrdinalIgnoreCase);

        /// <summary>Types sur lesquels un membre peut proposer des modifications (entités identifiées par GlobalId).</summary>
        public static IReadOnlyList<IEntityType> ProposableEntityTypes =>
            SharedEntityTypes
                .Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType) && !DerivedTypes.Contains(e.ClrType))
                .ToList();

        public static IEntityType? FindEntityType(string clrName) =>
            Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == clrName);

        /// <summary>Propriétés comparées / proposables : scalaires, non clés, non étrangères, non techniques, non binaires.</summary>
        public static IEnumerable<IProperty> EditableProperties(IEntityType entityType) =>
            entityType.GetProperties().Where(p =>
                !p.IsPrimaryKey() &&
                !p.IsForeignKey() &&
                !p.IsShadowProperty() &&
                !TechnicalProperties.Contains(p.Name) &&
                p.ClrType != typeof(byte[]));

        /// <summary>Libellé français d'un type d'entité pour l'affichage.</summary>
        public static string EntityTypeLabel(string clrName) => clrName switch
        {
            "Project" => "Projet",
            "Objective" => "Objectif",
            "Result" => "Résultat",
            "Activity" => "Activité",
            "Indicator" => "Indicateur",
            "Risk" => "Risque",
            "BudgetItem" => "Ligne budgétaire",
            "BudgetCategory" => "Catégorie budgétaire",
            "FundingSource" => "Source de financement",
            "Output" => "Produit",
            "ProblemNode" => "Arbre à problèmes",
            "SeMilestone" => "Jalon S&E",
            "StrategicFoundation" => "Planification stratégique",
            "StrategicObjective" => "Objectif stratégique",
            "TheoryOfChangeNode" => "Théorie du changement",
            "StakeholderEntry" => "Partie prenante",
            "SwotItem" => "SWOT",
            "PestelItem" => "PESTEL",
            "NeedAssessment" => "Besoin",
            "StrategicOption" => "Option stratégique",
            "RiskEntry" => "Risque stratégique",
            "AssumptionEntry" => "Hypothèse",
            "ProjectDocument" => "Document",
            "TerritoryProfile" => "Territoire",
            "BeneficiaryGroup" => "Bénéficiaires",
            "DataSource" => "Source de données",
            "ContextIndicator" => "Indicateur de contexte",
            _ => clrName
        };

        /// <summary>Colonnes candidates pour nommer un élément, par ordre de préférence.</summary>
        public static readonly string[] LabelColumns = { "Title", "Name", "Label", "Text", "Statement", "Description", "Province", "Region", "Code", "Content" };
    }

    /// <summary>
    /// Conversion entre la représentation brute SQLite (texte invariant) et les valeurs CLR des propriétés EF.
    /// Une même représentation sert à la détection (comparaison de fichiers) et à l'application (EF), quel que soit le fournisseur.
    /// </summary>
    public static class EntityValueCodec
    {
        /// <summary>Représentation texte invariante d'une valeur lue dans SQLite.</summary>
        public static string? RawToString(object? raw) => raw switch
        {
            null or DBNull => null,
            string s => s,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => raw.ToString()
        };

        /// <summary>Convertit une valeur brute en valeur CLR de la propriété (lève FormatException si impossible).</summary>
        public static object? FromRaw(IProperty property, string? raw)
        {
            var converter = property.GetValueConverter();
            var targetType = converter?.ProviderClrType ?? property.ClrType;
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            object? value;
            if (raw == null)
            {
                value = null;
            }
            else if (underlying == typeof(string))
            {
                value = raw;
            }
            else if (underlying == typeof(bool))
            {
                value = raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (underlying.IsEnum)
            {
                value = long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    ? Enum.ToObject(underlying, n)
                    : Enum.Parse(underlying, raw, ignoreCase: true);
            }
            else if (underlying == typeof(decimal))
            {
                value = decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            else if (underlying == typeof(double) || underlying == typeof(float))
            {
                value = Convert.ChangeType(double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture), underlying, CultureInfo.InvariantCulture);
            }
            else if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short) || underlying == typeof(byte))
            {
                value = Convert.ChangeType(long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture), underlying, CultureInfo.InvariantCulture);
            }
            else if (underlying == typeof(DateTime))
            {
                value = DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            else if (underlying == typeof(DateTimeOffset))
            {
                value = DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);
            }
            else if (underlying == typeof(TimeSpan))
            {
                value = TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
            }
            else if (underlying == typeof(Guid))
            {
                value = Guid.Parse(raw);
            }
            else
            {
                value = Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
            }

            if (converter != null && value != null) value = converter.ConvertFromProvider(value);
            if (value == null && !property.IsNullable && property.ClrType == typeof(string)) value = string.Empty;
            return value;
        }

        /// <summary>Représentation brute d'une valeur CLR (pour comparer la valeur officielle actuelle).</summary>
        public static string? ToRaw(IProperty property, object? value)
        {
            if (value == null) return null;
            var converter = property.GetValueConverter();
            if (converter != null) value = converter.ConvertToProvider(value);
            return value switch
            {
                null => null,
                bool b => b ? "1" : "0",
                Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        /// <summary>Égalité sémantique de deux représentations brutes (ex. "1500" et "1500.0" pour un décimal).</summary>
        public static bool AreEquivalent(IProperty property, string? a, string? b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return true;
            if (property.ClrType == typeof(string) || Nullable.GetUnderlyingType(property.ClrType) == typeof(string))
                return string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b);
            try
            {
                return Equals(FromRaw(property, a), FromRaw(property, b));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
