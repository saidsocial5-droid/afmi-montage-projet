using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    public enum TextEnhancementMode
    {
        Ameliorer,
        Enrichir,
        Detailler,
        Reformuler,
        Generer,
        Corriger
    }

    public enum AiEnhancementAction
    {
        None,
        Replace,
        Append
    }

    public class TextEnhancementResult
    {
        public bool IsSuccess { get; set; }
        public string EnhancedText { get; set; } = string.Empty;
        public string EngineUsed { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public class TextEnhancementService
    {
        public async Task<TextEnhancementResult> EnhanceAsync(
            string text,
            TextEnhancementMode mode,
            string? fieldLabel = null,
            int? projectId = null,
            string? customInstruction = null,
            string? tone = null,
            bool isSelection = false)
        {
            try
            {
                // 1. Récupération du contexte complet du projet si disponible
                string projectContext = string.Empty;
                if (projectId.HasValue && projectId.Value > 0)
                {
                    try
                    {
                        projectContext = ProjectContextBuilder.BuildFullContext(projectId.Value);
                    }
                    catch
                    {
                        // Le contexte est optionnel, on continue sans bloquer
                    }
                }

                // 2. Consigne système selon le mode
                string systemInstruction = BuildSystemInstruction(mode);

                // 3. Construction du prompt enrichi
                string prompt = BuildPrompt(text, mode, fieldLabel, projectContext, customInstruction, tone, isSelection);

                // 4. Appel du routeur IA (Cloud ou Local Ollama)
                var router = new AiRouterService();
                // Réponse dans la langue du texte d'origine (ADR 26) : le modèle peut sinon répondre dans la langue des consignes
                // (une langue demandée dans la consigne libre de l'utilisateur prime)
                var sourceLanguage = Language.LanguageGuard.Detect(text);
                string? requested = Language.LanguageGuard.ExplicitRequestedLanguage(customInstruction ?? string.Empty, string.Empty);
                string? expected = requested != null ? (requested == "multi" ? null : requested)
                                 : sourceLanguage.IsReliable ? sourceLanguage.Language : null;
                var routingResult = await router.GenerateResponseAsync(prompt, systemInstruction, expectedLanguage: expected);

                if (!routingResult.IsSuccess && string.IsNullOrWhiteSpace(routingResult.ResponseText))
                {
                    return new TextEnhancementResult
                    {
                        IsSuccess = false,
                        ErrorMessage = !string.IsNullOrWhiteSpace(routingResult.ErrorMessage)
                            ? routingResult.ErrorMessage
                            : "Impossible de contacter le moteur IA. Vérifiez votre connexion Internet ou la configuration d'Ollama local dans les Paramètres.",
                        EngineUsed = routingResult.EngineName
                    };
                }

                string cleaned = CleanAiResponse(routingResult.ResponseText);

                return new TextEnhancementResult
                {
                    IsSuccess = true,
                    EnhancedText = cleaned,
                    EngineUsed = routingResult.EngineName
                };
            }
            catch (Exception ex)
            {
                return new TextEnhancementResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Erreur lors du traitement IA : {ex.Message}"
                };
            }
        }

        private string BuildSystemInstruction(TextEnhancementMode mode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tu es un expert senior en ingénierie de projets de développement international, Gestion Axée sur les Résultats (GAR / RBM), Cadre Logique et normes des bailleurs de fonds (AFD, Union Européenne, PNUD, Banque Mondiale, BAD, USAID).");
            sb.AppendLine();

            switch (mode)
            {
                case TextEnhancementMode.Ameliorer:
                    sb.AppendLine("MISSION : AMÉLIORER la qualité rédactionnelle, le style professionnel, la clarté et l'impact du texte.");
                    sb.AppendLine("DIRECTIVES STRICTES :");
                    sb.AppendLine("1. Adopte un ton institutionnel, rigoureux, précis et élégant.");
                    sb.AppendLine("2. Respecte scrupuleusement les faits, les chiffres et les intentions de l'auteur sans déformer la réalité.");
                    sb.AppendLine("3. Supprime les redondances, le style télégraphique et les formulations maladroites.");
                    sb.AppendLine("4. Emploie le vocabulaire standard de la coopération internationale et du développement.");
                    sb.AppendLine("5. Réponds UNIQUEMENT avec le texte final amélioré, sans formule de politesse, sans titre méta, sans guillemets superflus.");
                    break;

                case TextEnhancementMode.Enrichir:
                    sb.AppendLine("MISSION : ENRICHIR et ÉTOFFER le texte en développant l'argumentation, les justifications et les précisions techniques.");
                    sb.AppendLine("DIRECTIVES STRICTES :");
                    sb.AppendLine("1. Développe les idées clés avec des arguments solides, des éléments de cadrage méthodologique et des retombées positives attendues.");
                    sb.AppendLine("2. Appuie-toi sur les données et le contexte du projet fournis pour ancrer le texte dans la réalité.");
                    sb.AppendLine("3. Évite le verbiage vide : chaque phrase ajoutée doit apporter une valeur concrète pour un évaluateur de projet.");
                    sb.AppendLine("4. Structure si nécessaire avec des phrases bien articulées et fluides.");
                    sb.AppendLine("5. Réponds UNIQUEMENT avec le texte final enrichi, sans introduction ni conclusion conversationnelle.");
                    break;

                case TextEnhancementMode.Reformuler:
                    sb.AppendLine("MISSION : REFORMULER le texte pour lui donner une nouvelle tournure, plus dynamique, percutante et orientée résultats.");
                    sb.AppendLine("DIRECTIVES STRICTES :");
                    sb.AppendLine("1. Réécris la phrase ou le paragraphe avec des structures syntaxiques modernes et actives.");
                    sb.AppendLine("2. Conserve fidèlement le sens original tout en rendant la lecture captivante et persuasive.");
                    sb.AppendLine("3. Privilégie les verbes d'action et la clarté conceptuelle.");
                    sb.AppendLine("4. Réponds UNIQUEMENT avec la proposition reformulée.");
                    break;

                case TextEnhancementMode.Generer:
                    sb.AppendLine("MISSION : PROPOSER / RÉDIGER le contenu pertinent et complet adapté au champ ciblé et au contexte du projet.");
                    sb.AppendLine("DIRECTIVES STRICTES :");
                    sb.AppendLine("1. Utilise les informations globales du projet (secteur, objectifs, zone, bénéficiaires) pour proposer une rédaction réaliste, crédible et de haute qualité.");
                    sb.AppendLine("2. Si le champ requiert des précisions techniques, formule-les selon les meilleures pratiques du secteur.");
                    sb.AppendLine("3. Réponds UNIQUEMENT avec le contenu rédigé prêt à l'emploi.");
                    break;

                case TextEnhancementMode.Corriger:
                    sb.AppendLine("MISSION : CORRIGER scrupuleusement l'orthographe, la grammaire, la syntaxe et la ponctuation.");
                    sb.AppendLine("DIRECTIVES STRICTES :");
                    sb.AppendLine("1. Corrige toutes les fautes d'accord, de conjugaison et de typographie.");
                    sb.AppendLine("2. Ne modifie pas le style ou les idées de l'auteur.");
                    sb.AppendLine("3. Réponds UNIQUEMENT avec le texte rigoureusement corrigé.");
                    break;
            }

            return sb.ToString();
        }

        private string BuildPrompt(
            string text,
            TextEnhancementMode mode,
            string? fieldLabel,
            string projectContext,
            string? customInstruction,
            string? tone,
            bool isSelection)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(fieldLabel))
            {
                sb.AppendLine($"[CHAMP DU FORMULAIRE : {fieldLabel}]");
            }

            if (!string.IsNullOrWhiteSpace(tone))
            {
                sb.AppendLine($"[TONALITÉ ET STYLE DEMANDÉS : {tone}]");
            }

            if (!string.IsNullOrWhiteSpace(customInstruction))
            {
                sb.AppendLine($"[CONSIGNE PARTICULIÈRE DE L'UTILISATEUR : {customInstruction}]");
            }

            if (!string.IsNullOrWhiteSpace(projectContext))
            {
                sb.AppendLine();
                sb.AppendLine("--- CONTEXTE GLOBAL DU PROJET ---");
                sb.AppendLine(projectContext);
                sb.AppendLine("---------------------------------");
            }

            sb.AppendLine();
            if (isSelection)
            {
                sb.AppendLine("ATTENTION : L'utilisateur a sélectionné une phrase ou un passage spécifique à traiter :");
                sb.AppendLine("\"\"\"");
                sb.AppendLine(text);
                sb.AppendLine("\"\"\"");
                sb.AppendLine("Traite uniquement cette phrase/ce passage pour qu'il s'insère parfaitement à l'endroit sélectionné.");
            }
            else
            {
                sb.AppendLine("TEXTE À TRAITER :");
                sb.AppendLine("\"\"\"");
                sb.AppendLine(string.IsNullOrWhiteSpace(text) ? "(Le champ est actuellement vide. Génère une proposition complète)." : text);
                sb.AppendLine("\"\"\"");
            }

            return sb.ToString();
        }

        private string CleanAiResponse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string text = raw.Trim();

            // Supprimer d'éventuels messages de routage [Log] au début
            if (text.StartsWith("*[") && text.Contains("*\n\n"))
            {
                int idx = text.IndexOf("*\n\n");
                text = text.Substring(idx + 4).Trim();
            }

            // Supprimer les balises de code Markdown au début et à la fin si présentes
            if (text.StartsWith("```markdown", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(11).Trim();
            }
            else if (text.StartsWith("```text", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(7).Trim();
            }
            else if (text.StartsWith("```"))
            {
                text = text.Substring(3).Trim();
            }

            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3).Trim();
            }

            // Supprimer les triples guillemets enveloppants
            if (text.StartsWith("\"\"\"") && text.EndsWith("\"\"\"") && text.Length > 6)
            {
                text = text.Substring(3, text.Length - 6).Trim();
            }

            // Supprimer d'éventuelles formules d'introduction courantes
            var introRegex = new Regex(@"^(Voici (une version|la proposition|le texte|la formulation) [^:\n]+:\s*)", RegexOptions.IgnoreCase);
            text = introRegex.Replace(text, "").Trim();

            return text;
        }
    }
}
