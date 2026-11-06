using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Helpers;

namespace AFMI_Montage_projet_2.Services
{
    public class AiAssistantService
    {
        private static readonly HttpClient HttpClient = new();

        static AiAssistantService()
        {
            // Définir un timeout à 100 secondes pour supporter les connexions lentes et les temps de réponse de l'IA
            HttpClient.Timeout = TimeSpan.FromSeconds(100);
        }

        private static AppConfig? GetConfig()
        {
            using var db = new AppDbContext();
            return db.AppConfigs.FirstOrDefault();
        }

        public static bool IsConfigured()
        {
            var config = GetConfig();
            if (config == null) return false;
            string key = EncryptionHelper.Unprotect(config.EncryptedApiKey);
            return !string.IsNullOrWhiteSpace(key);
        }

        public static async Task<string> CallAiAsync(string prompt, string systemInstruction = "", bool enableSearch = false)
        {
            var router = new AiRouterService();
            // La recherche Internet doit être transmise au routeur (elle était ignorée auparavant)
            var routingResult = await router.GenerateResponseAsync(prompt, systemInstruction, enableSearch: enableSearch);
            // Échec du moteur : exception (les appelants l'affichent), jamais un message d'erreur renvoyé comme réponse
            if (!routingResult.IsSuccess)
                throw new InvalidOperationException(routingResult.ErrorMessage);
            return routingResult.ResponseText;
        }

        /// <summary>Vrai si le fournisseur cloud configuré sait effectuer une recherche Internet (Gemini : Google Search).</summary>
        public static bool CloudProviderSupportsSearch()
        {
            var config = GetConfig();
            return config != null && config.Provider == "Gemini" && config.CloudOptInGranted && !config.UseLocalByDefault;
        }

        public static async Task<string> CallCloudAiAsync(string prompt, string systemInstruction = "", bool enableSearch = false)
        {
            var config = GetConfig();
            if (config == null)
                throw new Exception("L'Assistant IA n'est pas configuré. Veuillez renseigner votre clé d'API dans les paramètres.");

            string apiKey = EncryptionHelper.Unprotect(config.EncryptedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("La clé d'API IA est vide. Veuillez configurer l'IA dans les paramètres.");

            string provider = config.Provider;
            string model = config.SelectedModel;

            // Mapping de secours pour les bases de données existantes qui contiennent de vieux modèles (1.5 ou 2.5)
            if (model == "gemini-1.5-flash" || model == "gemini-2.5-flash") model = "gemini-3.6-flash";
            if (model == "gemini-1.5-pro" || model == "gemini-2.5-pro") model = "gemini-3.6-pro";

            try
            {
                if (provider == "Gemini")
                {
                    return await CallGeminiAsync(apiKey, model, prompt, systemInstruction, enableSearch);
                }
                else if (provider == "OpenAI")
                {
                    return await CallOpenAiAsync(apiKey, model, prompt, systemInstruction);
                }
                else if (provider == "Claude")
                {
                    return await CallClaudeAsync(apiKey, model, prompt, systemInstruction);
                }
                else if (provider == "DeepSeek")
                {
                    return await CallDeepSeekAsync(apiKey, model, prompt, systemInstruction);
                }
                else
                {
                    throw new Exception($"Fournisseur d'IA inconnu : {provider}");
                }
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Délai de connexion dépassé. Veuillez vérifier que vous êtes bien connecté à Internet.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Erreur de connexion réseau vers le service d'IA : {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de l'appel à l'IA ({provider}) : {ex.Message}");
            }
        }

        private static async Task<string> CallGeminiAsync(string apiKey, string model, string prompt, string systemInstruction, bool enableSearch)
        {
            // Gemini API
            // Clé dans l'en-tête x-goog-api-key, jamais dans l'URL (qui finit dans les journaux des proxys)
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            // Si une consigne système est fournie, on la concatène au prompt pour Gemini Pro
            string fullPrompt = string.IsNullOrEmpty(systemInstruction) 
                ? prompt 
                : $"{systemInstruction}\n\nConsigne de l'utilisateur :\n{prompt}";

            object requestBody;
            if (enableSearch)
            {
                requestBody = new
                {
                    contents = new[] {
                        new {
                            parts = new[] {
                                new { text = fullPrompt }
                            }
                        }
                    },
                    tools = new[] {
                        new {
                            google_search = new {}
                        }
                    },
                    generationConfig = new {
                        maxOutputTokens = 8192
                    }
                };
            }
            else
            {
                requestBody = new
                {
                    contents = new[] {
                        new {
                            parts = new[] {
                                new { text = fullPrompt }
                            }
                        }
                    },
                    generationConfig = new {
                        maxOutputTokens = 8192
                    }
                };
            }

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("x-goog-api-key", apiKey);
            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                try
                {
                    using var errDoc = JsonDocument.Parse(errorBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errorEl) &&
                        errorEl.TryGetProperty("message", out var msgEl))
                    {
                        throw new Exception($"Erreur Gemini : {msgEl.GetString()}");
                    }
                }
                catch (Exception ex) when (ex.Message.StartsWith("Erreur Gemini")) { throw; }
                catch (Exception parseEx) { AppLogger.Info(nameof(AiAssistantService), $"Corps d'erreur Gemini illisible : {parseEx.Message}"); }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new Exception("Vous avez dépassé la limite de requêtes gratuites autorisées par minute (Quota temporairement saturé). Veuillez patienter environ 1 minute avant de ré-essayer, ou désactivez l'option de recherche sur Internet.");
                }
                throw new Exception($"Erreur HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            
            // candidates[0].content.parts[*].text : avec la recherche Google, la réponse peut être répartie en plusieurs parties
            var parts = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts");

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl)) sb.Append(textEl.GetString());
            }
            return sb.ToString();
        }

        private static async Task<string> CallOpenAiAsync(string apiKey, string model, string prompt, string systemInstruction)
        {
            string url = "https://api.openai.com/v1/chat/completions";

            var requestBody = new
            {
                model = model,
                messages = new[] {
                    new { role = "system", content = string.IsNullOrEmpty(systemInstruction) ? "Tu es un assistant expert en montage de projet." : systemInstruction },
                    new { role = "user", content = prompt }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            
            var json = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                try
                {
                    using var errDoc = JsonDocument.Parse(errorBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errorEl) &&
                        errorEl.TryGetProperty("message", out var msgEl))
                    {
                        throw new Exception($"Erreur OpenAI : {msgEl.GetString()}");
                    }
                }
                catch (Exception ex) when (ex.Message.StartsWith("Erreur OpenAI")) { throw; }
                catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiAssistantService), $"Erreur ignorée : {swallowedEx.Message}"); }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new Exception("Limite de requêtes OpenAI dépassée (Quota saturé). Veuillez vérifier votre compte ou patienter une minute.");
                }
                throw new Exception($"Erreur HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);

            // Naviguer dans le JSON de OpenAI : choices[0].message.content
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return text ?? string.Empty;
        }

        private static async Task<string> CallClaudeAsync(string apiKey, string model, string prompt, string systemInstruction)
        {
            string url = "https://api.anthropic.com/v1/messages";

            var requestBody = new
            {
                model = model,
                max_tokens = 8192,
                system = systemInstruction,
                messages = new[] {
                    new { role = "user", content = prompt }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            
            var json = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                try
                {
                    using var errDoc = JsonDocument.Parse(errorBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errorEl) &&
                        errorEl.TryGetProperty("message", out var msgEl))
                    {
                        throw new Exception($"Erreur Claude : {msgEl.GetString()}");
                    }
                }
                catch (Exception ex) when (ex.Message.StartsWith("Erreur Claude")) { throw; }
                catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiAssistantService), $"Erreur ignorée : {swallowedEx.Message}"); }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new Exception("Limite de requêtes Anthropic Claude dépassée (Quota saturé). Veuillez vérifier votre compte ou patienter une minute.");
                }
                throw new Exception($"Erreur HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);

            // Naviguer dans le JSON d'Anthropic : content[0].text
            var text = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return text ?? string.Empty;
        }

        private static async Task<string> CallDeepSeekAsync(string apiKey, string model, string prompt, string systemInstruction)
        {
            string url = "https://api.deepseek.com/chat/completions";

            var requestBody = new
            {
                model = model,
                messages = new[] {
                    new { role = "system", content = string.IsNullOrEmpty(systemInstruction) ? "Tu es un assistant expert en montage de projet." : systemInstruction },
                    new { role = "user", content = prompt }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            
            var json = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                try
                {
                    using var errDoc = JsonDocument.Parse(errorBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errorEl) &&
                        errorEl.TryGetProperty("message", out var msgEl))
                    {
                        throw new Exception($"Erreur DeepSeek : {msgEl.GetString()}");
                    }
                }
                catch (Exception ex) when (ex.Message.StartsWith("Erreur DeepSeek")) { throw; }
                catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiAssistantService), $"Erreur ignorée : {swallowedEx.Message}"); }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new Exception("Limite de requêtes DeepSeek dépassée (Quota temporairement saturé ou solde insuffisant). Veuillez vérifier votre compte ou patienter une minute.");
                }
                throw new Exception($"Erreur HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);

            // Le format de réponse est identique à OpenAI
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return text ?? string.Empty;
        }
    }
}

