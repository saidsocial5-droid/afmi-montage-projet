using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    public partial class RiskProposal : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _category = "Environnemental";

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private int _probability = 3;

        [ObservableProperty]
        private int _impact = 3;

        [ObservableProperty]
        private string _mitigationMeasure = string.Empty;

        [ObservableProperty]
        private string _responsibleRole = "Chef de projet";

        public int RiskScore => Probability * Impact;

        public string ScoreColor => RiskScore switch
        {
            >= 12 => "#EF4444", // Rouge (Critique)
            >= 6 => "#F59E0B",  // Orange (Moyen)
            _ => "#10B981"      // Vert (Faible)
        };

        public string ScoreLabel => RiskScore switch
        {
            >= 12 => "Critique",
            >= 6 => "Modéré",
            _ => "Faible"
        };
    }

    /// <summary>
    /// Service d'assistance IA pour l'identification, la cotation et l'audit des risques de projet.
    /// </summary>
    public class RiskAiService
    {
        public async Task<List<RiskProposal>> GenerateRisksAsync(int projectId)
        {
            string context = ProjectContextBuilder.BuildFullContext(projectId);

            string prompt = $@"Tu es un expert senior en gestion des risques de projets de développement durable et montage de dossiers pour bailleurs de fonds internationaux (GEF, PNUD, UE, Banque Mondiale, AFD).
Analyse le projet ci-dessous et identifie entre 5 et 7 risques majeurs réalistes, pertinents et spécifiques au contexte d'intervention.

Pour chaque risque, fournis :
- category : parmi 'Environnemental', 'Social', 'Financier', 'Technique', 'Gouvernance', 'Interne', 'Externe'
- description : formulation claire et précise du risque (1 à 2 phrases)
- probability : entier entre 1 (très faible) et 5 (très probable)
- impact : entier entre 1 (négligeable) et 5 (critique / arrêt du projet)
- mitigation : mesure d'atténuation ou plan de contingence opérationnel concret
- responsibleRole : fonction ou acteur responsable du suivi (ex: Coordonnateur de projet, Responsable financier, Comité de pilotage...)

Données du projet :
---
{context}
---

Réponds EXCLUSIVEMENT en JSON valide avec la structure suivante :
[
  {{
    ""category"": ""Environnemental"",
    ""description"": ""Description claire du risque..."",
    ""probability"": 3,
    ""impact"": 4,
    ""mitigation"": ""Mesure d'atténuation concrète..."",
    ""responsibleRole"": ""Chef de projet""
  }}
]";

            string sys = "Tu es un auditeur et expert en gestion des risques de projets. Réponds STRICTEMENT en JSON sans aucun texte explicatif avant ou après.";

            try
            {
                string raw = await AiAssistantService.CallAiAsync(prompt, sys);
                var proposals = ParseRisksJson(raw);
                if (proposals != null && proposals.Count > 0)
                {
                    return proposals;
                }
            }
            catch
            {
                // En cas d'indisponibilité du service IA ou d'erreur réseau, bascule sur les risques de secours
            }

            return GenerateFallbackRisks();
        }

        public async Task<(string Mitigation, string ResponsibleRole)> SuggestMitigationAsync(string riskDescription, string category, int projectId)
        {
            if (string.IsNullOrWhiteSpace(riskDescription))
            {
                return ("Veuillez d'abord renseigner la description du risque.", "Responsable de projet");
            }

            string context = ProjectContextBuilder.BuildFullContext(projectId);

            string prompt = $@"Pour ce projet :
{context}

Risque identifié (Catégorie: {category}) :
""{riskDescription}""

Propose une mesure d'atténuation / contingence préventive et opérationnelle, ainsi que le rôle responsable adapté.
Réponds au format :
MESURE: [Texte de la mesure d'atténuation concrète en 2 à 3 phrases]
ROLE: [Rôle responsable]";

            string sys = "Tu es un expert en gestion des risques de projets. Réponds en français de manière directe et concise selon le format demandé.";

            try
            {
                string raw = await AiAssistantService.CallAiAsync(prompt, sys);
                string mitigation = string.Empty;
                string role = "Coordonnateur de projet";

                var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("MESURE:", StringComparison.OrdinalIgnoreCase))
                    {
                        mitigation = line.Substring("MESURE:".Length).Trim();
                    }
                    else if (line.StartsWith("ROLE:", StringComparison.OrdinalIgnoreCase))
                    {
                        role = line.Substring("ROLE:".Length).Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(mitigation))
                {
                    mitigation = raw.Trim();
                }

                return (mitigation, role);
            }
            catch (Exception ex)
            {
                return ($"Mesure préventive : Mettre en place un plan de suivi continu et des protocoles de concertation locale ({ex.Message}).", "Coordonnateur terrain");
            }
        }

        public async Task<(int Probability, int Impact, string Explanation)> EstimateScoreAsync(string riskDescription, int projectId)
        {
            if (string.IsNullOrWhiteSpace(riskDescription))
            {
                return (3, 3, "Description vide.");
            }

            string context = ProjectContextBuilder.BuildFullContext(projectId);

            string prompt = $@"Évalue la sévérité de ce risque dans le cadre du projet :
Projet : {context}
Risque : ""{riskDescription}""

Donne une note de Probabilité de 1 à 5 et une note d'Impact de 1 à 5.
Réponds au format :
P: [1-5]
I: [1-5]
EXPLICATION: [1 phrase courte justifiant la note]";

            string sys = "Tu es un évaluateur de risques. Réponds de façon concise selon le format demandé.";

            try
            {
                string raw = await AiAssistantService.CallAiAsync(prompt, sys);
                int p = 3;
                int i = 3;
                string exp = string.Empty;

                var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("P:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(Regex.Match(line, @"\d+").Value, out p);
                    }
                    else if (line.StartsWith("I:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(Regex.Match(line, @"\d+").Value, out i);
                    }
                    else if (line.StartsWith("EXPLICATION:", StringComparison.OrdinalIgnoreCase))
                    {
                        exp = line.Substring("EXPLICATION:".Length).Trim();
                    }
                }

                p = Math.Clamp(p, 1, 5);
                i = Math.Clamp(i, 1, 5);
                return (p, i, exp);
            }
            catch
            {
                return (3, 3, "Estimation standard.");
            }
        }

        public async Task<string> AuditRiskMatrixAsync(List<Risk> risks, int projectId)
        {
            string context = ProjectContextBuilder.BuildFullContext(projectId);

            var sb = new StringBuilder();
            sb.AppendLine("Liste des risques actuels du projet :");
            if (risks.Count == 0)
            {
                sb.AppendLine("Aucun risque n'est actuellement enregistré.");
            }
            else
            {
                foreach (var r in risks)
                {
                    int score = r.Probability * r.Impact;
                    sb.AppendLine($"- [{r.Category}] {r.Description} | Probabilité: {r.Probability}, Impact: {r.Impact} (Score: {score}/25) | Statut: {r.Status} | Atténuation: {(string.IsNullOrWhiteSpace(r.MitigationMeasure) ? "AUCUNE" : r.MitigationMeasure)}");
                }
            }

            string prompt = $@"Tu es un auditeur qualité pour bailleurs de fonds internationaux (GEF, Union Européenne, AFD, PNUD).
Effectue un diagnostic et audit rigoureux de la matrice des risques de ce projet.

Contexte du projet :
{context}

{sb}

Rédige un rapport d'audit concis et percutant structuré comme suit :
# 🛡️ Rapport d'Audit & Diagnostic de la Matrice des Risques

### 📊 1. Analyse de la Répartition et Criticité
(Évalue la distribution des scores, le nombre de risques en zone rouge/critique, et le niveau de réalisme des cotations)

### ⚠️ 2. Risques Critiques ou Lacunes d'Atténuation
(Mets en évidence les risques prioritaires insuffisamment couverts ou dépourvus de plan de contingence robuste)

### 🔍 3. Catégories Oubliées ou Sous-Évaluées
(Identifie les catégories de risques typiques manquantes au vu des activités du projet : ex. climatique, foncier, social, trésorerie)

### 💡 4. Recommandations Clés pour le Dossier Bailleurs
(3 à 4 préconisations opérationnelles immédiates pour sécuriser l'approbation du projet)";

            string sys = "Tu es un expert auditeur de projets. Rédige en français avec rigueur, style professionnel et mise en page Markdown soignée.";

            try
            {
                return await AiAssistantService.CallAiAsync(prompt, sys);
            }
            catch (Exception ex)
            {
                return $"# 🛡️ Diagnostic de la Matrice des Risques\n\n* **Risques enregistrés :** {risks.Count}\n* **Risques critiques (Score ≥ 12) :** {risks.Count(r => r.Probability * r.Impact >= 12)}\n\nUne erreur réseau s'est produite lors de l'appel à l'IA : {ex.Message}";
            }
        }

        private List<RiskProposal>? ParseRisksJson(string raw)
        {
            string cleaned = raw.Trim();
            var mdMatch = Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
            if (mdMatch.Success) cleaned = mdMatch.Groups[1].Value.Trim();

            int firstBracket = cleaned.IndexOf('[');
            int lastBracket = cleaned.LastIndexOf(']');
            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                cleaned = cleaned.Substring(firstBracket, lastBracket - firstBracket + 1);
            }

            using var doc = JsonDocument.Parse(cleaned);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var list = new List<RiskProposal>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var p = new RiskProposal();
                if (el.TryGetProperty("category", out var cat)) p.Category = cat.GetString() ?? "Environnemental";
                if (el.TryGetProperty("description", out var desc)) p.Description = desc.GetString() ?? string.Empty;
                if (el.TryGetProperty("probability", out var prob)) p.Probability = Math.Clamp(prob.GetInt32(), 1, 5);
                if (el.TryGetProperty("impact", out var imp)) p.Impact = Math.Clamp(imp.GetInt32(), 1, 5);
                if (el.TryGetProperty("mitigation", out var mit)) p.MitigationMeasure = mit.GetString() ?? string.Empty;
                if (el.TryGetProperty("responsibleRole", out var role)) p.ResponsibleRole = role.GetString() ?? "Chef de projet";

                if (!string.IsNullOrWhiteSpace(p.Description))
                {
                    list.Add(p);
                }
            }

            return list;
        }

        private List<RiskProposal> GenerateFallbackRisks()
        {
            return new List<RiskProposal>
            {
                new RiskProposal
                {
                    Category = "Environnemental",
                    Description = "Aléas climatiques extrêmes (sécheresse prolongée, gel tardif ou incendies) affectant les parcelles d'intervention",
                    Probability = 3,
                    Impact = 4,
                    MitigationMeasure = "Sélection d'essences forestières et pastorales résilientes, installation de systèmes d'irrigation économe et protocoles de veille météo",
                    ResponsibleRole = "Expert forestier / Responsable technique"
                },
                new RiskProposal
                {
                    Category = "Social",
                    Description = "Réticence ou faible adhésion initiale de certains usagers ou éleveurs locaux aux nouvelles règles de mise en défens",
                    Probability = 3,
                    Impact = 3,
                    MitigationMeasure = "Ateliers de concertation participative précoce, signature de chartes locales et intégration des notables et coopératives",
                    ResponsibleRole = "Médiateur communautaire / Coordonnateur terrain"
                },
                new RiskProposal
                {
                    Category = "Financier",
                    Description = "Retard dans le déblocage des tranches de subvention ou fluctuation des coûts des intrants et matériaux",
                    Probability = 2,
                    Impact = 4,
                    MitigationMeasure = "Plan de trésorerie prévisionnel avec fonds de roulement d'urgence et conventions claires avec les bailleurs",
                    ResponsibleRole = "Responsable administratif et financier"
                },
                new RiskProposal
                {
                    Category = "Technique",
                    Description = "Difficultés d'accès aux zones de montagne isolées pendant la saison hivernale pour les chantiers",
                    Probability = 3,
                    Impact = 3,
                    MitigationMeasure = "Calendrier d'exécution adapté concentrant les travaux de terrain au printemps et en automne",
                    ResponsibleRole = "Coordonnateur des opérations"
                },
                new RiskProposal
                {
                    Category = "Gouvernance",
                    Description = "Changements institutionnels au niveau des partenaires communaux ou lenteurs dans les autorisations administratives",
                    Probability = 2,
                    Impact = 3,
                    MitigationMeasure = "Formalisation de conventions-cadres pérennes et comités de pilotage trimestriels avec les autorités locales",
                    ResponsibleRole = "Directeur de projet / Président de l'association"
                }
            };
        }
    }
}
