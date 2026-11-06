using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Collab;
using Microsoft.Win32;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class CollaborationViewModel : ObservableObject, IDisposable
    {
        private readonly int _projectId;
        private readonly AiRouterService _aiRouterService = new();
        private readonly VoiceNoteAssistantService _voiceNoteService = new();
        private DispatcherTimer? _recordingTimer;
        private DateTime _recordingStartTime;

        [ObservableProperty]
        private ObservableCollection<MeetingRoom> _meetingRooms = new();

        [ObservableProperty]
        private MeetingRoom? _selectedRoom;

        [ObservableProperty]
        private string _currentRoomUrl = "https://meet.jit.si/"; // Page d'accueil Jitsi par défaut

        [ObservableProperty]
        private string _meetingNotes = string.Empty;

        [ObservableProperty]
        private string _meetingSummary = string.Empty;

        [ObservableProperty]
        private bool _isGeneratingSummary;

        // Propriétés de l'assistance vocale IA
        [ObservableProperty]
        private bool _isVoiceRecording;

        [ObservableProperty]
        private string _recordingDurationText = "00:00";

        [ObservableProperty]
        private bool _isProcessingVoiceNotes;

        [ObservableProperty]
        private string _voiceStatusMessage = string.Empty;

        public CollaborationViewModel(int projectId)
        {
            _projectId = projectId;
            LoadRooms();
        }

        private void LoadRooms()
        {
            using var db = new AppDbContext();
            var rooms = db.MeetingRooms
                .Where(r => r.ProjectId == _projectId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
            
            MeetingRooms = new ObservableCollection<MeetingRoom>(rooms);
        }

        [RelayCommand]
        private void GenerateJitsiRoom()
        {
            var roomName = $"AFMI-Projet-{_projectId}-{Guid.NewGuid().ToString().Substring(0, 8)}";
            var url = $"https://meet.jit.si/{roomName}";

            var newRoom = new MeetingRoom
            {
                ProjectId = _projectId,
                RoomName = $"Réunion Jitsi - {DateTime.Now:g}",
                RoomUrl = url,
                Platform = "Jitsi",
                CreatedAt = DateTime.Now
            };

            using var db = new AppDbContext();
            db.MeetingRooms.Add(newRoom);
            db.SaveChanges();

            MeetingRooms.Insert(0, newRoom);
            SelectedRoom = newRoom;
        }

        [RelayCommand]
        private void GenerateGoogleMeetPlaceholder()
        {
            var newRoom = new MeetingRoom
            {
                ProjectId = _projectId,
                RoomName = $"Réunion Google Meet - {DateTime.Now:g}",
                RoomUrl = "https://meet.google.com/new",
                Platform = "Google Meet",
                CreatedAt = DateTime.Now
            };

            using var db = new AppDbContext();
            db.MeetingRooms.Add(newRoom);
            db.SaveChanges();

            MeetingRooms.Insert(0, newRoom);
            SelectedRoom = newRoom;
        }

        partial void OnSelectedRoomChanged(MeetingRoom? value)
        {
            if (value != null && !string.IsNullOrWhiteSpace(value.RoomUrl))
            {
                CurrentRoomUrl = value.RoomUrl;
            }
        }
        
        [RelayCommand]
        private void DeleteRoom(MeetingRoom room)
        {
            if (room == null) return;
            
            if (MessageBox.Show("Voulez-vous vraiment supprimer ce lien de réunion ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using var db = new AppDbContext();
                db.MeetingRooms.Remove(room);
                db.SaveChanges();
                
                MeetingRooms.Remove(room);
                if (SelectedRoom == room)
                {
                    SelectedRoom = null;
                    CurrentRoomUrl = "https://meet.jit.si/";
                }
            }
        }

        #region Assistance Vocale IA (Prise de Notes Automatique)

        [RelayCommand]
        private void StartVoiceRecording()
        {
            if (IsVoiceRecording) return;

            bool started = _voiceNoteService.StartRecording(out string error);
            if (!started)
            {
                MessageBox.Show(error, "Microphone", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsVoiceRecording = true;
            _recordingStartTime = DateTime.Now;
            RecordingDurationText = "00:00";
            VoiceStatusMessage = "Écoute de la discussion en cours...";

            _recordingTimer?.Stop();
            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _recordingTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _recordingStartTime;
                RecordingDurationText = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            };
            _recordingTimer.Start();
        }

        [RelayCommand]
        private async Task StopVoiceRecordingAndGenerateNotesAsync()
        {
            if (!IsVoiceRecording) return;

            _recordingTimer?.Stop();
            IsVoiceRecording = false;

            IsProcessingVoiceNotes = true;
            VoiceStatusMessage = "L'IA analyse la discussion vocale et rédige les notes...";

            try
            {
                var (success, notes, error) = await _voiceNoteService.StopRecordingAndProcessAsync();
                if (success && !string.IsNullOrWhiteSpace(notes))
                {
                    AppendOrSetMeetingNotes(notes, "Enregistrement direct au micro");
                    VoiceStatusMessage = "Prise de notes vocale terminée avec succès !";
                }
                else
                {
                    MessageBox.Show(string.IsNullOrWhiteSpace(error) ? "Aucune note n'a pu être générée." : error,
                        "Prise de notes vocale", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Une erreur est survenue lors de l'analyse vocale : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessingVoiceNotes = false;
            }
        }

        [RelayCommand]
        private async Task ImportAudioFileAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Sélectionner un enregistrement audio de discussion (WAV, MP3, M4A, OGG...)",
                Filter = "Fichiers Audio (*.wav;*.mp3;*.m4a;*.ogg;*.webm;*.aac;*.wma)|*.wav;*.mp3;*.m4a;*.ogg;*.webm;*.aac;*.wma|Tous les fichiers (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                IsProcessingVoiceNotes = true;
                VoiceStatusMessage = $"Analyse du fichier audio '{Path.GetFileName(dlg.FileName)}' par l'IA...";

                try
                {
                    var (success, notes, error) = await _voiceNoteService.ProcessAudioFileAsync(dlg.FileName);
                    if (success && !string.IsNullOrWhiteSpace(notes))
                    {
                        AppendOrSetMeetingNotes(notes, $"Audio importé : {Path.GetFileName(dlg.FileName)}");
                        VoiceStatusMessage = "Notes extraites avec succès du fichier audio !";
                    }
                    else
                    {
                        MessageBox.Show(string.IsNullOrWhiteSpace(error) ? "Impossible d'extraire les notes." : error,
                            "Analyse Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de l'analyse de l'audio : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsProcessingVoiceNotes = false;
                }
            }
        }

        [RelayCommand]
        private async Task StructureNotesWithAiAsync()
        {
            if (string.IsNullOrWhiteSpace(MeetingNotes))
            {
                MessageBox.Show("Le bloc de notes est vide. Écrivez ou dictez d'abord des notes pour que l'IA puisse les structurer.",
                    "Notes vides", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsProcessingVoiceNotes = true;
            VoiceStatusMessage = "Structuration et mise en forme des notes par l'IA...";

            try
            {
                var (success, notes, error) = await _voiceNoteService.StructureTranscriptWithAiAsync(MeetingNotes);
                if (success && !string.IsNullOrWhiteSpace(notes))
                {
                    MeetingNotes = notes;
                    VoiceStatusMessage = "Notes structurées avec succès !";
                }
                else
                {
                    MessageBox.Show(error, "Structuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessingVoiceNotes = false;
            }
        }

        [RelayCommand]
        private void ClearMeetingNotes()
        {
            if (string.IsNullOrWhiteSpace(MeetingNotes)) return;

            if (MessageBox.Show("Voulez-vous vraiment effacer le brouillon de notes actuel ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                MeetingNotes = string.Empty;
                VoiceStatusMessage = string.Empty;
            }
        }

        private void AppendOrSetMeetingNotes(string newNotes, string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(MeetingNotes))
            {
                MeetingNotes = newNotes;
            }
            else
            {
                MeetingNotes += $"\n\n---\n⏱️ **Prise de notes vocale ({DateTime.Now:dd/MM/yyyy HH:mm} - {sourceLabel})**\n\n{newNotes}";
            }
        }

        #endregion

        [RelayCommand]
        private async Task GenerateMeetingSummary()
        {
            if (string.IsNullOrWhiteSpace(MeetingNotes))
            {
                MessageBox.Show("Veuillez d'abord prendre quelques notes brutes pendant la réunion pour que l'IA puisse générer un résumé.", "Notes manquantes", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsGeneratingSummary = true;
            MeetingSummary = "L'Agent IA rédige le compte-rendu... Veuillez patienter.";

            try
            {
                string systemPrompt = "Tu es un assistant expert en gestion de projets. Ta tâche est de transformer les notes brutes prises lors d'une réunion en un compte-rendu professionnel et structuré. Utilise le format Markdown.";
                string prompt = $@"Voici les notes brutes de la réunion :
---
{MeetingNotes}
---

Génère un compte-rendu clair avec :
1. Les principaux sujets abordés.
2. Les décisions prises.
3. Les actions à réaliser (Qui fait quoi).";

                var result = await _aiRouterService.GenerateResponseAsync(prompt, systemPrompt, "MeetingSummary");
                
                if (result.IsSuccess)
                {
                    MeetingSummary = result.ResponseText;
                }
                else
                {
                    MeetingSummary = $"Erreur lors de la génération du résumé : {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                MeetingSummary = $"Une erreur est survenue : {ex.Message}";
            }
            finally
            {
                IsGeneratingSummary = false;
            }
        }

        public void Dispose()
        {
            _recordingTimer?.Stop();
            _voiceNoteService.Dispose();
        }
    }
}
