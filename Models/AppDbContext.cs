using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Models
{
    public partial class AppDbContext : DbContext
    {
        public DbSet<Project> Projects { get; set; } = null!;
        public DbSet<Objective> Objectives { get; set; } = null!;
        public DbSet<Result> Results { get; set; } = null!;
        public DbSet<Activity> Activities { get; set; } = null!;
        public DbSet<Indicator> Indicators { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<BudgetCategory> BudgetCategories { get; set; } = null!;
        public DbSet<BudgetItem> BudgetItems { get; set; } = null!;
        public DbSet<AppConfig> AppConfigs { get; set; } = null!;
        public DbSet<AppSetting> AppSettings { get; set; } = null!;

        // Nouvelles entités formulaires dynamiques & projets sectoriels
        public DbSet<FormTemplate> FormTemplates { get; set; } = null!;
        public DbSet<FormInstance> FormInstances { get; set; } = null!;
        public DbSet<Taxonomy> Taxonomies { get; set; } = null!;
        public DbSet<TaxonomyValue> TaxonomyValues { get; set; } = null!;
        public DbSet<Output> Outputs { get; set; } = null!;
        public DbSet<StandardIndicator> StandardIndicators { get; set; } = null!;
        public DbSet<Risk> Risks { get; set; } = null!;
        public DbSet<SeMilestone> SeMilestones { get; set; } = null!;
        public DbSet<FundingSource> FundingSources { get; set; } = null!;
        public DbSet<BudgetItemContribution> BudgetItemContributions { get; set; } = null!;
        public DbSet<SafeguardEvaluation> SafeguardEvaluations { get; set; } = null!;
        public DbSet<AiLog> AiLogs { get; set; } = null!;

        // Nouveaux modèles : Chat, Arbre de problème, Analyse de documents
        public DbSet<ProblemNode> ProblemNodes { get; set; } = null!;
        public DbSet<ProjectDocument> ProjectDocuments { get; set; } = null!;
        public DbSet<MeetingRoom> MeetingRooms { get; set; } = null!;
        public DbSet<ActivityReport> ActivityReports { get; set; } = null!;

        // Nouveaux modèles V3 : Utilisateurs, Rôles, Traçabilité
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ProjectRole> ProjectRoles { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<LinkedDevice> LinkedDevices { get; set; } = null!;

        // Collaboration distante V5 : Contributions & Votes
        public DbSet<Contribution> Contributions { get; set; } = null!;
        public DbSet<Vote> Votes { get; set; } = null!;

        // وحدة التخطيط الاستراتيجي وتحليل المعطيات والارتباط V6
        public DbSet<StrategicFoundation> StrategicFoundations { get; set; } = null!;
        public DbSet<DataSource> DataSources { get; set; } = null!;
        public DbSet<ContextIndicator> ContextIndicators { get; set; } = null!;
        public DbSet<StakeholderEntry> Stakeholders { get; set; } = null!;
        public DbSet<SwotItem> SwotItems { get; set; } = null!;
        public DbSet<PestelItem> PestelItems { get; set; } = null!;
        public DbSet<NeedAssessment> NeedsAssessments { get; set; } = null!;
        public DbSet<StrategicOption> StrategicOptions { get; set; } = null!;
        public DbSet<TheoryOfChangeNode> TheoryOfChangeNodes { get; set; } = null!;
        public DbSet<StrategicObjective> StrategicObjectives { get; set; } = null!;
        public DbSet<RiskEntry> StrategicRisks { get; set; } = null!;
        public DbSet<AssumptionEntry> StrategicAssumptions { get; set; } = null!;
        public DbSet<StrategicLink> StrategicLinks { get; set; } = null!;
        public DbSet<AuditLogEntry> AuditLogEntries { get; set; } = null!;

        // Assistant IA Rédactionnel Universel V7
        public DbSet<TextVersionHistory> TextVersionHistories { get; set; } = null!;
        public DbSet<TextAssistAuditLog> TextAssistAuditLogs { get; set; } = null!;
        public DbSet<TextAssistSettings> TextAssistSettings { get; set; } = null!;

        // رادار المشاريع الذكي وخلاصات التمويل V8
        public DbSet<GrantOpportunity> GrantsPipeline { get; set; } = null!;
        public DbSet<RssItemCache> RssItemsCache { get; set; } = null!;

        // Base de veille : appels à venir et appels vus non retenus (ADR 28)
        public DbSet<UpcomingCall> UpcomingCalls { get; set; } = null!;
        public DbSet<RejectedCall> RejectedCalls { get; set; } = null!;

        // Territoire d'intervention et bénéficiaires (ADR 25)
        public DbSet<TerritoryProfile> TerritoryProfiles { get; set; } = null!;
        public DbSet<BeneficiaryGroup> BeneficiaryGroups { get; set; } = null!;

        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured) return;

            if (IsCloudDatabaseConfigured)
            {
                // Mode Cloud (PostgreSQL via Supabase ou autre)
                optionsBuilder.UseNpgsql(DatabaseConfigManager.LoadSettings().GetPostgresConnectionString());
            }
            else
            {
                // Mode Hors-ligne / Local (SQLite)
                string dbPath = LocalDatabasePath;
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                optionsBuilder.UseSqlite($"Data Source={dbPath};Default Timeout=30;");
            }
        }

        /// <summary>
        /// Variable d'environnement qui redirige toutes les données locales (base SQLite, sauvegardes, journal)
        /// vers un autre dossier, et force le mode SQLite. Utilisée par les tests pour ne jamais toucher la base réelle.
        /// </summary>
        public const string DataDirectoryOverrideVariable = "AFMI_DATA_DIR";

        public static string? DataDirectoryOverride =>
            Environment.GetEnvironmentVariable(DataDirectoryOverrideVariable) is { Length: > 0 } dir ? dir : null;

        /// <summary>Dossier des données locales : %LOCALAPPDATA%\AFMI_Montage_Projet (sauf redirection).</summary>
        public static string LocalDataDirectory => DataDirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AFMI_Montage_Projet");

        /// <summary>Fichier SQLite local : %LOCALAPPDATA%\AFMI_Montage_Projet\projects.db.</summary>
        public static string LocalDatabasePath => Path.Combine(LocalDataDirectory, "projects.db");

        /// <summary>Vrai si la configuration active utilise PostgreSQL (mode cloud) plutôt que le fichier SQLite local.</summary>
        public static bool IsCloudDatabaseConfigured
        {
            get
            {
                if (DataDirectoryOverride != null) return false;
                var settings = DatabaseConfigManager.LoadSettings();
                return settings.UseCloud && !string.IsNullOrWhiteSpace(settings.Host);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cascade delete configurations
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Objectives)
                .WithOne(o => o.Project)
                .HasForeignKey(o => o.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Objective>()
                .HasMany(o => o.Results)
                .WithOne(r => r.Objective)
                .HasForeignKey(r => r.ObjectiveId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Result>()
                .HasMany(r => r.Activities)
                .WithOne(a => a.Result)
                .HasForeignKey(a => a.ResultId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Activity>()
                .HasMany(a => a.Indicators)
                .WithOne(i => i.Activity)
                .HasForeignKey(i => i.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.BudgetItems)
                .WithOne(b => b.Project)
                .HasForeignKey(b => b.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BudgetCategory>()
                .HasMany(c => c.BudgetItems)
                .WithOne(b => b.Category)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cascade delete pour Output -> Activity
            modelBuilder.Entity<Output>()
                .HasMany(o => o.Activities)
                .WithOne(a => a.Output)
                .HasForeignKey(a => a.OutputId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour FormTemplate -> FormInstance
            modelBuilder.Entity<FormTemplate>()
                .HasMany<FormInstance>()
                .WithOne(i => i.FormTemplate)
                .HasForeignKey(i => i.FormTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> FormInstance
            modelBuilder.Entity<Project>()
                .HasMany<FormInstance>()
                .WithOne(i => i.Project)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Taxonomy -> TaxonomyValue
            modelBuilder.Entity<Taxonomy>()
                .HasMany(t => t.Values)
                .WithOne(v => v.Taxonomy)
                .HasForeignKey(v => v.TaxonomyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> Risk
            modelBuilder.Entity<Project>()
                .HasMany<Risk>()
                .WithOne(r => r.Project)
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> SeMilestone
            modelBuilder.Entity<Project>()
                .HasMany<SeMilestone>()
                .WithOne(m => m.Project)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> FundingSource
            modelBuilder.Entity<Project>()
                .HasMany<FundingSource>()
                .WithOne(s => s.Project)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour BudgetItem -> BudgetItemContribution
            modelBuilder.Entity<BudgetItem>()
                .HasMany<BudgetItemContribution>()
                .WithOne(c => c.BudgetItem)
                .HasForeignKey(c => c.BudgetItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> SafeguardEvaluation
            modelBuilder.Entity<Project>()
                .HasMany<SafeguardEvaluation>()
                .WithOne(e => e.Project)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> ProblemNode
            modelBuilder.Entity<Project>()
                .HasMany<ProblemNode>()
                .WithOne(n => n.Project)
                .HasForeignKey(n => n.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing relationship for ProblemNode
            modelBuilder.Entity<ProblemNode>()
                .HasMany(n => n.ChildNodes)
                .WithOne(n => n.ParentNode)
                .HasForeignKey(n => n.ParentNodeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> ProjectDocument
            modelBuilder.Entity<Project>()
                .HasMany<ProjectDocument>()
                .WithOne(d => d.Project)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> MeetingRoom
            modelBuilder.Entity<Project>()
                .HasMany<MeetingRoom>()
                .WithOne(m => m.Project)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Project -> ProjectRole
            modelBuilder.Entity<Project>()
                .HasMany<ProjectRole>()
                .WithOne(pr => pr.Project)
                .HasForeignKey(pr => pr.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour User -> ProjectRole
            modelBuilder.Entity<User>()
                .HasMany(u => u.ProjectRoles)
                .WithOne(pr => pr.User)
                .HasForeignKey(pr => pr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete pour Activity -> ActivityReport
            modelBuilder.Entity<Activity>()
                .HasMany<ActivityReport>()
                .WithOne(ar => ar.Activity)
                .HasForeignKey(ar => ar.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActivityReport>()
                .HasOne(ar => ar.ReportedByUser)
                .WithMany()
                .HasForeignKey(ar => ar.ReportedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Restrict delete on ProposedByUserId to avoid multiple cascade paths
            modelBuilder.Entity<Objective>().HasOne(x => x.ProposedByUser).WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Result>().HasOne(x => x.ProposedByUser).WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Activity>().HasOne(x => x.ProposedByUser).WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ProblemNode>().HasOne(x => x.ProposedByUser).WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(DeleteBehavior.SetNull);

            // Configuration Contributions & Votes
            modelBuilder.Entity<Vote>()
                .HasOne(v => v.Contribution)
                .WithMany(c => c.Votes)
                .HasForeignKey(v => v.ContributionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vote>()
                .HasIndex(v => new { v.ContributionId, v.VoterEmail })
                .IsUnique();

            // V6 : Configuration de l'Unité de Tخطيط الاستراتيجي (Strategic Planning)
            modelBuilder.Entity<Project>()
                .HasOne(p => p.StrategicFoundation)
                .WithOne(sf => sf.Project)
                .HasForeignKey<StrategicFoundation>(sf => sf.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.DataSources).WithOne(d => d.StrategicFoundation).HasForeignKey(d => d.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.ContextIndicators).WithOne(c => c.StrategicFoundation).HasForeignKey(c => c.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.Stakeholders).WithOne(s => s.StrategicFoundation).HasForeignKey(s => s.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.SwotItems).WithOne(s => s.StrategicFoundation).HasForeignKey(s => s.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.PestelItems).WithOne(p => p.StrategicFoundation).HasForeignKey(p => p.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.NeedsAssessments).WithOne(n => n.StrategicFoundation).HasForeignKey(n => n.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.StrategicOptions).WithOne(o => o.StrategicFoundation).HasForeignKey(o => o.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.TheoryOfChangeNodes).WithOne(t => t.StrategicFoundation).HasForeignKey(t => t.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.StrategicObjectives).WithOne(o => o.StrategicFoundation).HasForeignKey(o => o.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.Risks).WithOne(r => r.StrategicFoundation).HasForeignKey(r => r.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StrategicFoundation>().HasMany(sf => sf.Assumptions).WithOne(a => a.StrategicFoundation).HasForeignKey(a => a.StrategicFoundationId).OnDelete(DeleteBehavior.Cascade);

            // Territoire (un profil par projet) et groupes de bénéficiaires
            modelBuilder.Entity<TerritoryProfile>()
                .HasOne(t => t.Project)
                .WithMany()
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TerritoryProfile>().HasIndex(t => t.ProjectId).IsUnique();

            modelBuilder.Entity<BeneficiaryGroup>()
                .HasOne(b => b.Project)
                .WithMany()
                .HasForeignKey(b => b.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Filtres globaux de suppression logique (Soft-Delete)
            modelBuilder.Entity<StrategicFoundation>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<DataSource>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ContextIndicator>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<StakeholderEntry>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<SwotItem>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<PestelItem>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<NeedAssessment>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<StrategicOption>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<TheoryOfChangeNode>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<StrategicObjective>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<RiskEntry>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<AssumptionEntry>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<StrategicLink>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<AuditLogEntry>().HasQueryFilter(e => !e.IsDeleted);
        }

        // Méthode pour initialiser des données par défaut si la DB est vide
        public void SeedDefaultData()
        {
            // النسخ الاحتياطي الآمن لقاعدة البيانات قبل أي تعديل
            BackupDatabaseBeforeMigration();

            Database.EnsureCreated();

            // Tables ajoutées au fil des versions pour les anciennes bases SQLite locales.
            // (Syntaxe SQLite : sur PostgreSQL, EnsureCreated crée déjà le modèle complet.)
            if (IsSqliteProvider) CreateLegacySqliteTablesIfNotExist();

            // Tables ajoutées au modèle depuis la création de la base (territoire, bénéficiaires…)
            EnsureModelTablesExist();
            _columnCache = null;

            // Comptes créés avant les rôles (ADR 34) : les comptes existants restent actifs, et un administrateur système garde ses droits
            bool usersHadStatus = GetExistingColumns("Users")?.Contains("Status") ?? true;
            AddColumnIfMissing("Users", "Status", "TEXT NOT NULL DEFAULT 'Active'");
            AddColumnIfMissing("Users", "Role", "TEXT NOT NULL DEFAULT 'Reader'");
            if (!usersHadStatus)
                RunSchemaStep("Rôles des comptes existants", () =>
                    Database.ExecuteSqlRaw(IsSqliteProvider
                        ? "UPDATE \"Users\" SET \"Role\" = 'SuperAdmin' WHERE \"IsSystemAdmin\" = 1;"
                        : "UPDATE \"Users\" SET \"Role\" = 'SuperAdmin' WHERE \"IsSystemAdmin\" = TRUE;"));

            MigrateObsoleteGeminiModels();

            // V4 & V5 : Inject BaseEntity collaborative fields safely
            var entityTypes = Model.GetEntityTypes();
            foreach (var entityType in entityTypes)
            {
                var tableName = entityType.GetTableName();
                if (tableName != null)
                {
                    AddColumnIfMissing(tableName, "GlobalId", "TEXT");
                    AddColumnIfMissing(tableName, "DriveFileId", "TEXT");
                    AddColumnIfMissing(tableName, "SyncVersion", "INTEGER NOT NULL DEFAULT 1");
                    AddColumnIfMissing(tableName, "Version", "INTEGER NOT NULL DEFAULT 1");
                    AddColumnIfMissing(tableName, "UpdatedByEmail", "TEXT");
                    AddColumnIfMissing(tableName, "ContributionStatus", "TEXT NOT NULL DEFAULT 'Official'");
                    AddColumnIfMissing(tableName, "AuthorEmail", "TEXT");
                    AddColumnIfMissing(tableName, "AuthorName", "TEXT");
                    AddColumnIfMissing(tableName, "AuthorColor", "TEXT");
                    AddColumnIfMissing(tableName, "ApprovedBy", "TEXT");
                    AddColumnIfMissing(tableName, "ApprovedAt", "TEXT");
                    AddColumnIfMissing(tableName, "LastModifiedAt", "TEXT NOT NULL DEFAULT '2000-01-01 00:00:00'");
                }
            }

                        // Initialize empty GlobalIds safely
            foreach (var entityType in entityTypes)
            {
                var tableName = entityType.GetTableName();
                if (tableName != null)
                {
                    if (GetExistingColumns(tableName)?.Contains("GlobalId") != true) continue;
                    string newId = IsSqliteProvider ? "hex(randomblob(16))" : "md5(random()::text || clock_timestamp()::text)";
#pragma warning disable EF1002 // Types SQL et noms de tables constants ou issus du modèle EF, jamais une saisie utilisateur.
                    RunSchemaStep($"GlobalId {tableName}", () => Database.ExecuteSqlRaw($"UPDATE \"{tableName}\" SET \"GlobalId\" = {newId} WHERE \"GlobalId\" IS NULL OR \"GlobalId\" = '';"));
#pragma warning restore EF1002
                }
            }

            // V3 : Ajouter les champs collaboratifs aux tables du cadre logique
            string[] tablesToUpdate = { "Objectives", "Results", "Activities", "ProblemNodes" };
            foreach (var table in tablesToUpdate)
            {
                AddColumnIfMissing(table, "ProposedByUserId", "INTEGER NULL");
                AddColumnIfMissing(table, "Status", "TEXT NOT NULL DEFAULT 'Approved'");
                AddColumnIfMissing(table, "IsAnonymized", "INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing(table, "AiSuggestionBadge", "TEXT NOT NULL DEFAULT ''");
            }

            AddColumnIfMissing("AppConfigs", "UseLocalByDefault", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing("AppConfigs", "CloudOptInGranted", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("AppConfigs", "DataMinimizationEnabled", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing("AppConfigs", "MonthlyRequestLimit", "INTEGER NOT NULL DEFAULT 200");

            AddColumnIfMissing("Projects", "Justification", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "ProjectApproach", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "ProjectImpacts", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "StrategicAlignment", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "Sustainability", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "OrganizationalStrengths", "TEXT NOT NULL DEFAULT ''");

            // Colonnes Analyse SWOT
            AddColumnIfMissing("Projects", "SwotStrengths", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "SwotWeaknesses", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "SwotOpportunities", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "SwotThreats", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Projects", "GlobalRequirementsSummary", "TEXT NOT NULL DEFAULT ''");

            // Mises à jour des tables existantes (Activities et Indicators)
            AddColumnIfMissing("Activities", "OutputId", "INTEGER NULL");
            AddColumnIfMissing("Indicators", "ActualValue", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Indicators", "GenderMaleValue", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Indicators", "GenderFemaleValue", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Indicators", "AgeYouthValue", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Indicators", "AgeAdultValue", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Indicators", "StandardIndicatorId", "INTEGER NULL");
            AddColumnIfMissing("BudgetItems", "ActivityId", "INTEGER NULL");

            // Espace d'équipe (ADR 23) : libellé et projet des propositions
            AddColumnIfMissing("Contributions", "EntityLabel", "TEXT NULL");
            AddColumnIfMissing("Contributions", "ProjectGlobalId", "TEXT NULL");

            // Analyse structurée des documents (ADR 25)
            AddColumnIfMissing("ProjectDocuments", "ExtractedText", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("ProjectDocuments", "ContentHash", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("ProjectDocuments", "InsightsJson", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("ProjectDocuments", "DetectedKind", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("ProjectDocuments", "AnalyzedAt", NullableDateTimeColumn);
            AddColumnIfMissing("ProjectDocuments", "IntegratedAt", NullableDateTimeColumn);

            // Vérifier s'il y a déjà des catégories budgétaires
            if (!BudgetCategories.Any())
            {
                BudgetCategories.AddRange(new BudgetCategory[]
                {
                    new() { Code = "RH", Name = "Ressources Humaines" },
                    new() { Code = "FONC", Name = "Frais de Fonctionnement" },
                    new() { Code = "INV", Name = "Investissements & Équipements" },
                    new() { Code = "COM", Name = "Communication & Visibilité" },
                    new() { Code = "SE", Name = "Suivi, Évaluation & Apprentissage" },
                    new() { Code = "IMP", Name = "Imprévus & Provisions (Réserve)" }
                });
                SaveChanges();
            }

            // Initialiser les taxonomies de base (ODD et Secteurs d'intervention) si vides
            if (!Taxonomies.Any())
            {
                var oddTax = new Taxonomy
                {
                    Name = "Objectifs de Développement Durable (ODD)",
                    Description = "Les 17 ODD de l'ONU pour le suivi d'impact."
                };
                Taxonomies.Add(oddTax);
                SaveChanges();

                TaxonomyValues.AddRange(new TaxonomyValue[]
                {
                    new() { TaxonomyId = oddTax.Id, Code = "ODD1", Label = "Pas de pauvreté" },
                    new() { TaxonomyId = oddTax.Id, Code = "ODD2", Label = "Faim « zéro »" },
                    new() { TaxonomyId = oddTax.Id, Code = "ODD3", Label = "Bonne santé et bien-être" },
                    new() { TaxonomyId = oddTax.Id, Code = "ODD4", Label = "Éducation de qualité" },
                    new() { TaxonomyId = oddTax.Id, Code = "ODD5", Label = "Égalité entre les sexes" },
                    new() { TaxonomyId = oddTax.Id, Code = "ODD6", Label = "Eau propre et assainissement" },
                    new() { TaxonomyId = oddTax.Id, Code = "ODD13", Label = "Mesures relatives à la lutte contre les changements climatiques" },
                    new() { TaxonomyId = oddTax.Id, Code = "ODD15", Label = "Vie terrestre" }
                });

                var sectTax = new Taxonomy
                {
                    Name = "Domaines d'Intervention Sectoriels",
                    Description = "Secteurs d'intervention clés pour le développement local."
                };
                Taxonomies.Add(sectTax);
                SaveChanges();

                TaxonomyValues.AddRange(new TaxonomyValue[]
                {
                    new() { TaxonomyId = sectTax.Id, Code = "ENV", Label = "Environnement & Biodiversité" },
                    new() { TaxonomyId = sectTax.Id, Code = "AGRI", Label = "Agriculture & Souveraineté alimentaire" },
                    new() { TaxonomyId = sectTax.Id, Code = "EAU", Label = "Accès à l'eau potable & assainissement" },
                    new() { TaxonomyId = sectTax.Id, Code = "ENRG", Label = "Énergies renouvelables & climat" },
                    new() { TaxonomyId = sectTax.Id, Code = "CAP", Label = "Renforcement de capacités & éducation" }
                });
                SaveChanges();
            }

            // Initialiser les indicateurs standardisés de base
            if (!StandardIndicators.Any())
            {
                StandardIndicators.AddRange(new StandardIndicator[]
                {
                    new() { Name = "Nombre de bénéficiaires directs", Description = "Nombre de personnes touchées directement par les activités du projet.", Category = "Social", Unit = "personnes" },
                    new() { Name = "Surface forestière ou agricole restaurée", Description = "Nombre d'hectares de terres restaurées ou préservées.", Category = "Environnement", Unit = "hectares" },
                    new() { Name = "Nombre d'emplois verts créés", Description = "Emplois durables créés dans le cadre du projet.", Category = "Économique", Unit = "emplois" },
                    new() { Name = "Taux de participation des femmes", Description = "Pourcentage de femmes impliquées activement.", Category = "Social", Unit = "%" }
                });
                SaveChanges();
            }

            // Initialiser le modèle de formulaire FEM/PMF - ICCA
            if (!FormTemplates.Any())
            {
                string schemaJson = @"[
                    {
                        ""Section"": ""1. Éligibilité & Cadrage Sectoriel"",
                        ""Fields"": [
                            { ""Name"": ""SectorIntervention"", ""Label"": ""Secteur prioritaire du projet"", ""Type"": ""Dropdown"", ""Options"": [""Conservation de la biodiversité"", ""Lutte contre la dégradation des terres"", ""Atténuation des changements climatiques"", ""Gestion durable des forêts""], ""Placeholder"": ""Sélectionner le domaine thématique..."" },
                            { ""Name"": ""IccaCategory"", ""Label"": ""Catégorie de territoire de vie (ICCA)"", ""Type"": ""Dropdown"", ""Options"": [""Forêt communautaire"", ""Zone côtière gérée localement"", ""Pâturages traditionnels"", ""Autre""], ""Placeholder"": ""Sélectionner la catégorie..."" },
                            { ""Name"": ""IsCommunityLed"", ""Label"": ""Le projet est-il initié et géré directement par des communautés autochtones ou locales ?"", ""Type"": ""Checkbox"", ""Placeholder"": """" }
                        ]
                    },
                    {
                        ""Section"": ""2. Analyse Narrative Avancée"",
                        ""Fields"": [
                            { ""Name"": ""CommunityParticipation"", ""Label"": ""Méthodologie de participation communautaire"", ""Type"": ""TextLong"", ""Placeholder"": ""Expliquez comment les bénéficiaires et les communautés locales sont impliqués dans les phases du projet..."" },
                            { ""Name"": ""GenderInclusionPlan"", ""Label"": ""Plan d'inclusion du genre et des jeunes"", ""Type"": ""TextLong"", ""Placeholder"": ""Précisez les mesures spécifiques pour garantir l'équité de genre et la place des jeunes..."" }
                        ]
                    },
                    {
                        ""Section"": ""3. Évaluation des Risques Sociaux & Environnementaux (Safeguards)"",
                        ""Fields"": [
                            { ""Name"": ""SafeguardBiodiversity"", ""Label"": ""Le projet présente-t-il des risques sur la biodiversité ou les zones protégées ?"", ""Type"": ""Dropdown"", ""Options"": [""Nul"", ""Faible"", ""Moyen"", ""Fort""], ""Placeholder"": ""Sélectionner le niveau de risque..."" },
                            { ""Name"": ""SafeguardIndigenous"", ""Label"": ""Le projet implique-t-il le déplacement physique ou économique de populations ?"", ""Type"": ""Dropdown"", ""Options"": [""Nul"", ""Faible"", ""Moyen"", ""Fort""], ""Placeholder"": ""Sélectionner le niveau de risque..."" },
                            { ""Name"": ""SafeguardGenderEquity"", ""Label"": ""Le projet garantit-il la conformité en matière d'égalité homme-femme ?"", ""Type"": ""Checkbox"", ""Placeholder"": """" },
                            { ""Name"": ""SafeguardMitigationPlan"", ""Label"": ""Mesures de mitigation et de gestion des sauvegardes"", ""Type"": ""TextLong"", ""Placeholder"": ""Décrivez les mesures de prévention et d'atténuation prévues pour les risques identifiés ci-dessus..."" }
                        ]
                    }
                ]";

                FormTemplates.Add(new FormTemplate
                {
                    Name = "Modèle FEM/PMF - Initiative ICCA-GSI",
                    Description = "Formulaire sectoriel avancé pour le Programme de Micro-Financements du Fonds pour l'Environnement Mondial.",
                    JsonSchema = schemaJson,
                    Version = 1,
                    IsActive = true
                });
                SaveChanges();
            }

            // V6 : Migration et Création des tables de l'Unité de Tخطيط الاستراتيجي (Strategic Planning)
            CreateStrategicTablesIfNotExist();
            EnsureStrategicFoundationForLegacyProjects();

            // V7 : Assistant IA Rédactionnel Universel
            CreateTextAssistTablesIfNotExist();

            // Base de veille : notation /25, confirmation et fiche des 13 champs (ADR 28)
            UpgradeVeilleSchema();

            // En dernier, après toutes les mises à niveau qui fixent leurs propres valeurs par défaut :
            // toute colonne du modèle encore absente d'une ancienne table est ajoutée (ADR 34)
            EnsureModelColumnsExist();

            LogSchemaUpgradeSummary();
        }

        /// <summary>
        /// Création des tables introduites au fil des versions, pour les bases SQLite antérieures
        /// à leur apparition (EnsureCreated ne modifie pas une base existante).
        /// </summary>
        private void CreateLegacySqliteTablesIfNotExist()
        {
            RunSchemaStep("Tables AppConfigs / AppSettings", () =>
            {
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"AppConfigs\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"Provider\" TEXT NOT NULL, \"EncryptedApiKey\" TEXT NOT NULL, \"SelectedModel\" TEXT NOT NULL);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"AppSettings\" (\"Key\" TEXT PRIMARY KEY, \"Value\" TEXT NOT NULL, \"Scope\" TEXT NOT NULL, \"LastUpdated\" TEXT NOT NULL, \"UpdatedBy\" TEXT NOT NULL);");
            });

            // Tables de l'extension (taxonomies, risques, financements...)
            try
            {
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Taxonomies\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"Name\" TEXT NOT NULL, \"Description\" TEXT NOT NULL);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"TaxonomyValues\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"TaxonomyId\" INTEGER NOT NULL, \"Code\" TEXT NOT NULL, \"Label\" TEXT NOT NULL, \"ParentId\" INTEGER NULL, FOREIGN KEY(\"TaxonomyId\") REFERENCES \"Taxonomies\"(\"Id\") ON DELETE CASCADE, FOREIGN KEY(\"ParentId\") REFERENCES \"TaxonomyValues\"(\"Id\") ON DELETE SET NULL);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Outputs\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ResultId\" INTEGER NOT NULL, \"Code\" TEXT NOT NULL, \"Description\" TEXT NOT NULL, \"ResponsibleRole\" TEXT NOT NULL, \"TargetDate\" TEXT NOT NULL, FOREIGN KEY(\"ResultId\") REFERENCES \"Results\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"FormTemplates\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"Name\" TEXT NOT NULL, \"Description\" TEXT NOT NULL, \"JsonSchema\" TEXT NOT NULL, \"Version\" INTEGER NOT NULL, \"IsActive\" INTEGER NOT NULL);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"FormInstances\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"FormTemplateId\" INTEGER NOT NULL, \"ProjectId\" INTEGER NOT NULL, \"JsonAnswers\" TEXT NOT NULL, \"LastUpdatedAt\" TEXT NOT NULL, FOREIGN KEY(\"FormTemplateId\") REFERENCES \"FormTemplates\"(\"Id\") ON DELETE CASCADE, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Risks\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"Category\" TEXT NOT NULL, \"Description\" TEXT NOT NULL, \"Probability\" INTEGER NOT NULL, \"Impact\" INTEGER NOT NULL, \"MitigationMeasure\" TEXT NOT NULL, \"ResponsibleRole\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, \"CreatedAt\" TEXT NOT NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"SeMilestones\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"Title\" TEXT NOT NULL, \"Description\" TEXT NOT NULL, \"TargetDate\" TEXT NOT NULL, \"Frequency\" TEXT NOT NULL, \"ResponsibleRole\" TEXT NOT NULL, \"ExpectedDeliverable\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"FundingSources\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"Name\" TEXT NOT NULL, \"Type\" TEXT NOT NULL, \"NatureOfFunding\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, \"TotalAmountAllocated\" TEXT NOT NULL, \"Currency\" TEXT NOT NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"BudgetItemContributions\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"BudgetItemId\" INTEGER NOT NULL, \"FundingSourceId\" INTEGER NOT NULL, \"AllocatedAmount\" TEXT NOT NULL, FOREIGN KEY(\"BudgetItemId\") REFERENCES \"BudgetItems\"(\"Id\") ON DELETE CASCADE, FOREIGN KEY(\"FundingSourceId\") REFERENCES \"FundingSources\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"SafeguardEvaluations\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"Category\" TEXT NOT NULL, \"RiskLevel\" TEXT NOT NULL, \"Description\" TEXT NOT NULL, \"MitigationAction\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"StandardIndicators\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"Name\" TEXT NOT NULL, \"Description\" TEXT NOT NULL, \"Category\" TEXT NOT NULL, \"Unit\" TEXT NOT NULL);");
            }
            catch (Exception ex) { AppLogger.Error("Schema", "Échec d'une étape de création/mise à niveau du schéma.", ex); }

            // Ajouter automatiquement les nouvelles colonnes si la base existe déjà
            try
            {
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"AiLogs\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"EngineUsed\" TEXT NOT NULL, \"PromptSummary\" TEXT NOT NULL, \"ResponseTimeMs\" INTEGER NOT NULL, \"IsSuccess\" INTEGER NOT NULL, \"ErrorMessage\" TEXT NOT NULL, \"Timestamp\" TEXT NOT NULL);");
            }
            catch (Exception ex) { AppLogger.Error("Schema", "Échec d'une étape de création/mise à niveau du schéma.", ex); }

            // Créer les tables des nouvelles fonctionnalités
            try
            {
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"ProblemNodes\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"Title\" TEXT NOT NULL, \"Description\" TEXT NOT NULL, \"NodeType\" TEXT NOT NULL, \"ParentNodeId\" INTEGER NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE, FOREIGN KEY(\"ParentNodeId\") REFERENCES \"ProblemNodes\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"ProjectDocuments\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"FileName\" TEXT NOT NULL, \"FilePath\" TEXT NOT NULL, \"DocumentType\" TEXT NOT NULL, \"AiSummary\" TEXT NOT NULL, \"UploadedAt\" TEXT NOT NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"MeetingRooms\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"RoomName\" TEXT NOT NULL, \"RoomUrl\" TEXT NOT NULL, \"CreatedAt\" TEXT NOT NULL, \"Platform\" TEXT NOT NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"ActivityReports\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ActivityId\" INTEGER NOT NULL, \"ReportedByUserId\" INTEGER NULL, \"ReportDate\" TEXT NOT NULL, \"Narrative\" TEXT NOT NULL, \"ExecutionRate\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, \"PhotoPaths\" TEXT NOT NULL, FOREIGN KEY(\"ActivityId\") REFERENCES \"Activities\"(\"Id\") ON DELETE CASCADE, FOREIGN KEY(\"ReportedByUserId\") REFERENCES \"Users\"(\"Id\") ON DELETE SET NULL);");
            }
            catch (Exception ex) { AppLogger.Error("Schema", "Échec d'une étape de création/mise à niveau du schéma.", ex); }

            // V3 : Nouvelles tables pour le collaboratif
            try
            {
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Users\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"Username\" TEXT NOT NULL, \"Email\" TEXT NOT NULL, \"PasswordHash\" TEXT NOT NULL, \"IsSystemAdmin\" INTEGER NOT NULL, \"CreatedAt\" TEXT NOT NULL);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"ProjectRoles\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"ProjectId\" INTEGER NOT NULL, \"UserId\" INTEGER NOT NULL, \"RoleName\" TEXT NOT NULL, FOREIGN KEY(\"ProjectId\") REFERENCES \"Projects\"(\"Id\") ON DELETE CASCADE, FOREIGN KEY(\"UserId\") REFERENCES \"Users\"(\"Id\") ON DELETE CASCADE);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"AuditLogs\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"EntityType\" TEXT NOT NULL, \"EntityId\" INTEGER NOT NULL, \"ActionUserId\" INTEGER NOT NULL, \"ActionType\" TEXT NOT NULL, \"OldValue\" TEXT NOT NULL, \"NewValue\" TEXT NOT NULL, \"Timestamp\" TEXT NOT NULL, FOREIGN KEY(\"ActionUserId\") REFERENCES \"Users\"(\"Id\") ON DELETE SET NULL);");
                Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Notifications\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"UserId\" INTEGER NOT NULL, \"Title\" TEXT NOT NULL, \"Message\" TEXT NOT NULL, \"IsRead\" INTEGER NOT NULL, \"CreatedAt\" TEXT NOT NULL, FOREIGN KEY(\"UserId\") REFERENCES \"Users\"(\"Id\") ON DELETE CASCADE);");
            }
            catch (Exception ex) { AppLogger.Error("Schema", "Échec d'une étape de création/mise à niveau du schéma.", ex); }

            // V5 : Nouvelles tables pour les contributions et votes (travail collaboratif distribué)
            try
            {
                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""Contributions"" (
                    ""Id"" TEXT PRIMARY KEY,
                    ""EntityType"" TEXT NOT NULL,
                    ""EntityId"" TEXT NOT NULL,
                    ""FieldName"" TEXT NOT NULL,
                    ""BaseValue"" TEXT,
                    ""ProposedValue"" TEXT,
                    ""AuthorEmail"" TEXT NOT NULL,
                    ""AuthorName"" TEXT NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""BaseVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Status"" TEXT NOT NULL DEFAULT 'Proposed',
                    ""DriveFileId"" TEXT,
                    ""ReviewedAt"" TEXT,
                    ""ReviewedByEmail"" TEXT,
                    ""ReviewNotes"" TEXT
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""Votes"" (
                    ""Id"" TEXT PRIMARY KEY,
                    ""ContributionId"" TEXT NOT NULL,
                    ""VoterEmail"" TEXT NOT NULL,
                    ""VoterName"" TEXT NOT NULL,
                    ""Approve"" INTEGER NOT NULL,
                    ""Comment"" TEXT,
                    ""At"" TEXT NOT NULL,
                    ""DriveFileId"" TEXT,
                    FOREIGN KEY(""ContributionId"") REFERENCES ""Contributions""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Votes_ContributionId_VoterEmail"" ON ""Votes"" (""ContributionId"", ""VoterEmail"");");
            }
            catch (Exception ex) { AppLogger.Error("Schema", "Échec d'une étape de création/mise à niveau du schéma.", ex); }
        }

        /// <summary>Migre les anciens identifiants de modèles Gemini obsolètes vers la version 3.6.</summary>
        private void MigrateObsoleteGeminiModels()
        {
            try
            {
                var oldConfig = AppConfigs.FirstOrDefault();
                if (oldConfig != null)
                {
                    bool changed = false;
                    if (oldConfig.SelectedModel == "gemini-1.5-flash" || oldConfig.SelectedModel == "gemini-2.5-flash")
                    {
                        oldConfig.SelectedModel = "gemini-3.6-flash";
                        changed = true;
                    }
                    else if (oldConfig.SelectedModel == "gemini-1.5-pro" || oldConfig.SelectedModel == "gemini-2.5-pro")
                    {
                        oldConfig.SelectedModel = "gemini-3.6-pro";
                        changed = true;
                    }

                    if (changed)
                    {
                        SaveChanges();
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("Schema", "Migration des modèles Gemini obsolètes impossible.", ex); }
        }

        private void CreateStrategicTablesIfNotExist()
        {
            AddColumnIfMissing("Projects", "AllowCloudAi", "INTEGER NOT NULL DEFAULT 0");

            try
            {
                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""StrategicFoundations"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""ProjectId"" INTEGER NOT NULL UNIQUE,
                    ""IdeaTitle"" TEXT NOT NULL DEFAULT '',
                    ""TargetZone"" TEXT NOT NULL DEFAULT '',
                    ""TargetBeneficiaries"" TEXT NOT NULL DEFAULT '',
                    ""Sector"" TEXT NOT NULL DEFAULT '',
                    ""EstimatedDurationMonths"" INTEGER NOT NULL DEFAULT 12,
                    ""EstimatedBudget"" NUMERIC NOT NULL DEFAULT 0,
                    ""Currency"" TEXT NOT NULL DEFAULT 'EUR',
                    ""PotentialDonor"" TEXT NOT NULL DEFAULT '',
                    ""DonorPriorities"" TEXT NOT NULL DEFAULT '',
                    ""CompletenessScore"" INTEGER NOT NULL DEFAULT 0,
                    ""CoreProblemStatement"" TEXT NOT NULL DEFAULT '',
                    ""ProblemEvidenceData"" TEXT NOT NULL DEFAULT '',
                    ""SelectedStrategicOptionRationale"" TEXT NOT NULL DEFAULT '',
                    ""GeneralStrategicObjective"" TEXT NOT NULL DEFAULT '',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""ProjectId"") REFERENCES ""Projects""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""DataSources"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Title"" TEXT NOT NULL DEFAULT '',
                    ""SourceType"" TEXT NOT NULL DEFAULT 'Document',
                    ""FilePath"" TEXT NOT NULL DEFAULT '',
                    ""ReliabilityLevel"" TEXT NOT NULL DEFAULT 'High',
                    ""PublicationDate"" TEXT NULL,
                    ""ExtractedKeyInsights"" TEXT NOT NULL DEFAULT '',
                    ""RawExtractedText"" TEXT NOT NULL DEFAULT '',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""ContextIndicators"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Name"" TEXT NOT NULL DEFAULT '',
                    ""BaselineValue"" TEXT NOT NULL DEFAULT '',
                    ""Unit"" TEXT NOT NULL DEFAULT '',
                    ""Year"" INTEGER NOT NULL DEFAULT 2026,
                    ""SourceName"" TEXT NOT NULL DEFAULT '',
                    ""Notes"" TEXT NOT NULL DEFAULT '',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""Stakeholders"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Name"" TEXT NOT NULL DEFAULT '',
                    ""Category"" TEXT NOT NULL DEFAULT 'Beneficiary',
                    ""Role"" TEXT NOT NULL DEFAULT '',
                    ""PowerLevel"" INTEGER NOT NULL DEFAULT 3,
                    ""InterestLevel"" INTEGER NOT NULL DEFAULT 3,
                    ""Expectations"" TEXT NOT NULL DEFAULT '',
                    ""EngagementStrategy"" TEXT NOT NULL DEFAULT 'ManageClosely',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""SwotItems"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Category"" TEXT NOT NULL DEFAULT 'Strength',
                    ""Statement"" TEXT NOT NULL DEFAULT '',
                    ""SourceEvidence"" TEXT NOT NULL DEFAULT '',
                    ""ImportanceWeight"" INTEGER NOT NULL DEFAULT 3,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""PestelItems"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Dimension"" TEXT NOT NULL DEFAULT 'Political',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""ImpactOnProject"" TEXT NOT NULL DEFAULT 'Neutral',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""NeedsAssessments"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""NeedDescription"" TEXT NOT NULL DEFAULT '',
                    ""TargetGroupAffected"" TEXT NOT NULL DEFAULT '',
                    ""UrgencyScore"" INTEGER NOT NULL DEFAULT 3,
                    ""FeasibilityScore"" INTEGER NOT NULL DEFAULT 3,
                    ""ImpactScore"" INTEGER NOT NULL DEFAULT 3,
                    ""PriorityRank"" INTEGER NOT NULL DEFAULT 1,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""StrategicOptions"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""OptionTitle"" TEXT NOT NULL DEFAULT '',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""FeasibilityRating"" NUMERIC NOT NULL DEFAULT 3.0,
                    ""ImpactRating"" NUMERIC NOT NULL DEFAULT 3.0,
                    ""CostEffectivenessRating"" NUMERIC NOT NULL DEFAULT 3.0,
                    ""IsSelected"" INTEGER NOT NULL DEFAULT 0,
                    ""DecisionRationale"" TEXT NOT NULL DEFAULT '',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""TheoryOfChangeNodes"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""NodeType"" TEXT NOT NULL DEFAULT 'Activity',
                    ""Title"" TEXT NOT NULL DEFAULT '',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""LinkedAssumptions"" TEXT NOT NULL DEFAULT '',
                    ""OrderIndex"" INTEGER NOT NULL DEFAULT 0,
                    ""ParentNodeId"" INTEGER NULL,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""StrategicObjectives"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Code"" TEXT NOT NULL DEFAULT 'SO1',
                    ""Title"" TEXT NOT NULL DEFAULT '',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""IsSpecificObjective"" INTEGER NOT NULL DEFAULT 1,
                    ""IsSpecific"" INTEGER NOT NULL DEFAULT 1,
                    ""IsMeasurable"" INTEGER NOT NULL DEFAULT 1,
                    ""IsAchievable"" INTEGER NOT NULL DEFAULT 1,
                    ""IsRelevant"" INTEGER NOT NULL DEFAULT 1,
                    ""IsTimeBound"" INTEGER NOT NULL DEFAULT 1,
                    ""TargetTimeline"" TEXT NOT NULL DEFAULT '',
                    ""LinkedNeedIds"" TEXT NOT NULL DEFAULT '',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""StrategicRisks"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Code"" TEXT NOT NULL DEFAULT 'R01',
                    ""Category"" TEXT NOT NULL DEFAULT 'Operational',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""Probability"" INTEGER NOT NULL DEFAULT 3,
                    ""Impact"" INTEGER NOT NULL DEFAULT 3,
                    ""MitigationStrategy"" TEXT NOT NULL DEFAULT '',
                    ""ResponsibleParty"" TEXT NOT NULL DEFAULT '',
                    ""Status"" TEXT NOT NULL DEFAULT 'Active',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""StrategicAssumptions"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""StrategicFoundationId"" INTEGER NOT NULL,
                    ""Statement"" TEXT NOT NULL DEFAULT '',
                    ""AssociatedLevel"" TEXT NOT NULL DEFAULT 'ActivityToOutput',
                    ""ValidationMechanism"" TEXT NOT NULL DEFAULT '',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL,
                    FOREIGN KEY(""StrategicFoundationId"") REFERENCES ""StrategicFoundations""(""Id"") ON DELETE CASCADE
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""StrategicLinks"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""ProjectId"" INTEGER NOT NULL,
                    ""SourceEntityType"" TEXT NOT NULL DEFAULT '',
                    ""SourceEntityId"" INTEGER NOT NULL DEFAULT 0,
                    ""TargetEntityType"" TEXT NOT NULL DEFAULT '',
                    ""TargetEntityId"" INTEGER NOT NULL DEFAULT 0,
                    ""LinkType"" TEXT NOT NULL DEFAULT '',
                    ""Status"" TEXT NOT NULL DEFAULT 'Synced',
                    ""LastSyncValue"" TEXT NULL,
                    ""LastSyncAt"" TEXT NOT NULL,
                    ""Notes"" TEXT NULL,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL
                );");

                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""AuditLogEntries"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""ProjectId"" INTEGER NULL,
                    ""ActionType"" TEXT NOT NULL DEFAULT '',
                    ""UserEmail"" TEXT NOT NULL DEFAULT '',
                    ""Details"" TEXT NOT NULL DEFAULT '',
                    ""OverrideReason"" TEXT NOT NULL DEFAULT '',
                    ""ImpactSummary"" TEXT NOT NULL DEFAULT '',
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                    ""DeletedAt"" TEXT NULL,
                    ""DeletedBy"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""GlobalId"" TEXT,
                    ""DriveFileId"" TEXT,
                    ""SyncVersion"" INTEGER NOT NULL DEFAULT 1,
                    ""Version"" INTEGER NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" TEXT,
                    ""ContributionStatus"" TEXT NOT NULL DEFAULT 'Official',
                    ""AuthorEmail"" TEXT,
                    ""AuthorName"" TEXT,
                    ""AuthorColor"" TEXT,
                    ""ApprovedBy"" TEXT,
                    ""ApprovedAt"" TEXT,
                    ""LastModifiedAt"" TEXT NOT NULL
                );");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Schema", "Strategic Table Creation Warning", ex);
            }
        }

        public void EnsureStrategicFoundationForLegacyProjects()
        {
            try
            {
                var existingProjects = Projects.ToList();
                foreach (var p in existingProjects)
                {
                    var foundation = StrategicFoundations.IgnoreQueryFilters().FirstOrDefault(sf => sf.ProjectId == p.Id);
                    if (foundation == null)
                    {
                        foundation = new StrategicFoundation
                        {
                            ProjectId = p.Id,
                            IdeaTitle = p.Title,
                            TargetZone = p.InterventionZone,
                            EstimatedBudget = p.TotalBudget,
                            Currency = Project.CurrencyOf(p),
                            CoreProblemStatement = p.Justification,
                            GeneralStrategicObjective = p.GeneralObjective,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "Migration"
                        };
                        StrategicFoundations.Add(foundation);
                    }
                }
                SaveChanges();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Schema", "Legacy Migration Warning", ex);
            }
        }

#pragma warning disable EF1002 // Types SQL et noms de tables constants ou issus du modèle EF, jamais une saisie utilisateur.
        private void CreateTextAssistTablesIfNotExist()
        {
            try
            {

                bool isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
                string pkDef = isPostgres ? "SERIAL PRIMARY KEY" : "INTEGER PRIMARY KEY AUTOINCREMENT";
                string intType = isPostgres ? "INTEGER" : "INTEGER";
                string textType = "TEXT";

                Database.ExecuteSqlRaw($@"CREATE TABLE IF NOT EXISTS ""TextVersionHistories"" (
                    ""Id"" {pkDef},
                    ""GlobalId"" {textType} NOT NULL DEFAULT '',
                    ""ProjectId"" {intType} NULL,
                    ""FieldIdentifier"" {textType} NOT NULL DEFAULT '',
                    ""FieldSemanticType"" {textType} NOT NULL DEFAULT 'Generique',
                    ""ActionType"" {textType} NOT NULL DEFAULT 'Enrichir',
                    ""OriginalText"" {textType} NOT NULL DEFAULT '',
                    ""ProposedText"" {textType} NOT NULL DEFAULT '',
                    ""FinalText"" {textType} NOT NULL DEFAULT '',
                    ""ContextUsedJson"" {textType} NULL,
                    ""WarningsJson"" {textType} NULL,
                    ""ProviderUsed"" {textType} NOT NULL DEFAULT '',
                    ""ModelUsed"" {textType} NOT NULL DEFAULT '',
                    ""CreatedAt"" {textType} NOT NULL,
                    ""CreatedBy"" {textType} NULL
                );");

                Database.ExecuteSqlRaw($@"CREATE TABLE IF NOT EXISTS ""TextAssistAuditLogs"" (
                    ""Id"" {pkDef},
                    ""ProjectId"" {intType} NULL,
                    ""FieldIdentifier"" {textType} NOT NULL DEFAULT '',
                    ""ActionType"" {textType} NOT NULL DEFAULT '',
                    ""ProviderUsed"" {textType} NOT NULL DEFAULT '',
                    ""ModelUsed"" {textType} NOT NULL DEFAULT '',
                    ""WasAccepted"" {intType} NOT NULL DEFAULT 0,
                    ""WasModified"" {intType} NOT NULL DEFAULT 0,
                    ""OriginalLength"" {intType} NOT NULL DEFAULT 0,
                    ""FinalLength"" {intType} NOT NULL DEFAULT 0,
                    ""ElapsedMilliseconds"" {intType} NOT NULL DEFAULT 0,
                    ""CreatedAt"" {textType} NOT NULL,
                    ""CreatedBy"" {textType} NULL
                );");

                Database.ExecuteSqlRaw($@"CREATE TABLE IF NOT EXISTS ""TextAssistSettings"" (
                    ""Id"" {pkDef},
                    ""ProjectId"" {intType} NULL,
                    ""UserId"" {textType} NULL,
                    ""TargetLength"" {textType} NOT NULL DEFAULT 'Equivalent',
                    ""ToneRegister"" {textType} NOT NULL DEFAULT 'InstitutionnelBailleur',
                    ""OutputLanguage"" {textType} NOT NULL DEFAULT 'Auto',
                    ""CustomInstruction"" {textType} NULL,
                    ""AllowCloudAi"" {intType} NOT NULL DEFAULT 1,
                    ""UpdatedAt"" {textType} NOT NULL
                );");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Schema", "TextAssist Table Creation Warning", ex);
            }

            // V8 : وحدة رادار المشاريع الذكي (Radar des Projets) وخلاصات RSS
            try
            {
                bool isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
                string pkDef = isPostgres ? "SERIAL PRIMARY KEY" : "INTEGER PRIMARY KEY AUTOINCREMENT";
                string intType = isPostgres ? "INTEGER" : "INTEGER";
                string textType = "TEXT";

                Database.ExecuteSqlRaw($@"CREATE TABLE IF NOT EXISTS ""grants_pipeline"" (
                    ""Id"" {pkDef},
                    ""ExternalId"" {textType} NOT NULL DEFAULT '',
                    ""Title"" {textType} NOT NULL DEFAULT '',
                    ""Donor"" {textType} NOT NULL DEFAULT '',
                    ""OriginalUrl"" {textType} NOT NULL DEFAULT '',
                    ""EstimatedBudget"" NUMERIC NULL,
                    ""Currency"" {textType} NULL,
                    ""Deadline"" {textType} NULL,
                    ""SummaryAr"" {textType} NOT NULL DEFAULT '',
                    ""SummaryFr"" {textType} NOT NULL DEFAULT '',
                    ""RelevanceScore"" REAL NOT NULL DEFAULT 0,
                    ""MatchedAxes"" {textType} NOT NULL DEFAULT '',
                    ""Rationale"" {textType} NOT NULL DEFAULT '',
                    ""PipelineStatus"" {textType} NOT NULL DEFAULT 'Mise sous veille',
                    ""AddedAt"" {textType} NOT NULL,
                    ""Notes"" {textType} NOT NULL DEFAULT '',
                    ""IsDeleted"" {intType} NOT NULL DEFAULT 0,
                    ""DeletedAt"" {textType} NULL,
                    ""GlobalId"" {textType} NOT NULL DEFAULT '',
                    ""DriveFileId"" {textType} NULL,
                    ""SyncVersion"" {intType} NOT NULL DEFAULT 1,
                    ""Version"" {intType} NOT NULL DEFAULT 1,
                    ""UpdatedByEmail"" {textType} NULL,
                    ""ContributionStatus"" {textType} NOT NULL DEFAULT 'Official'
                );");

                Database.ExecuteSqlRaw($@"CREATE TABLE IF NOT EXISTS ""rss_items_cache"" (
                    ""Id"" {pkDef},
                    ""FeedId"" {textType} NOT NULL DEFAULT '',
                    ""FeedName"" {textType} NOT NULL DEFAULT '',
                    ""Donor"" {textType} NOT NULL DEFAULT '',
                    ""ExternalId"" {textType} NOT NULL DEFAULT '',
                    ""Title"" {textType} NOT NULL DEFAULT '',
                    ""CleanDescription"" {textType} NOT NULL DEFAULT '',
                    ""Link"" {textType} NOT NULL DEFAULT '',
                    ""PublishDate"" {textType} NULL,
                    ""FetchedAt"" {textType} NOT NULL,
                    ""IsRead"" {intType} NOT NULL DEFAULT 0,
                    ""IsHidden"" {intType} NOT NULL DEFAULT 0,
                    ""IsInPipeline"" {intType} NOT NULL DEFAULT 0,
                    ""AiScore"" REAL NULL,
                    ""AiScoreRationale"" {textType} NULL,
                    ""AiMatchedAxes"" {textType} NULL,
                    ""AiSummaryAr"" {textType} NULL,
                    ""AiSummaryFr"" {textType} NULL,
                    ""AiEstimatedBudget"" NUMERIC NULL,
                    ""AiCurrency"" {textType} NULL,
                    ""AiDeadline"" {textType} NULL,
                    ""IsScored"" {intType} NOT NULL DEFAULT 0
                );");

                Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_grants_pipeline_ExternalId"" ON ""grants_pipeline"" (""ExternalId"");");
                Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_rss_items_cache_ExternalId"" ON ""rss_items_cache"" (""ExternalId"");");
                Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_rss_items_cache_FeedId"" ON ""rss_items_cache"" (""FeedId"");");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Schema", "Radar Table Creation Warning", ex);
            }
        }

#pragma warning restore EF1002

        /// <summary>
        /// Colonnes de la base de veille sur grants_pipeline (les tables veille_a_venir et veille_non_retenus
        /// viennent du modèle via EnsureModelTablesExist). Idempotent, pour SQLite et PostgreSQL.
        /// </summary>
        private void UpgradeVeilleSchema()
        {
            const string table = "grants_pipeline";
            const string text = "TEXT NOT NULL DEFAULT ''";
            const string note = "INTEGER NOT NULL DEFAULT 0";

            AddColumnIfMissing(table, "VeilleId", text);
            AddColumnIfMissing(table, "Priority", text);
            AddColumnIfMissing(table, "ScorePertinence", note);
            AddColumnIfMissing(table, "ScoreEligibilite", note);
            AddColumnIfMissing(table, "ScoreFinancier", note);
            AddColumnIfMissing(table, "ScoreFaisabilite", note);
            AddColumnIfMissing(table, "ScoreStrategique", note);
            AddColumnIfMissing(table, "ScoreTotal", note);
            AddColumnIfMissing(table, "ScoreJustification", text);
            AddColumnIfMissing(table, "Confirmation", "TEXT NOT NULL DEFAULT 'non confirmé (agrégateur)'");
            AddColumnIfMissing(table, "VerifiedAt", NullableDateTimeColumn);
            AddColumnIfMissing(table, "VeilleAxes", text);
            AddColumnIfMissing(table, "CallType", text);
            AddColumnIfMissing(table, "Duration", text);
            AddColumnIfMissing(table, "Zone", text);
            AddColumnIfMissing(table, "Eligibility", text);
            AddColumnIfMissing(table, "Cofinancing", text);
            AddColumnIfMissing(table, "OverheadCosts", text);
            AddColumnIfMissing(table, "DetectionSource", text);
            AddColumnIfMissing(table, "ConsortiumAdvice", text);

            // « Mise sous veille » (statut d'origine du Radar) devient « Nouveau » dans le cycle de veille.
            RunSchemaStep("Statuts de veille grants_pipeline", () =>
                Database.ExecuteSqlRaw(@"UPDATE ""grants_pipeline"" SET ""PipelineStatus"" = 'Nouveau' WHERE ""PipelineStatus"" = 'Mise sous veille';"));
        }
    }
}
