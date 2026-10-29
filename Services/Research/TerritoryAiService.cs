using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services.Strategic;

namespace AFMI_Montage_projet_2.Services.Research
{
    public class BeneficiarySuggestion
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = BeneficiaryTypes.Direct;
        public string Category { get; set; } = string.Empty;
        public int? EstimatedCount { get; set; }
        public string CountUnit { get; set; } = "personnes";
        public int? WomenPercent { get; set; }
        public int? YouthPercent { get; set; }
        public string Characteristics { get; set; } = string.Empty;
        public string Needs { get; set; } = string.Empty;
        public string ExpectedBenefits { get; set; } = string.Empty;
        public string SelectionCriteria { get; set; } = string.Empty;
    }

    /// <summary>Profil territorial proposé par l'IA (mêmes rubriques que <see cref="TerritoryProfile"/>).</summary>
    public class TerritorySuggestion
    {
        public string Region { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string Communes { get; set; } = string.Empty;
        public string AreaType { get; set; } = string.Empty;
        public string Altitude { get; set; } = string.Empty;
        public string Climate { get; set; } = string.Empty;
        public string Ecosystems { get; set; } = string.Empty;
        public string NaturalResources { get; set; } = string.Empty;
        public string WaterResources { get; set; } = string.Empty;
        public string Population { get; set; } = string.Empty;
        public string Households { get; set; } = string.Empty;
        public string PovertyRate { get; set; } = string.Empty;
        public string SocialProfile { get; set; } = string.Empty;
        public string MainLivelihoods { get; set; } = string.Empty;
        public string InfrastructureAccess { get; set; } = string.Empty;
        public string EnvironmentalIssues { get; set; } = string.Empty;
        public string SocialIssues { get; set; } = string.Empty;
        public string EconomicIssues { get; set; } = string.Empty;
        public string ClimateVulnerabilities { get; set; } = string.Empty;
        public string InstitutionalActors { get; set; } = string.Empty;
        public string Opportunities { get; set; } = string.Empty;
        public string Sources { get; set; } = string.Empty;
        public string Synthesis { get; set; } = string.Empty;
        public List<BeneficiarySuggestion> Beneficiaries { get; set; } = new();
    }

    public class TerritorySuggestionResult
    {
        public int FilledFields { get; set; }
        public List<BeneficiaryGroup> NewGroups { get; set; } = new();
        public bool UsedWebSearch { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Assistance IA au diagnostic territorial et à l'identification des bénéficiaires (ADR 25).
    /// La proposition ne remplit que les rubriques vides et n'ajoute que les groupes absents ;
    /// les chiffres doivent être sourcés ou marqués « à vérifier ».
    /// </summary>
    public class TerritoryAiService
    {
        private readonly StructuredAiPrompter _prompter;

        public TerritoryAiService(StructuredAiPrompter? prompter = null) => _prompter = prompter ?? StructuredAiPrompter.Instance;

        public async Task<TerritorySuggestion> SuggestAsync(int projectId, TerritoryProfile current, IReadOnlyCollection<BeneficiaryGroup> groups)
        {
            bool webSearch = AiAssistantService.CloudProviderSupportsSearch();
            string context = ProjectContextBuilder.BuildFullContext(projectId);
            string prompt =
                "Établis le diagnostic du territoire d'intervention et identifie les bénéficiaires directs et indirects du projet décrit ci-dessous.\n" +
                $"Localisation connue : {(current.HasLocation ? current.ZoneLabel : "à déduire du contexte")}{(string.IsNullOrWhiteSpace(current.AreaType) ? "" : $" ({current.AreaType})")}.\n" +
                "Règles :\n" +
                "- Rubriques déjà renseignées par l'équipe (à ne pas contredire) : " + string.Join(", ", FilledLabels(current)) + ".\n" +
                "- Données chiffrées : indique la source et l'année entre parenthèses (ex. RGPH 2024, HCP, monographie communale). Si tu n'es pas sûr, écris « (à vérifier) ». N'invente jamais un chiffre précis sans source.\n" +
                "- Problèmes : formulations concrètes, une par ligne.\n" +
                "- Bénéficiaires : groupes distincts, réalistes pour le projet ; « Direct » = bénéficie des activités, « Indirect » = bénéficie des retombées. Parts de femmes et de jeunes si pertinentes.\n" +
                (webSearch ? "- Utilise la recherche web pour vérifier les données démographiques et socio-économiques récentes.\n" : "") +
                "Réponds en français.\n\n" + ProjectIntegrationService.Shorten(context, 9000);

            var suggestion = await _prompter.GenerateStructuredAsync<TerritorySuggestion>(prompt, SchemaExample,
                "Tu es expert en diagnostic territorial et en développement rural au Maroc.", enableSearch: webSearch);
            return suggestion;
        }

        /// <summary>Remplit les rubriques vides du profil et renvoie les groupes de bénéficiaires absents.</summary>
        public static TerritorySuggestionResult Apply(TerritorySuggestion s, TerritoryProfile target, IReadOnlyCollection<BeneficiaryGroup> existingGroups)
        {
            var result = new TerritorySuggestionResult();
            void Fill(Func<TerritoryProfile, string> get, Action<TerritoryProfile, string> set, string value)
            {
                if (string.IsNullOrWhiteSpace(get(target)) && !string.IsNullOrWhiteSpace(value))
                {
                    set(target, value.Trim());
                    result.FilledFields++;
                }
            }

            Fill(t => t.Region, (t, v) => t.Region = v, s.Region);
            Fill(t => t.Province, (t, v) => t.Province = v, s.Province);
            Fill(t => t.Communes, (t, v) => t.Communes = v, s.Communes);
            Fill(t => t.AreaType, (t, v) => t.AreaType = v, s.AreaType);
            Fill(t => t.Altitude, (t, v) => t.Altitude = v, s.Altitude);
            Fill(t => t.Climate, (t, v) => t.Climate = v, s.Climate);
            Fill(t => t.Ecosystems, (t, v) => t.Ecosystems = v, s.Ecosystems);
            Fill(t => t.NaturalResources, (t, v) => t.NaturalResources = v, s.NaturalResources);
            Fill(t => t.WaterResources, (t, v) => t.WaterResources = v, s.WaterResources);
            Fill(t => t.Population, (t, v) => t.Population = v, s.Population);
            Fill(t => t.Households, (t, v) => t.Households = v, s.Households);
            Fill(t => t.PovertyRate, (t, v) => t.PovertyRate = v, s.PovertyRate);
            Fill(t => t.SocialProfile, (t, v) => t.SocialProfile = v, s.SocialProfile);
            Fill(t => t.MainLivelihoods, (t, v) => t.MainLivelihoods = v, s.MainLivelihoods);
            Fill(t => t.InfrastructureAccess, (t, v) => t.InfrastructureAccess = v, s.InfrastructureAccess);
            Fill(t => t.EnvironmentalIssues, (t, v) => t.EnvironmentalIssues = v, s.EnvironmentalIssues);
            Fill(t => t.SocialIssues, (t, v) => t.SocialIssues = v, s.SocialIssues);
            Fill(t => t.EconomicIssues, (t, v) => t.EconomicIssues = v, s.EconomicIssues);
            Fill(t => t.ClimateVulnerabilities, (t, v) => t.ClimateVulnerabilities = v, s.ClimateVulnerabilities);
            Fill(t => t.InstitutionalActors, (t, v) => t.InstitutionalActors = v, s.InstitutionalActors);
            Fill(t => t.Opportunities, (t, v) => t.Opportunities = v, s.Opportunities);
            Fill(t => t.Synthesis, (t, v) => t.Synthesis = v, s.Synthesis);
            if (!string.IsNullOrWhiteSpace(s.Sources))
            {
                string tagged = $"Proposition IA à vérifier : {s.Sources}";
                if (!target.Sources.Contains(s.Sources, StringComparison.OrdinalIgnoreCase))
                    target.Sources = string.IsNullOrWhiteSpace(target.Sources) ? tagged : target.Sources + " ; " + tagged;
            }

            foreach (var b in s.Beneficiaries.Where(b => !string.IsNullOrWhiteSpace(b.Name)))
            {
                if (existingGroups.Concat(result.NewGroups).Any(g => ProjectIntegrationService.SameMeaning(g.Name, b.Name))) continue;
                result.NewGroups.Add(new BeneficiaryGroup
                {
                    ProjectId = target.ProjectId,
                    Name = ProjectIntegrationService.Shorten(b.Name, 200),
                    BeneficiaryType = string.Equals(b.Type, BeneficiaryTypes.Indirect, StringComparison.OrdinalIgnoreCase) ? BeneficiaryTypes.Indirect : BeneficiaryTypes.Direct,
                    Category = b.Category,
                    EstimatedCount = b.EstimatedCount is > 0 ? b.EstimatedCount : null,
                    CountUnit = string.IsNullOrWhiteSpace(b.CountUnit) ? "personnes" : b.CountUnit,
                    WomenPercent = b.WomenPercent is >= 0 and <= 100 ? b.WomenPercent : null,
                    YouthPercent = b.YouthPercent is >= 0 and <= 100 ? b.YouthPercent : null,
                    Characteristics = b.Characteristics,
                    Needs = b.Needs,
                    ExpectedBenefits = b.ExpectedBenefits,
                    SelectionCriteria = b.SelectionCriteria
                });
            }

            result.UsedWebSearch = AiAssistantService.CloudProviderSupportsSearch();
            result.Message = $"{result.FilledFields} rubrique(s) complétée(s) et {result.NewGroups.Count} groupe(s) de bénéficiaires proposé(s)" +
                             (result.UsedWebSearch ? " (avec recherche Internet)." : " (sans recherche Internet : vérifiez les chiffres).") +
                             " Les rubriques déjà renseignées n'ont pas été modifiées. Relisez puis enregistrez.";
            return result;
        }

        private static IEnumerable<string> FilledLabels(TerritoryProfile p)
        {
            var map = new (string Label, string Value)[]
            {
                ("climat", p.Climate), ("écosystèmes", p.Ecosystems), ("ressources naturelles", p.NaturalResources), ("eau", p.WaterResources),
                ("population", p.Population), ("ménages", p.Households), ("pauvreté", p.PovertyRate), ("profil social", p.SocialProfile),
                ("activités économiques", p.MainLivelihoods), ("services", p.InfrastructureAccess), ("problèmes environnementaux", p.EnvironmentalIssues),
                ("problèmes sociaux", p.SocialIssues), ("problèmes économiques", p.EconomicIssues), ("vulnérabilités", p.ClimateVulnerabilities)
            };
            var filled = map.Where(m => !string.IsNullOrWhiteSpace(m.Value)).Select(m => $"{m.Label} = « {ProjectIntegrationService.Shorten(m.Value, 120)} »").ToList();
            return filled.Count == 0 ? new[] { "aucune" } : filled;
        }

        private const string SchemaExample = @"{
  ""Region"": ""Fès-Meknès"", ""Province"": ""Ifrane"", ""Communes"": ""Tigrigra, Timahdite"", ""AreaType"": ""Rural, Montagne"",
  ""Altitude"": ""1 600 – 2 200 m"", ""Climate"": ""Montagnard méditerranéen, hivers froids et enneigés"",
  ""Ecosystems"": ""Cédraie, chênaie verte, parcours, zones humides (dayets)"", ""NaturalResources"": ""Bois, parcours, plantes aromatiques"",
  ""WaterResources"": ""Sources, dayets, nappe phréatique"", ""Population"": ""≈ 12 000 habitants (RGPH 2024, à vérifier)"",
  ""Households"": ""≈ 2 500 ménages (à vérifier)"", ""PovertyRate"": ""(à vérifier)"", ""SocialProfile"": ""Population amazighe, forte proportion de jeunes"",
  ""MainLivelihoods"": ""Élevage ovin transhumant, céréaliculture, tourisme de nature"", ""InfrastructureAccess"": ""Pistes, accès limité aux services"",
  ""EnvironmentalIssues"": ""Surpâturage\nDépérissement du cèdre"", ""SocialIssues"": ""Exode des jeunes"", ""EconomicIssues"": ""Faible valorisation des produits locaux"",
  ""ClimateVulnerabilities"": ""Sécheresses récurrentes, vagues de froid"", ""InstitutionalActors"": ""ANEF, commune, DPA, coopératives, AGS"",
  ""Opportunities"": ""Parc national d'Ifrane, Réserve de biosphère"", ""Sources"": ""HCP RGPH 2024 ; monographie provinciale"",
  ""Synthesis"": ""Synthèse du diagnostic en 4 à 6 phrases."",
  ""Beneficiaries"": [
    {""Name"": ""Éleveurs membres des AGS"", ""Type"": ""Direct"", ""Category"": ""Éleveurs"", ""EstimatedCount"": 250, ""CountUnit"": ""personnes"", ""WomenPercent"": 10, ""YouthPercent"": 30,
     ""Characteristics"": ""Transhumants, revenus dépendants des parcours"", ""Needs"": ""Accès au fourrage ; organisation des parcours"", ""ExpectedBenefits"": ""Réduction des coûts d'alimentation du cheptel"", ""SelectionCriteria"": ""Adhésion à une AGS""},
    {""Name"": ""Population des douars riverains"", ""Type"": ""Indirect"", ""Category"": ""Ménages"", ""EstimatedCount"": 1500, ""CountUnit"": ""ménages""}
  ]
}";
    }
}
