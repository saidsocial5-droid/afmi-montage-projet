using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    public class TextAssistResponse
    {
        public bool IsSuccess { get; set; }
        public string Proposal { get; set; } = string.Empty;
        public string ActionType { get; set; } = nameof(TextAssistActionType.Enrichir);
        public List<string> ContextUsed { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Placeholders { get; set; } = new();
        public string ProviderUsed { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
        public long ExecutionDurationMs { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service central orchestrant les actions de l'Assistant IA Rédactionnel :
    /// Enrichir, Détailler, Reformuler, avec garde-fous de fidélité, sortie structurée et persistance.
    /// </summary>
    public class TextAssistService
    {
        private readonly AiRouterService _router = new();

        /// <summary>
        /// Exécute l'action demandée (Enrichir, Détailler ou Reformuler) sur la zone de texte.
        /// </summary>
        public async Task<TextAssistResponse> ProcessActionAsync(
            TextAssistActionType action,
            string currentText,
            FieldDescriptor descriptor,
            TextAssistSettings? settings = null,
            int? projectId = null)
        {
            var sw = Stopwatch.StartNew();
            descriptor ??= new FieldDescriptor("field", "Champ de saisie");
            settings ??= await LoadSettingsAsync(projectId);

            var response = new TextAssistResponse
            {
                ActionType = action.ToString()
            };

            // 1. Extraire le contexte ciblé selon le type de champ
            string targetedContext = string.Empty;
            List<string> contextElements = new();

            if (projectId.HasValue && projectId.Value > 0)
            {
                try
                {
                    var (ctx, elems) = ProjectContextBuilder.GetTargetedContextElements(projectId.Value, descriptor);
                    targetedContext = ctx;
                    contextElements = elems;
                }
                catch
                {
                    // Contexte optionnel
                }
            }

            response.ContextUsed.AddRange(contextElements);

            // 2. Construire les instructions système et les consignes
            string systemPrompt = BuildSystemPrompt(action, descriptor, settings);
            string userPrompt = BuildUserPrompt(action, currentText, descriptor, targetedContext, settings);

            string rawResponseText = string.Empty;
            string engineUsed = "Local";

            // Langue de sortie : celle choisie, sinon celle du texte d'origine (ADR 26)
            string? outputLanguage = ResolveOutputLanguage(settings, currentText);

            try
            {
                var routeResult = await _router.GenerateResponseAsync(userPrompt, systemPrompt, expectedLanguage: outputLanguage);
                engineUsed = routeResult.EngineName;
                rawResponseText = routeResult.ResponseText;

                // Si le routeur échoue ou renvoie une erreur sans réponse exploitable
                if (!routeResult.IsSuccess && string.IsNullOrWhiteSpace(rawResponseText))
                {
                    // Fallback heuristique local garanti
                    var fallbackResult = GenerateHeuristicProposal(action, currentText, descriptor, targetedContext, settings);
                    response.IsSuccess = true;
                    response.Proposal = fallbackResult;
                    response.ProviderUsed = "Moteur Heuristique Intégré (RBM Offline)";
                    response.ModelUsed = "Rules-Engine-v2";
                    response.Warnings.Add("L'assistant a utilisé le moteur de règles RBM intégré (génération hors-ligne).");
                    sw.Stop();
                    response.ExecutionDurationMs = sw.ElapsedMilliseconds;
                    return response;
                }
            }
            catch (Exception ex)
            {
                // Fallback heuristique hors-ligne en cas d'exception réseau
                var fallbackResult = GenerateHeuristicProposal(action, currentText, descriptor, targetedContext, settings);
                response.IsSuccess = true;
                response.Proposal = fallbackResult;
                response.ProviderUsed = "Moteur Heuristique Intégré (RBM Offline)";
                response.ModelUsed = "Rules-Engine-v2";
                response.Warnings.Add($"Connexion IA indisponible ({ex.Message}). Application des règles RBM déterministes.");
                sw.Stop();
                response.ExecutionDurationMs = sw.ElapsedMilliseconds;
                return response;
            }

            // 3. Traiter et nettoyer la proposition (support JSON et texte brut)
            var parsed = ParseResponse(rawResponseText);
            string proposal = parsed.Proposal;
            if (parsed.ContextUsed.Any())
            {
                response.ContextUsed = response.ContextUsed.Union(parsed.ContextUsed).Distinct().ToList();
            }
            response.Warnings.AddRange(parsed.Warnings);
            response.Placeholders.AddRange(parsed.Placeholders);

            // 4. Contrôle automatique de fidélité (Garde-fous des invariants : chiffres, codes, dates)
            var fidelity = FidelityGuard.Verify(currentText, proposal, targetedContext);
            response.Placeholders.AddRange(fidelity.PlaceholdersFound);
            response.Placeholders = response.Placeholders.Distinct().ToList();

            if (!fidelity.IsValid && fidelity.MissingInvariants.Any())
            {
                // TENTATIVE CORRECTIVE UNIQUE (Retry avec consigne explicite)
                string retryPrompt = FidelityGuard.BuildCorrectionPrompt(proposal, fidelity.MissingInvariants);
                try
                {
                    var retryResult = await _router.GenerateResponseAsync(retryPrompt, systemPrompt, expectedLanguage: outputLanguage);
                    if (!string.IsNullOrWhiteSpace(retryResult.ResponseText))
                    {
                        var retryParsed = ParseResponse(retryResult.ResponseText);
                        var retryFidelity = FidelityGuard.Verify(currentText, retryParsed.Proposal, targetedContext);

                        // Si la tentative corrective a fonctionné ou s'est améliorée
                        if (retryFidelity.IsValid || retryFidelity.MissingInvariants.Count < fidelity.MissingInvariants.Count)
                        {
                            proposal = retryParsed.Proposal;
                            fidelity = retryFidelity;
                        }
                    }
                }
                catch
                {
                    // On conserve la proposition précédente avec l'avertissement adéquat
                }

                if (!fidelity.IsValid)
                {
                    response.Warnings.AddRange(fidelity.Warnings);
                }
            }

            // La proposition (extraite du JSON) doit être dans la langue attendue : sinon traduction automatique
            if (outputLanguage != null && Language.LanguageGuard.IsWrongLanguage(proposal, outputLanguage))
            {
                try
                {
                    var translation = await new Language.TranslationService().TranslateAsync(proposal, outputLanguage);
                    if (translation.IsSuccess && !string.IsNullOrWhiteSpace(translation.Text))
                    {
                        proposal = translation.Text;
                        response.Warnings.Add($"La réponse du modèle n'était pas en {Language.AppLanguages.Name(outputLanguage)} : elle a été traduite automatiquement.");
                        response.Warnings.AddRange(translation.Warnings);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(TextAssistService), "Traduction automatique de la proposition impossible.", ex);
                }
            }

            sw.Stop();
            response.IsSuccess = true;
            response.Proposal = proposal;
            response.ProviderUsed = engineUsed;
            response.ModelUsed = engineUsed;
            response.ExecutionDurationMs = sw.ElapsedMilliseconds;

            return response;
        }

        /// <summary>Langue choisie dans les préférences, sinon langue détectée du texte d'origine (null si indéterminée).</summary>
        public static string? ResolveOutputLanguage(TextAssistSettings settings, string sourceText)
        {
            if (settings.OutputLanguage != nameof(OutputLanguageOption.Auto))
                return Language.AppLanguages.FromLabel(settings.OutputLanguage);
            var detected = Language.LanguageGuard.Detect(sourceText);
            return detected.IsReliable ? detected.Language : null;
        }

        #region Prompt Engineering

        private string BuildSystemPrompt(TextAssistActionType action, FieldDescriptor descriptor, TextAssistSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tu es un expert senior en ingénierie de projets de développement international, Gestion Axée sur les Résultats (GAR / RBM), Cadre Logique et normes des bailleurs de fonds (AFD, Union Européenne, PNUD, Banque Mondiale, BAD, USAID).");
            sb.AppendLine();
            sb.AppendLine("RÔLE SPÉCIFIQUE DU CHAMP DANS LE CADRE LOGIQUE :");
            sb.AppendLine($"- Type : {descriptor.SemanticType}");
            sb.AppendLine($"- Règle RBM : {FieldTypeRegistry.GetSemanticGuidelines(descriptor.SemanticType)}");
            sb.AppendLine();

            sb.AppendLine("DIRECTIVE MAJEURE SELON L'ACTION :");
            switch (action)
            {
                case TextAssistActionType.Enrichir:
                    sb.AppendLine("ACTION : ENRICHIR");
                    sb.AppendLine("1. Ajoute des éléments pertinents (arguments, précisions méthodologiques, exemples ancrés, retombées attendues) en t'appuyant STRICTEMENT sur le contexte du projet fourni.");
                    sb.AppendLine("2. Le texte d'origine DOIT ÊTRE CONSERVÉ dans sa substance. Les ajouts complètent et renforcent le texte existant.");
                    sb.AppendLine("3. INTERDICTION STRICTE d'inventer des données statistiques, sources ou montants inexistants. Si une donnée chiffrée manque, utilise IMPÉRATIVEMENT la mention [à compléter : description].");
                    break;

                case TextAssistActionType.Detailler:
                    sb.AppendLine("ACTION : DÉTAILLER");
                    sb.AppendLine("1. Développe ce qui est déjà écrit (étapes chronologiques, sous-points, modalités pratiques, explications concrètes de mise en œuvre).");
                    sb.AppendLine("2. N'introduis AUCUNE affirmation nouvelle qui ne découle pas directement du texte d'origine ou du contexte du projet.");
                    sb.AppendLine("3. Le sens reste strictement identique, la profondeur et la structure augmentent.");
                    break;

                case TextAssistActionType.Reformuler:
                    sb.AppendLine("ACTION : REFORMULER");
                    sb.AppendLine("1. Réécris pour optimiser la clarté, l'élégance syntaxique et le registre professionnel adapté aux évaluateurs de bailleurs de fonds.");
                    sb.AppendLine("2. Ne modifie JAMAIS le sens, les faits, les engagements ni les chiffres.");
                    sb.AppendLine("3. La longueur reste proche de l'original (sauf demande de concision expresse).");
                    break;
            }

            sb.AppendLine();
            sb.AppendLine("PRÉFÉRENCES ET REGISTRE :");
            sb.AppendLine($"- Registre visé : {settings.ToneRegister}");
            sb.AppendLine($"- Longueur cible : {settings.TargetLength}");
            if (settings.OutputLanguage != nameof(OutputLanguageOption.Auto))
            {
                sb.AppendLine($"- Langue de sortie obligatoire : {settings.OutputLanguage}");
            }
            if (!string.IsNullOrWhiteSpace(settings.CustomInstruction))
            {
                sb.AppendLine($"- Consigne libre utilisateur : {settings.CustomInstruction}");
            }

            sb.AppendLine();
            sb.AppendLine("FORMAT DE SORTIE STRUCTURÉ OBLIGATOIRE (JSON ou Texte pur) :");
            sb.AppendLine("Tu peux répondre sous format JSON structuré comme suit :");
            sb.AppendLine(@"{
  ""proposal"": ""votre proposition complète rédigée"",
  ""context_used"": [""élément contextuel utilisé 1"", ""élément 2""],
  ""warnings"": [],
  ""placeholders"": [""[à compléter : ...] si applicable""]
}");
            sb.AppendLine("Si tu réponds en texte libre, donne DIRECTEMENT le texte rédigé sans aucune formule de politesse ni balises de méta-commentaire.");

            return sb.ToString();
        }

        private string BuildUserPrompt(
            TextAssistActionType action,
            string currentText,
            FieldDescriptor descriptor,
            string targetedContext,
            TextAssistSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"[CHAMP : {descriptor.FieldLabel}] (Identifiant: {descriptor.FieldIdentifier})");
            sb.AppendLine($"[ACTION DEMANDÉE : {action}]");

            if (!string.IsNullOrWhiteSpace(targetedContext))
            {
                sb.AppendLine();
                sb.AppendLine("--- CONTEXTE CIBLÉ DU PROJET ---");
                sb.AppendLine(targetedContext);
                sb.AppendLine("--------------------------------");
            }

            sb.AppendLine();
            sb.AppendLine("TEXTE SOURCE DU CHAMP :");
            sb.AppendLine("\"\"\"");
            sb.AppendLine(string.IsNullOrWhiteSpace(currentText) ? "(Le champ est vide. Rédige une proposition complète conforme au rôle sémantique)." : currentText);
            sb.AppendLine("\"\"\"");

            return sb.ToString();
        }

        #endregion

        #region Response Parsing & Cleaning

        private class ParsedResponse
        {
            public string Proposal { get; set; } = string.Empty;
            public List<string> ContextUsed { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
            public List<string> Placeholders { get; set; } = new();
        }

        private ParsedResponse ParseResponse(string raw)
        {
            var result = new ParsedResponse();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            string cleaned = raw.Trim();

            // Supprimer le header de routage éventuel *[Log]...*
            if (cleaned.StartsWith("*[") && cleaned.Contains("*\n\n"))
            {
                int idx = cleaned.IndexOf("*\n\n");
                cleaned = cleaned.Substring(idx + 4).Trim();
            }

            // Tentative d'analyse JSON si la réponse contient des accolades
            int jsonStart = cleaned.IndexOf('{');
            int jsonEnd = cleaned.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonCandidate = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
                try
                {
                    using var doc = JsonDocument.Parse(jsonCandidate);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("proposal", out var propEl) && !string.IsNullOrWhiteSpace(propEl.GetString()))
                    {
                        result.Proposal = propEl.GetString()!;

                        if (root.TryGetProperty("context_used", out var ctxEl) && ctxEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in ctxEl.EnumerateArray())
                            {
                                if (item.GetString() is string s && !string.IsNullOrWhiteSpace(s))
                                    result.ContextUsed.Add(s);
                            }
                        }

                        if (root.TryGetProperty("warnings", out var warnEl) && warnEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in warnEl.EnumerateArray())
                            {
                                if (item.GetString() is string s && !string.IsNullOrWhiteSpace(s))
                                    result.Warnings.Add(s);
                            }
                        }

                        if (root.TryGetProperty("placeholders", out var placeEl) && placeEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in placeEl.EnumerateArray())
                            {
                                if (item.GetString() is string s && !string.IsNullOrWhiteSpace(s))
                                    result.Placeholders.Add(s);
                            }
                        }

                        return result;
                    }
                }
                catch
                {
                    // Si ce n'est pas un JSON valide, traiter comme texte libre
                }
            }

            // Traitement en texte libre
            string freeText = cleaned;

            // Retirer balises markdown ```markdown ou ```text
            if (freeText.StartsWith("```markdown", StringComparison.OrdinalIgnoreCase))
                freeText = freeText.Substring(11).Trim();
            else if (freeText.StartsWith("```text", StringComparison.OrdinalIgnoreCase))
                freeText = freeText.Substring(7).Trim();
            else if (freeText.StartsWith("```"))
                freeText = freeText.Substring(3).Trim();

            if (freeText.EndsWith("```"))
                freeText = freeText.Substring(0, freeText.Length - 3).Trim();

            // Retirer triples guillemets enveloppants
            if (freeText.StartsWith("\"\"\"") && freeText.EndsWith("\"\"\"") && freeText.Length > 6)
                freeText = freeText.Substring(3, freeText.Length - 6).Trim();

            // Retirer formules d'introduction comme "Voici une proposition :"
            var introRegex = new Regex(@"^(Voici (une version|la proposition|le texte|la formulation) [^:\n]+:\s*)", RegexOptions.IgnoreCase);
            freeText = introRegex.Replace(freeText, "").Trim();

            result.Proposal = freeText;
            return result;
        }

        #endregion

        #region Heuristic Offline Fallback

        /// <summary>
        /// Génère une proposition heuristique déterministe basée sur les règles RBM en cas d'absence totale d'IA.
        /// </summary>
        public string GenerateHeuristicProposal(
            TextAssistActionType action,
            string currentText,
            FieldDescriptor descriptor,
            string targetedContext,
            TextAssistSettings settings)
        {
            if (string.IsNullOrWhiteSpace(currentText))
            {
                return descriptor.SemanticType switch
                {
                    FieldSemanticType.Titre => "Projet de renforcement durable des capacités communautaires et d'amélioration des conditions de vie",
                    FieldSemanticType.ProblemeCentral => "Dégradation préoccupante de l'accès aux services de base affectant durablement les populations vulnérables [à compléter : données chiffrées]",
                    FieldSemanticType.ObjectifGeneral => "Contribuer durablement à l'amélioration du bien-être socio-économique et environnemental des communautés cibles",
                    FieldSemanticType.ObjectifSpecifique => "Améliorer significativement et équitablement la couverture des besoins prioritaires d'ici la fin du projet",
                    FieldSemanticType.Resultat => "Capacités techniques et opérationnelles des bénéficiaires renforcées et mécanismes pérennes instaurés",
                    FieldSemanticType.Activite => "Organiser et animer des ateliers de formation pratique et d'accompagnement de proximité",
                    FieldSemanticType.Indicateur => "Nombre de bénéficiaires directs ayant amélioré leur situation d'au moins 30% d'ici la fin du projet [à compléter : baseline]",
                    FieldSemanticType.SourceVerification => "Rapports de mission, feuilles de présence signées et procès-verbaux de réception",
                    FieldSemanticType.Hypothese => "Maintien de la stabilité socio-politique locale et engagement continu des parties prenantes",
                    FieldSemanticType.Risque => "Retard dans la mobilisation des ressources financières ou aléas climatiques défavorables",
                    FieldSemanticType.JustificationBudgetaire => "Dépense indispensable à la réalisation directe de l'activité, estimée sur la base des prix standards du marché local",
                    _ => "Proposition rédigée selon les normes méthodologiques de la gestion axée sur les résultats"
                };
            }

            string trimmed = currentText.Trim();

            switch (action)
            {
                case TextAssistActionType.Enrichir:
                    // Ajout d'arguments méthodologiques et de pérennité en préservant le texte d'origine
                    if (descriptor.SemanticType == FieldSemanticType.Activite)
                        return $"{trimmed}, en veillant à l'implication active des parties prenantes et au respect des normes de qualité méthodologique.";
                    if (descriptor.SemanticType == FieldSemanticType.Indicateur)
                        return $"{trimmed} (désagrégé par genre et groupe d'âge, mesuré semestriellement).";
                    if (descriptor.SemanticType == FieldSemanticType.JustificationBudgetaire)
                        return $"{trimmed}. Ce montant est calculé selon les coûts unitaires de référence et garantit une utilisation efficiente des ressources allouées.";
                    return $"{trimmed} Cette mesure s'inscrit en cohérence directe avec les priorités stratégiques du projet et favorise une appropriation durable par les bénéficiaires cibles.";

                case TextAssistActionType.Detailler:
                    // Développement en étapes concrètes
                    return $"{trimmed}\n\nModalités de mise en œuvre :\n1. Cadrage préparatoire et concertation avec les acteurs concernés.\n2. Déploiement opérationnel et suivi rapproché des étapes.\n3. Évaluation des livrables et consolidation des acquis.";

                case TextAssistActionType.Reformuler:
                default:
                    // Amélioration de syntaxe sans changer le sens
                    string firstCharUpper = char.ToUpper(trimmed[0]) + (trimmed.Length > 1 ? trimmed.Substring(1) : "");
                    if (!firstCharUpper.EndsWith(".")) firstCharUpper += ".";
                    return firstCharUpper;
            }
        }

        #endregion

        #region Database Persistence

        /// <summary>
        /// Sauvegarde une version acceptée dans l'historique persistant.
        /// </summary>
        public async Task<TextVersionHistory> SaveVersionHistoryAsync(
            string fieldIdentifier,
            string originalText,
            string proposedText,
            string finalText,
            TextAssistActionType actionType,
            FieldSemanticType semanticType,
            string providerUsed,
            string modelUsed,
            List<string>? contextUsed = null,
            List<string>? warnings = null,
            int? projectId = null,
            string? userId = null)
        {
            using var db = new AppDbContext();

            var history = new TextVersionHistory
            {
                FieldIdentifier = fieldIdentifier,
                ProjectId = projectId,
                FieldSemanticType = semanticType.ToString(),
                ActionType = actionType.ToString(),
                OriginalText = originalText ?? string.Empty,
                ProposedText = proposedText ?? string.Empty,
                FinalText = finalText ?? string.Empty,
                ContextUsedJson = contextUsed != null && contextUsed.Any() ? JsonSerializer.Serialize(contextUsed) : null,
                WarningsJson = warnings != null && warnings.Any() ? JsonSerializer.Serialize(warnings) : null,
                ProviderUsed = providerUsed,
                ModelUsed = modelUsed,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId ?? SessionManager.CurrentUser?.Email ?? "User"
            };

            db.TextVersionHistories.Add(history);
            await db.SaveChangesAsync();

            return history;
        }

        /// <summary>
        /// Récupère l'historique des versions pour un champ donné.
        /// </summary>
        public async Task<List<TextVersionHistory>> GetVersionHistoryAsync(string fieldIdentifier, int? projectId = null, int maxCount = 10)
        {
            using var db = new AppDbContext();
            var query = db.TextVersionHistories.AsQueryable();

            if (projectId.HasValue)
            {
                query = query.Where(h => h.ProjectId == projectId.Value || h.ProjectId == null);
            }

            return await query
                .Where(h => h.FieldIdentifier == fieldIdentifier)
                .OrderByDescending(h => h.CreatedAt)
                .Take(maxCount)
                .ToListAsync();
        }

        /// <summary>
        /// Enregistre une entrée d'audit.
        /// </summary>
        public async Task LogAuditAsync(
            string fieldIdentifier,
            TextAssistActionType actionType,
            string providerUsed,
            string modelUsed,
            bool wasAccepted,
            bool wasModified,
            int originalLength,
            int finalLength,
            long elapsedMs,
            int? projectId = null,
            string? userId = null)
        {
            try
            {
                using var db = new AppDbContext();
                var log = new TextAssistAuditLog
                {
                    ProjectId = projectId,
                    FieldIdentifier = fieldIdentifier,
                    ActionType = actionType.ToString(),
                    ProviderUsed = providerUsed,
                    ModelUsed = modelUsed,
                    WasAccepted = wasAccepted,
                    WasModified = wasModified,
                    OriginalLength = originalLength,
                    FinalLength = finalLength,
                    ElapsedMilliseconds = elapsedMs,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId ?? SessionManager.CurrentUser?.Email ?? "User"
                };

                db.TextAssistAuditLogs.Add(log);
                await db.SaveChangesAsync();
            }
            catch
            {
                // Non bloquant
            }
        }

        /// <summary>
        /// Charge les préférences utilisateur/projet ou renvoie les valeurs par défaut.
        /// </summary>
        public async Task<TextAssistSettings> LoadSettingsAsync(int? projectId = null, string? userId = null)
        {
            try
            {
                using var db = new AppDbContext();
                var query = db.TextAssistSettings.AsQueryable();

                if (projectId.HasValue)
                {
                    var projSettings = await query.FirstOrDefaultAsync(s => s.ProjectId == projectId.Value);
                    if (projSettings != null) return projSettings;
                }

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var userSettings = await query.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (userSettings != null) return userSettings;
                }

                var defaultSettings = await query.FirstOrDefaultAsync();
                if (defaultSettings != null) return defaultSettings;
            }
            catch { }

            return new TextAssistSettings
            {
                TargetLength = nameof(TargetLengthOption.Equivalent),
                ToneRegister = nameof(ToneRegisterOption.InstitutionnelBailleur),
                OutputLanguage = nameof(OutputLanguageOption.Auto),
                AllowCloudAi = true
            };
        }

        /// <summary>
        /// Sauvegarde les préférences utilisateur/projet.
        /// </summary>
        public async Task SaveSettingsAsync(TextAssistSettings settings)
        {
            using var db = new AppDbContext();
            settings.UpdatedAt = DateTime.UtcNow;

            if (settings.Id > 0)
            {
                db.TextAssistSettings.Update(settings);
            }
            else
            {
                db.TextAssistSettings.Add(settings);
            }

            await db.SaveChangesAsync();
        }

        #endregion
    }
}
