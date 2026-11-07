using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AFMI_Montage_projet_2.Services
{
    /// <summary>
    /// Le moteur local (Ollama) n'a pas pu produire de réponse. Levée au lieu de renvoyer un texte d'erreur,
    /// pour que le routeur marque l'appel en échec et que les services activent leur repli sans IA (ADR 27).
    /// </summary>
    public class LocalAiUnavailableException : Exception
    {
        public LocalAiUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
    }

    public class LocalAiEngine : IAiEngine
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(180) };

        public string EngineName => "Local (Ollama / Modèle embarqué)";

        public async Task<string> GenerateAsync(string prompt, string systemInstruction = "", bool enableSearch = false)
        {
            var prefs = LocalSettingsService.LoadSettings();
            string baseUrl = !string.IsNullOrWhiteSpace(prefs.OllamaUrl) ? prefs.OllamaUrl.TrimEnd('/') : "http://localhost:11434";
            string userModel = prefs.OllamaModel;

            try
            {
                // 1. D'abord vérifier quels modèles sont disponibles
                var tagsResponse = await HttpClient.GetAsync(baseUrl + "/api/tags");
                if (!tagsResponse.IsSuccessStatusCode)
                    throw new LocalAiUnavailableException($"Impossible de contacter Ollama sur {baseUrl} (HTTP {(int)tagsResponse.StatusCode}). Vérifiez qu'il est lancé en arrière-plan.");

                string selectedModel;
                if (!string.IsNullOrWhiteSpace(userModel))
                {
                    // L'utilisateur a forcé un modèle
                    selectedModel = userModel;
                }
                else
                {
                    string tagsJson = await tagsResponse.Content.ReadAsStringAsync();
                    using var docTags = JsonDocument.Parse(tagsJson);
                    if (!docTags.RootElement.TryGetProperty("models", out var modelsArray) || modelsArray.GetArrayLength() == 0)
                        throw new LocalAiUnavailableException("Aucun modèle n'est installé dans Ollama. Téléchargez un modèle (ex. : ollama run llama3).");

                    var names = modelsArray.EnumerateArray()
                        .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();
                    // llama3 s'il est installé, sinon le premier modèle disponible
                    selectedModel = names.FirstOrDefault(n => n!.Contains("llama3")) ?? names.FirstOrDefault() ?? "llama3";
                }

                // 2. Générer avec le modèle trouvé
                var requestBody = new
                {
                    model = selectedModel,
                    prompt = prompt,
                    system = systemInstruction,
                    stream = false
                };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync(baseUrl + "/api/generate", content);
                string responseString = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new LocalAiUnavailableException($"Ollama a renvoyé une erreur ({(int)response.StatusCode}) : {responseString}");

                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("response", out var respProp))
                    return respProp.GetString() ?? string.Empty;

                throw new LocalAiUnavailableException("Réponse d'Ollama inattendue (champ « response » absent).");
            }
            catch (LocalAiUnavailableException)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new LocalAiUnavailableException("La réponse du modèle Ollama a pris trop de temps (plus de 3 minutes). Essayez un modèle plus léger ou activez le moteur Cloud dans les paramètres.", ex);
            }
            catch (Exception ex)
            {
                throw new LocalAiUnavailableException($"Le moteur d'IA local Ollama n'est pas actif ou a rencontré une erreur ({ex.Message}). Installez et lancez Ollama pour une génération hors ligne.", ex);
            }
        }
    }
}
