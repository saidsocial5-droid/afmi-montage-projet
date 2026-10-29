using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class CanvasQuestionItem : ObservableObject
    {
        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        private string _type = "TextLong"; // TextLong, TextShort, Dropdown, Checkbox

        [ObservableProperty]
        private string _placeholder = "Saisissez la réponse ici...";

        [ObservableProperty]
        private string _optionsCommaSeparated = string.Empty;
    }

    public partial class CanvasSectionItem : ObservableObject
    {
        [ObservableProperty]
        private string _sectionTitle = "1. Section Principale";

        [ObservableProperty]
        private ObservableCollection<CanvasQuestionItem> _questions = new();

        [RelayCommand]
        public void AddQuestion()
        {
            Questions.Add(new CanvasQuestionItem
            {
                Label = "Nouvelle question du bailleur",
                Type = "TextLong"
            });
        }

        [RelayCommand]
        public void RemoveQuestion(CanvasQuestionItem question)
        {
            Questions.Remove(question);
        }
    }

    public partial class CanvasDesignerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _canvasName = "Nouveau Canevas Bailleur";

        [ObservableProperty]
        private string _canvasDescription = "Modèle personnalisé créé manuellement.";

        [ObservableProperty]
        private ObservableCollection<CanvasSectionItem> _sections = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public FormTemplate? CreatedTemplate { get; private set; }

        public event Action? RequestClose;

        public CanvasDesignerViewModel()
        {
            // Initialiser avec une première section et question par défaut
            var defaultSection = new CanvasSectionItem
            {
                SectionTitle = "1. Cadrage et Justification"
            };
            defaultSection.Questions.Add(new CanvasQuestionItem
            {
                Label = "Quel est le contexte et la justification du besoin ?",
                Type = "TextLong"
            });
            defaultSection.Questions.Add(new CanvasQuestionItem
            {
                Label = "Quels sont les objectifs et résultats attendus ?",
                Type = "TextLong"
            });

            Sections.Add(defaultSection);
        }

        [RelayCommand]
        public void AddSection()
        {
            var newSection = new CanvasSectionItem
            {
                SectionTitle = $"{Sections.Count + 1}. Nouvelle Section"
            };
            newSection.Questions.Add(new CanvasQuestionItem
            {
                Label = "Nouvelle question ou exigence",
                Type = "TextLong"
            });
            Sections.Add(newSection);
        }

        [RelayCommand]
        public void RemoveSection(CanvasSectionItem section)
        {
            if (Sections.Count > 1)
            {
                Sections.Remove(section);
            }
            else
            {
                StatusMessage = "Le canevas doit comporter au moins une section.";
            }
        }

        [RelayCommand]
        public void RemoveQuestion(CanvasQuestionItem question)
        {
            foreach (var s in Sections)
            {
                if (s.Questions.Contains(question))
                {
                    s.Questions.Remove(question);
                    break;
                }
            }
        }

        [RelayCommand]
        public async Task SaveCanvas()
        {
            if (string.IsNullOrWhiteSpace(CanvasName))
            {
                StatusMessage = "Veuillez saisir un nom pour ce canevas.";
                return;
            }

            try
            {
                var schemaList = Sections.Select((s, sIndex) => new CanvasSectionModel
                {
                    Section = s.SectionTitle,
                    Fields = s.Questions.Select((q, qIndex) => new CanvasFieldModel
                    {
                        Name = $"Field_S{sIndex + 1}_Q{qIndex + 1}",
                        Label = q.Label,
                        Type = q.Type,
                        Placeholder = q.Placeholder,
                        Options = q.Type == "Dropdown" && !string.IsNullOrWhiteSpace(q.OptionsCommaSeparated)
                            ? q.OptionsCommaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).ToList()
                            : new()
                    }).ToList()
                }).ToList();

                string jsonSchema = JsonSerializer.Serialize(schemaList, new JsonSerializerOptions { WriteIndented = true });

                using var db = new AppDbContext();
                var template = new FormTemplate
                {
                    Name = CanvasName.Trim(),
                    Description = CanvasDescription.Trim(),
                    JsonSchema = jsonSchema,
                    Version = 1,
                    IsActive = true
                };

                db.FormTemplates.Add(template);
                await db.SaveChangesAsync();

                CreatedTemplate = template;
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'enregistrement : {ex.Message}";
            }
        }
    }
}
