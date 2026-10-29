using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AFMI_Montage_projet_2.Services
{
    public class LocalAiEngine : IAiEngine
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(180) };

        public string EngineName => "Local (Ollama / Modèle embarqué)";

        public async Task<string> GenerateAsync(string prompt, string systemInstruction = "", bool enableSearch = false)
        {
            try
            {
                var prefs = LocalSettingsService.LoadSettings();
                string baseUrl = !string.IsNullOrWhiteSpace(prefs.OllamaUrl) ? prefs.OllamaUrl.TrimEnd('/') : "http://localhost:11434";
                string userModel = prefs.OllamaModel;

                // 1. D'abord vérifier quels modèles sont disponibles
                string tagsUrl = baseUrl + "/api/tags";
                var tagsResponse = await HttpClient.GetAsync(tagsUrl);
                
                string selectedModel = "llama3"; // Par défaut
                
                if (tagsResponse.IsSuccessStatusCode)
                {
                    if (!string.IsNullOrWhiteSpace(userModel))
                    {
                        // L'utilisateur a forcé un modèle
                        selectedModel = userModel;
                    }
                    else 
                    {
                        string tagsJson = await tagsResponse.Content.ReadAsStringAsync();
                        using var docTags = JsonDocument.Parse(tagsJson);
                        if (docTags.RootElement.TryGetProperty("models", out var modelsArray) && modelsArray.GetArrayLength() > 0)
                        {
                            var firstModel = modelsArray[0].GetProperty("name").GetString();
                            
                            // Chercher s'il y a un llama3
                            bool hasLlama3 = false;
                            foreach(var m in modelsArray.EnumerateArray())
                            {
                                var name = m.GetProperty("name").GetString();
                                if (name != null && name.Contains("llama3")) {
                                    hasLlama3 = true;
                                    selectedModel = name;
                                    break;
                                }
                            }
                            
                            // Sinon utiliser le premier modèle disponible
                            if (!hasLlama3 && !string.IsNullOrEmpty(firstModel))
                            {
                                selectedModel = firstModel;
                            }
                        }
                        else 
                        {
                            return "**[Erreur Local]** Aucun modèle n'est installé dans Ollama. Veuillez télécharger un modèle (ex: ollama run llama3).";
                        }
                    }
                }
                else
                {
                    return $"**[Erreur Local]** Impossible de contacter Ollama sur {baseUrl}. Veuillez vérifier qu'il est lancé en arrière-plan.";
                }

                // 2. Générer avec le modèle trouvé
                string url = baseUrl + "/api/generate";
                var requestBody = new
                {
                    model = selectedModel,
                    prompt = prompt,
                    system = systemInstruction,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.TryGetProperty("response", out var respProp))
                    {
                        return respProp.GetString() ?? string.Empty;
                    }
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    return $"**[Erreur Génération Local]** Ollama a renvoyé une erreur : {error}";
                }
            }
            catch (TaskCanceledException)
            {
                return "**[Timeout Local]** La réponse du modèle Ollama a pris trop de temps (plus de 3 minutes). Essayez d'utiliser un modèle plus léger ou activez le moteur Cloud dans les paramètres.";
            }
            catch (Exception ex)
            {
                return $"**[Générateur Local Embarqué]**\n\nLe moteur d'IA local Ollama n'est pas actif ou a rencontré une erreur ({ex.Message}). Pour une génération hors ligne, veuillez installer Ollama. Cette fonctionnalité nécessite une connexion à internet si vous souhaitez utiliser le Cloud.";
            }

            return "**[Générateur Local Embarqué]**\n\nLe moteur d'IA local Ollama n'est pas actif sur votre système.";
        }
    }
}


