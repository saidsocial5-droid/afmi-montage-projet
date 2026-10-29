using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    public class AiRouterService
    {
        private readonly LocalAiEngine _localEngine = new();
        private readonly CloudAiEngine _cloudEngine = new();

        /// <summary>Réglage de la langue des réponses IA : auto (langue de la demande), fr, ar ou en (ADR 26).</summary>
        public const string ResponseLanguageSetting = "AI_ResponseLanguage";

        /// <param name="expectedLanguage">Langue imposée (fr/ar/en) ; null = réglage utilisateur ou langue de la demande.</param>
        public async Task<RoutingResult> GenerateResponseAsync(string prompt, string systemInstruction = "", string taskType = "Standard", bool enableSearch = false, string? expectedLanguage = null)
        {
            // Langue de réponse : consigne explicite au modèle, puis contrôle (et traduction) de la réponse
            string? responseLanguage = ResolveResponseLanguage(prompt, systemInstruction, taskType, expectedLanguage);
            if (responseLanguage != null && taskType != Language.TranslationService.TaskType)
                systemInstruction = (string.IsNullOrWhiteSpace(systemInstruction) ? "" : systemInstruction + "\n\n") + Language.LanguageGuard.LanguageInstruction(responseLanguage);

            using var db = new AppDbContext();
            var config = db.AppConfigs.FirstOrDefault();

            bool useLocal = true;
            bool cloudAllowed = false;
            bool minimizeData = true;
            int requestLimit = 200;

            if (config != null)
            {
                useLocal = config.UseLocalByDefault;
                cloudAllowed = config.CloudOptInGranted;
                minimizeData = config.DataMinimizationEnabled;
                requestLimit = config.MonthlyRequestLimit;
            }

            IAiEngine selectedEngine = _localEngine;
            string routingLogMessage = string.Empty;

            // 1. Vérifier si l'utilisateur souhaite utiliser le Cloud
            if (!useLocal && cloudAllowed)
            {
                // 2. Vérifier la connexion internet
                if (IsInternetAvailable())
                {
                    // 3. Vérifier le plafond mensuel de requêtes cloud
                    int currentMonthCalls = GetCloudCallsCountThisMonth();
                    if (currentMonthCalls < requestLimit)
                    {
                        selectedEngine = _cloudEngine;
                    }
                    else
                    {
                        routingLogMessage = $"[Quota Dépassé] Limite mensuelle de {requestLimit} requêtes cloud atteinte. Repli sur le moteur local.";
                        selectedEngine = _localEngine;
                    }
                }
                else
                {
                    routingLogMessage = "[Pas d'Internet] Aucune connexion détectée. Repli automatique sur le moteur local.";
                    selectedEngine = _localEngine;
                }
            }
            else if (!cloudAllowed && !useLocal)
            {
                routingLogMessage = "[Sécurité] Option Cloud non consentie. Utilisation obligatoire du moteur local.";
                selectedEngine = _localEngine;
            }

            // 4. Minimisation des données pour le moteur cloud
            string finalPrompt = prompt;
            if (selectedEngine == _cloudEngine && minimizeData)
            {
                finalPrompt = MinimiseData(prompt);
            }

            var stopwatch = Stopwatch.StartNew();
            bool isSuccess = false;
            bool searchUsed = false;
            string resultText = string.Empty;
            string errorMessage = string.Empty;

            try
            {
                resultText = await selectedEngine.GenerateAsync(finalPrompt, systemInstruction, enableSearch);
                isSuccess = true;
                searchUsed = enableSearch && selectedEngine == _cloudEngine && config?.Provider == "Gemini";
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                // En cas d'échec du moteur cloud, repli d'urgence vers le moteur local
                if (selectedEngine == _cloudEngine)
                {
                    routingLogMessage = $"[Échec Cloud] Erreur : {ex.Message}. Repli d'urgence sur le moteur local.";
                    selectedEngine = _localEngine;
                    try
                    {
                        resultText = await selectedEngine.GenerateAsync(prompt, systemInstruction);
                        isSuccess = true;
                    }
                    catch (Exception localEx)
                    {
                        errorMessage += $" | Échec local : {localEx.Message}";
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
                
                // Enregistrer dans les logs d'audit
                try
                {
                    var log = new AiLog
                    {
                        EngineUsed = selectedEngine.EngineName,
                        PromptSummary = prompt.Substring(0, Math.Min(100, prompt.Length)) + "...",
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        IsSuccess = isSuccess,
                        ErrorMessage = errorMessage,
                        Timestamp = DateTime.UtcNow
                    };
                    db.AiLogs.Add(log);
                    db.SaveChanges();
                }
                catch { }
            }

            // Réponse dans une autre langue que celle attendue (fréquent avec les modèles locaux) : traduction automatique
            bool translated = false;
            if (isSuccess && responseLanguage != null && taskType != Language.TranslationService.TaskType && taskType != "StructuredRbm" &&
                Language.LanguageGuard.IsWrongLanguage(resultText, responseLanguage))
            {
                var detected = Language.LanguageGuard.Detect(resultText).Language;
                var translation = await new Language.TranslationService().TranslateAsync(resultText, responseLanguage);
                if (translation.IsSuccess && !string.IsNullOrWhiteSpace(translation.Text))
                {
                    AppLogger.Info(nameof(AiRouterService), $"Réponse IA reçue en {Language.AppLanguages.Name(detected)}, traduite en {Language.AppLanguages.Name(responseLanguage)} ({taskType}).");
                    resultText = translation.Text;
                    translated = true;
                }
            }

            // Si un message de routage ou de repli a été émis, l'ajouter à la réponse
            if (!string.IsNullOrEmpty(routingLogMessage))
            {
                resultText = $"*{routingLogMessage}*\n\n{resultText}";
            }

            return new RoutingResult
            {
                ResponseText = resultText,
                EngineName = selectedEngine.EngineName,
                IsSuccess = isSuccess,
                RoutingMessage = routingLogMessage,
                SearchUsed = searchUsed,
                ResponseLanguage = responseLanguage,
                WasTranslated = translated
            };
        }

        /// <summary>
        /// Langue attendue : imposée par l'appelant, sinon réglage utilisateur (sauf si la demande précise elle-même
        /// une langue), sinon langue dominante de la demande. Null : aucun contrôle.
        /// </summary>
        public static string? ResolveResponseLanguage(string prompt, string systemInstruction, string taskType, string? expectedLanguage)
        {
            if (!string.IsNullOrEmpty(expectedLanguage)) return Language.AppLanguages.All.Contains(expectedLanguage) ? expectedLanguage : null;
            if (taskType == Language.TranslationService.TaskType) return null;

            string setting = Language.AppLanguages.Auto;
            try { setting = SettingsService.GetSetting(ResponseLanguageSetting, Language.AppLanguages.Auto); }
            catch (Exception ex) { AppLogger.Warn(nameof(AiRouterService), "Réglage de langue IA illisible.", ex); }

            // Une langue explicitement demandée dans la consigne (« en arabe ») prime sur le réglage ; bilingue/traduction : pas de contrôle
            string? explicitLanguage = Language.LanguageGuard.ExplicitRequestedLanguage(prompt, systemInstruction);
            if (explicitLanguage != null) return explicitLanguage == "multi" ? null : explicitLanguage;
            return Language.AppLanguages.All.Contains(setting) ? setting : Language.LanguageGuard.InferExpectedLanguage(prompt, systemInstruction);
        }

        private bool IsInternetAvailable()
        {
            try
            {
                using var ping = new Ping();
                var reply = ping.Send("8.8.8.8", 1500); // Google DNS
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        private int GetCloudCallsCountThisMonth()
        {
            try
            {
                using var db = new AppDbContext();
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                
                // Compter les logs cloud de ce mois
                return db.AiLogs
                    .AsEnumerable() // Ramener en mémoire pour filtrer proprement par date en SQLite
                    .Count(log => log.Timestamp >= startOfMonth && log.EngineUsed.Contains("Cloud") && log.IsSuccess);
            }
            catch
            {
                return 0;
            }
        }

        private string MinimiseData(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remplacer les adresses e-mails par [EMAIL_MASQUÉ]
            string emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
            string result = Regex.Replace(text, emailPattern, "[EMAIL_MASQUÉ]");

            // Remplacer les numéros de téléphone par [TÉL_MASQUÉ]
            string phonePattern = @"\+?[0-9]{2,4}[- ]?[0-9]{3,4}[- ]?[0-9]{3,4}";
            result = Regex.Replace(result, phonePattern, "[TÉL_MASQUÉ]");

            // Remplacer les montants précis (ex: 120 500 €) par [MONTANT_MASQUÉ] pour préserver le secret financier
            string moneyPattern = @"\b\d+[\s\d]*([€$]|EUR|USD)\b";
            result = Regex.Replace(result, moneyPattern, "[MONTANT_MASQUÉ]");

            return result;
        }
    }

    public class RoutingResult
    {
        public string ResponseText { get; set; } = string.Empty;
        public string EngineName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string RoutingMessage { get; set; } = string.Empty;

        /// <summary>Vrai si la réponse s'appuie sur une recherche Internet réelle (Gemini + Google Search).</summary>
        public bool SearchUsed { get; set; }

        /// <summary>Langue attendue de la réponse (null : non contrôlée).</summary>
        public string? ResponseLanguage { get; set; }

        /// <summary>La réponse du modèle était dans une autre langue et a été traduite automatiquement.</summary>
        public bool WasTranslated { get; set; }
    }
}

