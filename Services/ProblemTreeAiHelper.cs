using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AFMI_Montage_projet_2.Services
{
    public static class ProblemTreeAiHelper
    {
        private static bool IsOfflineMock(string response)
        {
            return response.Contains("Ollama non détecté") || string.IsNullOrWhiteSpace(response);
        }

        public static async Task<string> ReformulateTitleAsync(string originalTitle, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(originalTitle)) return originalTitle;
            string prompt = $"Contexte du projet : {projectContext}\n\nReformule ce titre de nœud de problème de manière très concise et percutante (1 phrase courte maximum) : {originalTitle}";
            string sys = "Tu es un expert en montage de projet. Réponds impérativement en FRANÇAIS. Ne fais AUCUNE phrase d'introduction ou de conclusion (ex: 'Voici la proposition :', 'En tant qu'expert...'). Renvoie UNIQUEMENT le texte final.";
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return originalTitle + " (Amélioré)";
            
            return result.Replace("**", "").Replace("\"", "").Trim();
        }

        public static async Task<string> CorrectTitleAsync(string originalTitle, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(originalTitle)) return originalTitle;
            string prompt = $"Corrige uniquement l'orthographe et la grammaire de ce titre, sans rien ajouter d'autre : {originalTitle}";
            string sys = "Tu corriges uniquement le texte en FRANÇAIS. Aucune introduction, aucun commentaire. Renvoie UNIQUEMENT le texte corrigé.";
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return originalTitle.Replace("z", "").Replace("probléme", "problème");
            return result.Replace("\"", "").Trim();
        }

        public static async Task<string> GenerateDescriptionAsync(string title, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(title)) return title;
            string prompt = $"Contexte du projet : {projectContext}\n\nRédige une description professionnelle courte (3 à 4 phrases) expliquant ce fait NÉGATIF (ce problème, cette difficulté) dans le cadre de ce projet : {title}. ATTENTION: Rédige la description de maniére é décrire un PROBLÈME, pas une solution ni un objectif.";
            string sys = "Tu es un expert en montage de projet. Réponds impérativement en FRANÇAIS. Ne fais AUCUNE phrase d'introduction (ex: 'Voici la description :'). Renvoie UNIQUEMENT la description finale.";
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return $"Le problème '{title}' représente un défi central pour le développement local. Ses impacts se font ressentir tant sur le plan économique que social, fragilisant la résilience de la communauté. L'objectif est d'atténuer ces effets par une approche participative et durable.";
            return result.Trim();
        }

        public static async Task<string> ReformulateDescriptionAsync(string originalDescription, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(originalDescription)) return originalDescription;
            string prompt = $"Contexte du projet : {projectContext}\n\nReformule ce texte de manière plus académique et formelle pour un dossier de financement de projet : {originalDescription}";
            string sys = "Tu es un expert en montage de projet. Réponds impérativement en FRANÇAIS. Ne fais AUCUNE phrase d'introduction ou de conclusion. Renvoie UNIQUEMENT le texte reformulé.";
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return "Le défi principal réside dans les faiblesses structurelles actuelles qui impactent négativement le développement de la zone. En l'absence de mesures correctives, la situation socio-économique risque de se détériorer davantage.";
            return result.Trim();
        }

        public static async Task<string> CorrectDescriptionAsync(string originalDescription, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(originalDescription)) return originalDescription;
            string prompt = $"Corrige uniquement l'orthographe et la grammaire de ce texte, sans le modifier sur le fond ni rajouter de commentaires : {originalDescription}";
            string sys = "Tu corriges uniquement le texte en FRANÇAIS. Aucune introduction, aucun commentaire. Renvoie UNIQUEMENT le texte corrigé.";
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return originalDescription.Replace("z", "").Replace("probléme", "problème");
            return result.Trim();
        }

        public static async Task<List<AiSuggestion>> AnalyzeAsListAsync(string description, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(description)) return new List<AiSuggestion>();
            
            string prompt = $"Contexte du projet : {projectContext}\n\nAnalyse cette description de problème de projet. Retourne exactement 3 points d'amélioration spécifiques. Réponds au format JSON strict, avec un tableau contenant des objets avec 'Text' (le point d'amélioration) et 'NeedsInput' (true ou false) et 'Placeholder' (exemple de réponse).\n\nTexte :\n{description}";
            string sys = "Tu es un expert. Tu renvoies UNIQUEMENT du JSON en FRANÇAIS, aucun texte avant ou après. Format: [{{\"Text\":\"...\", \"NeedsInput\":true, \"Placeholder\":\"...\"}]";
            
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return GetMockAnalysis();

            try
            {
                var match = Regex.Match(result, @"\[.*\]", RegexOptions.Singleline);
                if (match.Success) result = match.Value;
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestions = JsonSerializer.Deserialize<List<AiSuggestion>>(result, options);
                if (suggestions != null && suggestions.Any()) return suggestions;
            }
            catch { }
            return GetMockAnalysis();
        }

        public static async Task<List<AiSuggestion>> SuggestAsListAsync(string title, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(title)) return new List<AiSuggestion>();
            
            string prompt = $"Contexte du projet : {projectContext}\n\nPour ce problème (\"{title}\"), liste 2 causes et 2 conséquences probables. ATTENTION: Tu DOIS générer des constats négatifs existants (des problèmes). NE PROPOSE PAS de solutions, ni d'objectifs, ni d'activités (ex: ne dis pas 'Conception de...' ou 'Mise en place de...'). Réponds au format JSON strict, avec un tableau contenant des objets avec 'Text' (la cause ou conséquence) et 'NeedsInput' (true ou false) et 'Placeholder' (exemple de réponse).\n\nProblème :\n{title}";
            string sys = "Tu es un expert. Tu renvoies UNIQUEMENT du JSON en FRANÇAIS, aucun texte avant ou après. Format: [{{\"Text\":\"... (Cause)\", \"NeedsInput\":false, \"Placeholder\":\"\"}]";
            
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return GetMockSuggestions(title);

            try
            {
                var match = Regex.Match(result, @"\[.*\]", RegexOptions.Singleline);
                if (match.Success) result = match.Value;
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestions = JsonSerializer.Deserialize<List<AiSuggestion>>(result, options);
                if (suggestions != null && suggestions.Any()) return suggestions;
            }
            catch { }
            return GetMockSuggestions(title);
        }

        public static async Task<List<AiSuggestion>> SuggestTitlesForNodeAsync(string nodeType, string projectContext, string parentTitle)
        {
            string prompt;
            if (nodeType == "Core")
            {
                prompt = $"Contexte du projet : {projectContext}\n\nPropose 3 formulations professionnelles pour le problème central de ce projet. Un problème doit TOUJOURS être un fait négatif existant (ex: 'Faible accés é...', 'Dégradation de...'), et SURTOUT PAS une solution ou un objectif (ex: interdit de dire 'Conception d'un programme'). Réponds au format JSON strict: [{{\"Text\":\"...\", \"NeedsInput\":false}}].";
            }
            else if (nodeType == "Cause")
            {
                prompt = $"Contexte du projet : {projectContext}\n\nLe problème parent est : \"{parentTitle}\"\n\nPropose 3 CAUSES racines de ce problème. Une cause doit être formulée comme un FAIT NÉGATIF (ex: 'Manque d'infrastructures...', 'Absence de...', 'Insuffisance de...'). SURTOUT PAS de solutions ni d'actions. Réponds au format JSON strict: [{{\"Text\":\"...\", \"NeedsInput\":false}}].";
            }
            else
            {
                prompt = $"Contexte du projet : {projectContext}\n\nLe problème parent est : \"{parentTitle}\"\n\nPropose 3 CONSéQUENCES de ce problème. Une conséquence doit être un impact NÉGATIF existant ou futur (ex: 'Baisse des revenus', 'Hausse de l'exode'). SURTOUT PAS de solutions ni d'actions. Réponds au format JSON strict: [{{\"Text\":\"...\", \"NeedsInput\":false}}].";
            }
            string sys = "Tu es un expert en conception de projets. Tu renvoies UNIQUEMENT du JSON en FRANÇAIS, aucun texte avant ou aprés.";
            
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            try
            {
                var match = Regex.Match(result, @"\[.*\]", RegexOptions.Singleline);
                if (match.Success) result = match.Value;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestions = JsonSerializer.Deserialize<List<AiSuggestion>>(result, options);
                if (suggestions != null && suggestions.Any()) return suggestions;
            }
            catch { }
            return new List<AiSuggestion> { new AiSuggestion { Text = "Impossible de générer des suggestions valides." } };
        }

        public static async Task<string> MergeIntoDescriptionAsync(string originalDescription, string addedPoints, string projectContext)
        {
            if (string.IsNullOrWhiteSpace(originalDescription)) return addedPoints;
            if (string.IsNullOrWhiteSpace(addedPoints)) return originalDescription;
            
            string prompt = $"Contexte du projet : {projectContext}\n\nIntègre de manière fluide et professionnelle les points suivants à la description existante.\n\nDescription originale:\n{originalDescription}\n\nPoints à intégrer:\n{addedPoints}";
            string sys = "Tu es un expert en montage de projet. Réponds impérativement en FRANÇAIS. Ne fais AUCUNE phrase d'introduction (ex: 'Voici la description fusionnée :'). Renvoie UNIQUEMENT la description finale améliorée.";
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result)) return originalDescription + "\n\n" + addedPoints;
            return result.Trim();
        }

        public static async Task<List<AiSuggestion>> SuggestTitlesAsync(string projectContext)
        {
            string prompt = $"Voici les informations globales du projet et les exigences éventuelles du bailleur :\n\n{projectContext}\n\nGénère 3 propositions de titres professionnels et accrocheurs pour ce projet, parfaitement alignés avec les exigences. Réponds au format JSON strict, avec un tableau contenant des objets avec 'Text' (la proposition de titre) et 'NeedsInput' (toujours false).\nFormat: [{{\"Text\":\"Titre 1\", \"NeedsInput\":false}}]";
            string sys = "Tu es un expert. Tu renvoies UNIQUEMENT du JSON en FRANÇAIS, aucun texte avant ou après.";
            
            string result = await AiAssistantService.CallAiAsync(prompt, sys);
            if (IsOfflineMock(result))
            {
                return new List<AiSuggestion>
                {
                    new AiSuggestion { Text = "Projet de Développement Durable Local", NeedsInput = false },
                    new AiSuggestion { Text = "Initiative pour la Résilience Communautaire", NeedsInput = false },
                    new AiSuggestion { Text = "Programme d'Appui au Secteur Rural", NeedsInput = false }
                };
            }

            try
            {
                var match = Regex.Match(result, @"\[.*\]", RegexOptions.Singleline);
                if (match.Success) result = match.Value;
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestions = JsonSerializer.Deserialize<List<AiSuggestion>>(result, options);
                if (suggestions != null && suggestions.Any()) return suggestions;
            }
            catch { }
            return new List<AiSuggestion> { new AiSuggestion { Text = "Erreur de génération", NeedsInput = false } };
        }

        private static List<AiSuggestion> GetMockAnalysis()
        {
            return new List<AiSuggestion>
            {
                new AiSuggestion { Text = "Préciser les données quantitatives manquantes", NeedsInput = true, Placeholder = "Ex: 80% de la population, 500 personnes affectées..." },
                new AiSuggestion { Text = "Détailler les causes profondes", NeedsInput = true, Placeholder = "Ex: Manque de financement public, raisons culturelles..." },
                new AiSuggestion { Text = "Décrire l'impact sur les personnes vulnérables", NeedsInput = true, Placeholder = "Ex: Les femmes et les enfants sont les plus touchés..." }
            };
        }

        private static List<AiSuggestion> GetMockSuggestions(string title)
        {
            return new List<AiSuggestion> {
                new AiSuggestion { Text = "Faible implication des autorités locales (Cause)", NeedsInput = false },
                new AiSuggestion { Text = "Manque de financements initiaux (Cause)", NeedsInput = false },
                new AiSuggestion { Text = "Exode rural (Conséquence)", NeedsInput = false },
                new AiSuggestion { Text = "Détail supplémentaire ?", NeedsInput = true, Placeholder = "Précisez votre idée ici..." }
            };
        }
    }

    public class AiSuggestion
    {
        public string Text { get; set; } = string.Empty;
        public bool NeedsInput { get; set; } = false;
        public string Placeholder { get; set; } = string.Empty;
    }
}





