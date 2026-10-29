using System;
using System.Threading.Tasks;

namespace AFMI_Montage_projet_2.Services
{
    public class CloudAiEngine : IAiEngine
    {
        public string EngineName => "Cloud (Gemini / DeepSeek / OpenAI)";

        public async Task<string> GenerateAsync(string prompt, string systemInstruction = "", bool enableSearch = false)
        {
            if (!AiAssistantService.IsConfigured())
            {
                throw new Exception("Le moteur Cloud n'est pas configuré. Veuillez activer l'option Cloud et renseigner votre clé API dans les paramètres.");
            }

            return await AiAssistantService.CallCloudAiAsync(prompt, systemInstruction, enableSearch);
        }
    }
}
