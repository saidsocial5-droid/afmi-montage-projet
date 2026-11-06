using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class AiAssistantPanelViewModel : ObservableObject
    {
        private readonly AiRouterService _routerService = new();

        [ObservableProperty]
        private ObservableCollection<ChatMessage> _messages = new();

        [ObservableProperty]
        private string _inputMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _currentEngineName = "Local (Ollama)";

        [ObservableProperty]
        private string _currentEngineColor = "#16A34A"; // Vert pour local par défaut

        public AiAssistantPanelViewModel()
        {
            // Charger un message d'accueil initial
            Messages.Add(new ChatMessage
            {
                Sender = "IA",
                Content = "Bonjour ! Je suis votre assistant de montage de projets AFMI. Posez-moi des questions sur vos objectifs, indicateurs, budgets ou planifications.",
                EngineUsed = "Initialisation",
                BgColor = "#F3E8FF",
                Alignment = "Left"
            });
            UpdateEngineIndicator();
        }

        public void UpdateEngineIndicator()
        {
            try
            {
                using var db = new AppDbContext();
                var config = db.AppConfigs.FirstOrDefault();
                if (config != null)
                {
                    if (config.UseLocalByDefault || !config.CloudOptInGranted)
                    {
                        CurrentEngineName = "Local (Ollama / Embarqué)";
                        CurrentEngineColor = "#16A34A"; // Vert
                    }
                    else
                    {
                        CurrentEngineName = $"Cloud ({config.Provider})";
                        CurrentEngineColor = "#0284C7"; // Bleu
                    }
                }
            }
            catch
            {
                CurrentEngineName = "Local (Embarqué)";
                CurrentEngineColor = "#16A34A";
            }
        }

        [ObservableProperty]
        private string _selectedLanguage = "Français";

        public ObservableCollection<string> AvailableLanguages { get; } = new ObservableCollection<string>
        {
            "Français",
            "English",
            "Español",
            "العربية"
        };

        [RelayCommand]
        public async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(InputMessage) || IsBusy) return;

            string userPrompt = InputMessage.Trim();
            InputMessage = string.Empty;

            // Ajouter le message de l'utilisateur
            Messages.Add(new ChatMessage
            {
                Sender = "Vous",
                Content = userPrompt,
                EngineUsed = string.Empty,
                BgColor = "#E0F2FE", // Bleu clair pour l'utilisateur
                Alignment = "Right"
            });

            IsBusy = true;
            try
            {
                string systemInstruction = $"Tu es un expert en montage de projet AFMI. Tu dois OBLIGATOIREMENT répondre dans la langue suivante : {SelectedLanguage}. Si on te demande la définition d'un mot, explique-le dans le contexte du développement de projets agricoles/ruraux si possible.";
                // Appeler le routeur d'IA
                var result = await _routerService.GenerateResponseAsync(userPrompt, systemInstruction);

                // Ajouter la réponse de l'IA
                Messages.Add(new ChatMessage
                {
                    Sender = "Assistant IA",
                    Content = result.IsSuccess ? result.ResponseText : $"⚠️ Le moteur IA n'a pas pu répondre : {result.ErrorMessage}",
                    EngineUsed = result.EngineName,
                    BgColor = result.EngineName.Contains("Cloud") ? "#EFF6FF" : "#F8FAFC",
                    Alignment = "Left"
                });

                // Mettre à jour l'indicateur visuel en fonction du moteur qui a répondu
                CurrentEngineName = result.EngineName;
                CurrentEngineColor = result.EngineName.Contains("Cloud") ? "#0284C7" : "#16A34A";
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessage
                {
                    Sender = "Système",
                    Content = $"Erreur lors de la génération de réponse : {ex.Message}",
                    EngineUsed = "Erreur",
                    BgColor = "#FEE2E2", // Rouge clair pour l'erreur
                    Alignment = "Left"
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SendQuickAction(string actionText)
        {
            if (string.IsNullOrWhiteSpace(actionText)) return;
            InputMessage = actionText;
            SendMessageCommand.Execute(null);
        }

        [RelayCommand]
        public void CopyMessage(ChatMessage? message)
        {
            if (message == null) return;
            try
            {
                Clipboard.SetText(message.Content);
            }
            catch (Exception)
            {
                // Ignorer en cas d'erreur Clipboard (ex: processus verrouillant le presse-papier)
            }
        }

        [RelayCommand]
        public void ClearHistory()
        {
            Messages.Clear();
            Messages.Add(new ChatMessage
            {
                Sender = "IA",
                Content = "Historique effacé. Posez-moi une nouvelle question !",
                EngineUsed = "Système",
                BgColor = "#F3E8FF",
                Alignment = "Left"
            });
            UpdateEngineIndicator();
        }
    }

    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string EngineUsed { get; set; } = string.Empty;
        public string BgColor { get; set; } = "#F1F5F9";
        public string Alignment { get; set; } = "Left";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}

