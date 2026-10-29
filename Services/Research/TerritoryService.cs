using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services.Strategic;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.Services.Research
{
    public class BeneficiaryTotals
    {
        public int DirectGroups { get; set; }
        public int IndirectGroups { get; set; }
        public long DirectCount { get; set; }
        public long IndirectCount { get; set; }

        /// <summary>Part des femmes parmi les bénéficiaires directs dénombrés (moyenne pondérée), si renseignée.</summary>
        public double? DirectWomenPercent { get; set; }
        public double? DirectYouthPercent { get; set; }

        public string Summary =>
            $"Directs : {DirectGroups} groupe(s){(DirectCount > 0 ? $", {DirectCount:N0} personnes/unités" : "")}" +
            (DirectWomenPercent.HasValue ? $", dont {DirectWomenPercent:0} % de femmes" : "") +
            (DirectYouthPercent.HasValue ? $", {DirectYouthPercent:0} % de jeunes" : "") +
            $" — Indirects : {IndirectGroups} groupe(s){(IndirectCount > 0 ? $", {IndirectCount:N0}" : "")}";
    }

    /// <summary>
    /// Territoire d'intervention et bénéficiaires (ADR 25). Source unique : la zone (Project.InterventionZone,
    /// StrategicFoundation.TargetZone) et le public cible (TargetBeneficiaries) en sont dérivés à chaque enregistrement,
    /// et les bénéficiaires directs sont liés aux parties prenantes sans doublon.
    /// </summary>
    public class TerritoryService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public TerritoryService(Func<AppDbContext>? dbFactory = null) => _dbFactory = dbFactory ?? (() => new AppDbContext());

        public async Task<TerritoryProfile> GetOrCreateAsync(int projectId, CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();
            var profile = await db.TerritoryProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.ProjectId == projectId, cancellationToken);
            if (profile != null) return profile;

            // Premier accès : reprise de la zone déjà saisie dans le cadrage ou le socle stratégique
            var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
            var foundation = await db.StrategicFoundations.AsNoTracking().FirstOrDefaultAsync(f => f.ProjectId == projectId, cancellationToken);
            string existingZone = !string.IsNullOrWhiteSpace(project?.InterventionZone) ? project!.InterventionZone : foundation?.TargetZone ?? string.Empty;
            return new TerritoryProfile { ProjectId = projectId, Communes = existingZone };
        }

        public async Task<List<BeneficiaryGroup>> GetBeneficiariesAsync(int projectId, CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();
            return await db.BeneficiaryGroups.AsNoTracking()
                .Where(b => b.ProjectId == projectId)
                .OrderBy(b => b.BeneficiaryType == BeneficiaryTypes.Direct ? 0 : 1).ThenBy(b => b.Id)
                .ToListAsync(cancellationToken);
        }

        public static BeneficiaryTotals ComputeTotals(IEnumerable<BeneficiaryGroup> groups)
        {
            var list = groups.ToList();
            var direct = list.Where(g => g.IsDirect).ToList();
            var indirect = list.Where(g => !g.IsDirect).ToList();

            double? Weighted(Func<BeneficiaryGroup, int?> share)
            {
                var known = direct.Where(g => g.EstimatedCount > 0 && share(g).HasValue).ToList();
                if (known.Count == 0) return null;
                double total = known.Sum(g => (double)g.EstimatedCount!.Value);
                return known.Sum(g => g.EstimatedCount!.Value * (double)share(g)!.Value) / total;
            }

            return new BeneficiaryTotals
            {
                DirectGroups = direct.Count,
                IndirectGroups = indirect.Count,
                DirectCount = direct.Sum(g => (long)(g.EstimatedCount ?? 0)),
                IndirectCount = indirect.Sum(g => (long)(g.EstimatedCount ?? 0)),
                DirectWomenPercent = Weighted(g => g.WomenPercent),
                DirectYouthPercent = Weighted(g => g.YouthPercent)
            };
        }

        /// <summary>Synthèse textuelle du public cible (alimente StrategicFoundation.TargetBeneficiaries).</summary>
        public static string BuildBeneficiariesText(IEnumerable<BeneficiaryGroup> groups)
        {
            var list = groups.ToList();
            string Describe(IEnumerable<BeneficiaryGroup> g) => string.Join(" ; ", g.Select(b => b.EstimatedCount.HasValue ? $"{b.Name} ({b.EstimatedCount:N0} {b.CountUnit})" : b.Name));
            var parts = new List<string>();
            if (list.Any(g => g.IsDirect)) parts.Add("Directs : " + Describe(list.Where(g => g.IsDirect)));
            if (list.Any(g => !g.IsDirect)) parts.Add("Indirects : " + Describe(list.Where(g => !g.IsDirect)));
            return string.Join(" — ", parts);
        }

        /// <summary>
        /// Enregistre le profil et les groupes de bénéficiaires, puis répercute la zone, le public cible
        /// et les parties prenantes bénéficiaires dans les autres étapes.
        /// </summary>
        public async Task SaveAsync(int projectId, TerritoryProfile profile, IEnumerable<BeneficiaryGroup> groups, string userEmail = "Système", CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();

            var stored = await db.TerritoryProfiles.FirstOrDefaultAsync(t => t.ProjectId == projectId, cancellationToken);
            if (stored == null)
            {
                stored = new TerritoryProfile { ProjectId = projectId };
                db.TerritoryProfiles.Add(stored);
            }
            CopyProfile(profile, stored);
            stored.UpdatedAt = DateTime.UtcNow;
            stored.LastModifiedAt = DateTime.UtcNow;

            var incoming = groups.Where(g => !string.IsNullOrWhiteSpace(g.Name)).ToList();
            var existing = await db.BeneficiaryGroups.Where(b => b.ProjectId == projectId).ToListAsync(cancellationToken);
            foreach (var removed in existing.Where(e => incoming.All(i => i.Id != e.Id)))
                db.BeneficiaryGroups.Remove(removed);

            foreach (var group in incoming)
            {
                var target = group.Id > 0 ? existing.FirstOrDefault(e => e.Id == group.Id) : null;
                if (target == null)
                {
                    target = new BeneficiaryGroup { ProjectId = projectId };
                    db.BeneficiaryGroups.Add(target);
                }
                CopyGroup(group, target);
                target.LastModifiedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            await SynchronizeAsync(db, projectId, userEmail, cancellationToken);
            Security.AuditLogger.Log("Territoire et bénéficiaires", $"Profil territorial et {incoming.Count} groupe(s) de bénéficiaires enregistrés pour le projet {projectId}.", userEmail);
        }

        /// <summary>
        /// Répercute la source unique (territoire, bénéficiaires) dans les champs dérivés des autres étapes.
        /// </summary>
        public static async Task SynchronizeAsync(AppDbContext db, int projectId, string userEmail = "Système", CancellationToken cancellationToken = default)
        {
            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
            if (project == null) return;
            var profile = await db.TerritoryProfiles.FirstOrDefaultAsync(t => t.ProjectId == projectId, cancellationToken);
            var groups = await db.BeneficiaryGroups.Where(b => b.ProjectId == projectId).ToListAsync(cancellationToken);
            var foundation = await db.StrategicFoundations.Include(f => f.Stakeholders).FirstOrDefaultAsync(f => f.ProjectId == projectId, cancellationToken);

            if (foundation == null && (profile?.HasLocation == true || groups.Any()))
            {
                foundation = new StrategicFoundation { ProjectId = projectId, IdeaTitle = project.Title, Currency = project.Currency, CreatedBy = userEmail };
                db.StrategicFoundations.Add(foundation);
            }

            // Zone d'intervention : une seule valeur dans tout le projet
            if (profile?.HasLocation == true)
            {
                string zone = profile.ZoneLabel;
                project.InterventionZone = ProjectIntegrationService.Shorten(zone, 150);
                if (foundation != null) foundation.TargetZone = zone;
            }

            if (foundation != null && groups.Any())
            {
                foundation.TargetBeneficiaries = BuildBeneficiariesText(groups);

                // Bénéficiaires directs ↔ parties prenantes (catégorie « Beneficiary »), sans doublon
                foreach (var group in groups.Where(g => g.IsDirect))
                {
                    var linked = group.LinkedStakeholderId.HasValue
                        ? foundation.Stakeholders.FirstOrDefault(s => s.Id == group.LinkedStakeholderId.Value && !s.IsDeleted)
                        : null;
                    linked ??= foundation.Stakeholders.FirstOrDefault(s => !s.IsDeleted && ProjectIntegrationService.SameMeaning(s.Name, group.Name));

                    if (linked == null)
                    {
                        linked = new StakeholderEntry
                        {
                            StrategicFoundation = foundation,
                            Name = ProjectIntegrationService.Shorten(group.Name, 200),
                            Category = "Beneficiary",
                            Role = "Bénéficiaire direct",
                            InterestLevel = 5,
                            PowerLevel = 2,
                            EngagementStrategy = "KeepInformed",
                            Expectations = group.ExpectedBenefits
                        };
                        foundation.Stakeholders.Add(linked);
                        db.Stakeholders.Add(linked);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    else if (string.IsNullOrWhiteSpace(linked.Expectations) && !string.IsNullOrWhiteSpace(group.ExpectedBenefits))
                    {
                        linked.Expectations = group.ExpectedBenefits;
                    }
                    group.LinkedStakeholderId = linked.Id;
                }
                foundation.LastModifiedAt = DateTime.UtcNow;
            }

            project.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Alimente l'analyse des besoins à partir des besoins des groupes et des problèmes du territoire
        /// (étape suivante de la planification), sans doublon. Renvoie le nombre de besoins ajoutés.
        /// </summary>
        public async Task<int> FeedNeedsAssessmentAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var proposals = new List<IntegrationProposal>();
            using (var db = _dbFactory())
            {
                foreach (var g in await db.BeneficiaryGroups.AsNoTracking().Where(b => b.ProjectId == projectId).ToListAsync(cancellationToken))
                {
                    foreach (var need in SplitItems(g.Needs))
                        proposals.Add(new IntegrationProposal { Target = IntegrationTarget.Need, Title = need, Values = new() { ["Text"] = need, ["Group"] = g.Name } });
                }

                var profile = await db.TerritoryProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.ProjectId == projectId, cancellationToken);
                if (profile != null)
                {
                    foreach (var issue in SplitItems(profile.EnvironmentalIssues).Concat(SplitItems(profile.SocialIssues)).Concat(SplitItems(profile.EconomicIssues)))
                        proposals.Add(new IntegrationProposal { Target = IntegrationTarget.Need, Title = issue, Values = new() { ["Text"] = $"Répondre à : {issue}", ["Group"] = "Population de la zone" } });
                }
            }

            if (proposals.Count == 0) return 0;
            var result = await new ProjectIntegrationService(_dbFactory).ApplyAsync(projectId, proposals);
            return result.Added;
        }

        /// <summary>Découpe un texte libre en éléments (lignes, puces ou points-virgules).</summary>
        public static IEnumerable<string> SplitItems(string? text) =>
            (text ?? string.Empty).Split(new[] { '\n', ';', '•' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().TrimStart('-', '*').Trim())
                .Where(s => s.Length >= 6);

        /// <summary>Bloc de contexte (territoire + bénéficiaires) pour les assistants IA.</summary>
        public static string BuildContextBlock(TerritoryProfile? profile, IReadOnlyCollection<BeneficiaryGroup> groups, int maxFieldLength = 300)
        {
            var sb = new StringBuilder();
            string T(string s) => ProjectIntegrationService.Shorten(s, maxFieldLength);

            if (profile != null && (profile.HasLocation || profile.FilledDiagnosticSections > 0))
            {
                sb.AppendLine("=== TERRITOIRE D'INTERVENTION ===");
                if (profile.HasLocation) sb.AppendLine($"Localisation : {profile.ZoneLabel}{(string.IsNullOrWhiteSpace(profile.AreaType) ? "" : $" — milieu : {profile.AreaType}")}");
                void Line(string label, string value) { if (!string.IsNullOrWhiteSpace(value)) sb.AppendLine($"{label} : {T(value)}"); }
                Line("Localités", profile.Localities);
                Line("Climat", profile.Climate);
                Line("Écosystèmes", profile.Ecosystems);
                Line("Ressources naturelles", profile.NaturalResources);
                Line("Ressources en eau", profile.WaterResources);
                Line("Population", profile.Population);
                Line("Ménages", profile.Households);
                Line("Pauvreté", profile.PovertyRate);
                Line("Profil social", profile.SocialProfile);
                Line("Activités économiques", profile.MainLivelihoods);
                Line("Accès aux services", profile.InfrastructureAccess);
                Line("Problèmes environnementaux", profile.EnvironmentalIssues);
                Line("Problèmes sociaux", profile.SocialIssues);
                Line("Problèmes économiques", profile.EconomicIssues);
                Line("Vulnérabilités climatiques", profile.ClimateVulnerabilities);
                Line("Acteurs institutionnels", profile.InstitutionalActors);
                Line("Atouts", profile.Opportunities);
                sb.AppendLine();
            }

            if (groups.Count > 0)
            {
                sb.AppendLine("=== BÉNÉFICIAIRES ===");
                sb.AppendLine(ComputeTotals(groups).Summary);
                foreach (var g in groups.Take(12))
                {
                    sb.AppendLine($"• [{(g.IsDirect ? "Direct" : "Indirect")}] {g.Name} — {g.CountLabel}" +
                                  (g.WomenPercent.HasValue ? $", {g.WomenPercent} % femmes" : "") +
                                  (g.YouthPercent.HasValue ? $", {g.YouthPercent} % jeunes" : "") +
                                  (string.IsNullOrWhiteSpace(g.Needs) ? "" : $" ; besoins : {T(g.Needs)}") +
                                  (string.IsNullOrWhiteSpace(g.ExpectedBenefits) ? "" : $" ; bénéfices attendus : {T(g.ExpectedBenefits)}"));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void CopyProfile(TerritoryProfile s, TerritoryProfile t)
        {
            t.Country = s.Country; t.Region = s.Region; t.Province = s.Province; t.Communes = s.Communes; t.Localities = s.Localities;
            t.AreaType = s.AreaType; t.Surface = s.Surface; t.Altitude = s.Altitude;
            t.Climate = s.Climate; t.Ecosystems = s.Ecosystems; t.NaturalResources = s.NaturalResources; t.WaterResources = s.WaterResources;
            t.Population = s.Population; t.Households = s.Households; t.PovertyRate = s.PovertyRate; t.SocialProfile = s.SocialProfile;
            t.MainLivelihoods = s.MainLivelihoods; t.InfrastructureAccess = s.InfrastructureAccess;
            t.EnvironmentalIssues = s.EnvironmentalIssues; t.SocialIssues = s.SocialIssues; t.EconomicIssues = s.EconomicIssues;
            t.ClimateVulnerabilities = s.ClimateVulnerabilities; t.InstitutionalActors = s.InstitutionalActors; t.Opportunities = s.Opportunities;
            t.Sources = s.Sources; t.Synthesis = s.Synthesis;
        }

        private static void CopyGroup(BeneficiaryGroup s, BeneficiaryGroup t)
        {
            t.Name = s.Name.Trim(); t.BeneficiaryType = s.BeneficiaryType; t.Category = s.Category;
            t.EstimatedCount = s.EstimatedCount; t.CountUnit = s.CountUnit;
            t.WomenPercent = s.WomenPercent is null ? null : Math.Clamp(s.WomenPercent.Value, 0, 100);
            t.YouthPercent = s.YouthPercent is null ? null : Math.Clamp(s.YouthPercent.Value, 0, 100);
            t.Location = s.Location; t.Characteristics = s.Characteristics; t.Needs = s.Needs;
            t.ExpectedBenefits = s.ExpectedBenefits; t.SelectionCriteria = s.SelectionCriteria;
        }
    }
}
