using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Security;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Collab;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFMI_Montage_projet_2.ViewModels
{
    /// <summary>Modification locale proposée à l'envoi, avec case à cocher.</summary>
    public partial class SelectableChange : ObservableObject
    {
        public DetectedChange Change { get; }

        [ObservableProperty]
        private bool _isSelected = true;

        public SelectableChange(DetectedChange change) => Change = change;

        public string Title => Change.IsCreation ? $"➕ {Change.EntityLabel}" : $"✏️ {Change.EntityLabel}";
        public string Detail => Change.IsCreation
            ? "Nouvel élément à ajouter à la version officielle"
            : $"Champ « {Change.FieldName} » : {Shorten(Change.BaseValue)}  →  {Shorten(Change.NewValue)}";

        private static string Shorten(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "(vide)";
            text = text.Replace('\n', ' ').Replace('\r', ' ');
            return text.Length > 90 ? text[..87] + "…" : text;
        }
    }

    /// <summary>Ligne « Mes propositions ».</summary>
    public class MyProposalRow
    {
        public Contribution Contribution { get; }
        public MyProposalRow(Contribution c) => Contribution = c;

        public string Title => string.IsNullOrEmpty(Contribution.EntityLabel) ? Contribution.EntityType : Contribution.EntityLabel!;
        public string Kind => Contribution.FieldName switch
        {
            ProposalFields.Create => "Ajout",
            ProposalFields.Note => "Suggestion",
            _ => $"Modification de « {Contribution.FieldName} »"
        };
        public string StatusLabel => ContributionStatuses.Label(Contribution.Status);
        public string StatusColor => Contribution.Status switch
        {
            ContributionStatuses.Approved => "#047857",
            ContributionStatuses.Accepted => "#0369A1",
            ContributionStatuses.Rejected => "#B91C1C",
            ContributionStatuses.NeedsReview => "#B45309",
            _ => "#475569"
        };
        public string Date => Contribution.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        public string? ReviewNotes => Contribution.ReviewNotes;
    }

    /// <summary>
    /// Espace d'équipe (ADR 23) : connexion Google de chaque membre, version officielle des projets,
    /// envoi des contributions, validation par les administrateurs et gestion de l'équipe.
    /// </summary>
    public partial class TeamWorkspaceViewModel : ObservableObject, IDisposable
    {
        private const string AccessCodePrefix = "AFMI1";

        private readonly AuthService _auth = AuthService.Instance;
        private readonly TeamWorkspaceService _team = TeamWorkspaceService.Instance;
        private readonly Action? _onProjectsChanged;

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = string.Empty;

        // Connexion
        [ObservableProperty] private bool _isSignedIn;
        [ObservableProperty] private bool _isWorkspaceReady;
        [ObservableProperty] private string _userEmail = string.Empty;
        [ObservableProperty] private string _userName = string.Empty;
        [ObservableProperty] private string _roleLabel = string.Empty;
        [ObservableProperty] private string _stationLabel = string.Empty;
        [ObservableProperty] private bool _isOfficialStation;
        [ObservableProperty] private bool _isMemberStation;
        [ObservableProperty] private bool _canManageTeam;
        [ObservableProperty] private bool _canDecide;
        [ObservableProperty] private bool _canContribute;
        [ObservableProperty] private string _workspaceError = string.Empty;

        // Configuration OAuth (code d'accès)
        [ObservableProperty] private bool _hasClientCredentials;
        [ObservableProperty] private string _accessCodeInput = string.Empty;

        // Version officielle
        [ObservableProperty] private string _officialVersionText = string.Empty;
        [ObservableProperty] private string _newVersionText = string.Empty;

        // Contributions
        [ObservableProperty] private string _changesSummary = string.Empty;
        [ObservableProperty] private Project? _selectedNoteProject;
        [ObservableProperty] private string _noteText = string.Empty;

        // Équipe
        [ObservableProperty] private string _newMemberEmail = string.Empty;
        [ObservableProperty] private string _newMemberRole = TeamRoles.Member;

        public ObservableCollection<SelectableChange> DetectedChanges { get; } = new();
        public ObservableCollection<MyProposalRow> MyProposals { get; } = new();
        public ObservableCollection<Project> Projects { get; } = new();
        public ObservableCollection<TeamMemberDto> Members { get; } = new();
        public string[] Roles { get; } = { TeamRoles.Member, TeamRoles.Reviewer, TeamRoles.Admin };

        public string OwnerEmail => SecurityConstants.WorkspaceOwnerEmail;
        public string AdminsText => string.Join(" et ", SecurityConstants.SuperAdminEmails);

        public TeamWorkspaceViewModel(Action? onProjectsChanged = null)
        {
            _onProjectsChanged = onProjectsChanged;
            _auth.UserStateChanged += OnUserStateChanged;
            SyncService.Instance.OfficialVersionAvailable += OnOfficialVersionAvailable;
            RefreshState();
            LoadProjects();
            _ = LoadDynamicDataAsync();
        }

        public void Dispose()
        {
            _auth.UserStateChanged -= OnUserStateChanged;
            SyncService.Instance.OfficialVersionAvailable -= OnOfficialVersionAvailable;
        }

        private void OnUserStateChanged(object? sender, UserProfile? profile) =>
            Application.Current?.Dispatcher.Invoke(() => { RefreshState(); _ = LoadDynamicDataAsync(); });

        private void OnOfficialVersionAvailable(object? sender, OfficialSnapshotInfo info) =>
            Application.Current?.Dispatcher.Invoke(() =>
                NewVersionText = $"🔔 Nouvelle version officielle du {info.PublishedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} disponible.");

        private void RefreshState()
        {
            HasClientCredentials = AuthService.TryGetClientCredentials(out _, out _);
            var user = _auth.CurrentUser;
            IsSignedIn = _auth.IsAuthenticated;
            IsWorkspaceReady = _auth.IsWorkspaceReady;
            UserEmail = user?.Email ?? string.Empty;
            UserName = user?.DisplayName ?? string.Empty;
            RoleLabel = IsSignedIn ? TeamRoles.Label(user?.Role) : string.Empty;
            IsOfficialStation = _auth.IsOfficialStation;
            IsMemberStation = IsWorkspaceReady && !IsOfficialStation;
            StationLabel = !IsWorkspaceReady ? string.Empty
                : IsOfficialStation ? "Poste officiel : la base de ce poste fait foi et est publiée pour l'équipe."
                : "Poste membre : vos projets sont une copie de la version officielle.";
            CanManageTeam = _auth.CanManageTeam();
            CanDecide = IsWorkspaceReady && _auth.CanDecide();
            CanContribute = IsMemberStation && (_auth.CurrentTeamConfig?.CanPropose(UserEmail) ?? false);
            WorkspaceError = _auth.LastError ?? string.Empty;

            OfficialVersionText = IsOfficialStation
                ? (_team.LastPublishedAtUtc is DateTime p ? $"Dernière publication : {p.ToLocalTime():dd/MM/yyyy HH:mm}" : "Aucune publication pour l'instant.")
                : (_team.MirrorPublishedAtUtc is DateTime mirror ? $"Version officielle reçue : {mirror.ToLocalTime():dd/MM/yyyy HH:mm}" : "Aucune version officielle reçue sur ce poste.");

            Members.Clear();
            foreach (var m in _auth.CurrentTeamConfig?.Members ?? Enumerable.Empty<TeamMemberDto>()) Members.Add(m);
        }

        private void LoadProjects()
        {
            try
            {
                using var db = new AppDbContext();
                Projects.Clear();
                foreach (var p in db.Projects.OrderBy(p => p.Title).ToList()) Projects.Add(p);
                SelectedNoteProject ??= Projects.FirstOrDefault();
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TeamWorkspaceViewModel), "Chargement des projets impossible.", ex);
            }
        }

        private async Task LoadDynamicDataAsync()
        {
            await LoadMyProposalsAsync();
            if (IsMemberStation)
            {
                var info = SyncService.Instance.NewOfficialVersion;
                NewVersionText = info != null ? $"🔔 Nouvelle version officielle du {info.PublishedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} disponible." : string.Empty;
            }
        }

        #region Connexion

        [RelayCommand]
        private async Task SignInAsync()
        {
            if (!AuthService.TryGetClientCredentials(out var id, out var secret))
            {
                StatusMessage = "Collez d'abord le code d'accès de l'équipe (fourni par un administrateur).";
                return;
            }

            await RunBusyAsync("Connexion à Google… (une fenêtre du navigateur peut s'ouvrir)", async () =>
            {
                bool ok = await _auth.SignInAsync(id, secret);
                RefreshState();
                StatusMessage = ok
                    ? $"Connecté : {UserName} ({UserEmail})."
                    : (_auth.LastError ?? "Connexion impossible.");
                if (ok && IsMemberStation && !_team.HasBaseSnapshot)
                    StatusMessage += " Première connexion sur ce poste : cliquez sur « Mettre à jour mes projets » pour recevoir la version officielle.";
            });
        }

        [RelayCommand]
        private async Task SignOutAsync()
        {
            await RunBusyAsync("Déconnexion…", async () =>
            {
                await _auth.LogoutAsync();
                RefreshState();
                StatusMessage = "Déconnecté.";
            });
        }

        [RelayCommand]
        private void ApplyAccessCode()
        {
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(AccessCodeInput.Trim()));
                var parts = raw.Split('|');
                string clientId, clientSecret;
                if (parts.Length == 3 && parts[0] == AccessCodePrefix) { clientId = parts[1]; clientSecret = parts[2]; }
                else if (parts.Length == 3) { clientId = parts[0]; clientSecret = parts[1]; } // ancien code « Rejoindre un espace »
                else throw new FormatException();

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)) throw new FormatException();

                SettingsService.SaveSetting("Google_ClientId", clientId);
                SettingsService.SaveSetting("Google_ClientSecret", AFMI_Montage_projet_2.Helpers.EncryptionHelper.Protect(clientSecret));
                AccessCodeInput = string.Empty;
                RefreshState();
                StatusMessage = "Code d'accès enregistré. Cliquez maintenant sur « Se connecter avec Google » et choisissez VOTRE compte Google.";
            }
            catch (FormatException)
            {
                StatusMessage = "Code d'accès invalide. Copiez-le en entier, sans espace.";
            }
        }

        [RelayCommand]
        private void CopyAccessCode()
        {
            if (!AuthService.TryGetClientCredentials(out var id, out var secret))
            {
                StatusMessage = "Aucun identifiant Google configuré sur ce poste.";
                return;
            }
            var code = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{AccessCodePrefix}|{id}|{secret}"));
            Clipboard.SetText(code);
            StatusMessage = "Code d'accès copié. Transmettez-le uniquement aux membres de l'équipe (messagerie privée).";
        }

        #endregion

        #region Version officielle

        [RelayCommand]
        private async Task PublishAsync()
        {
            await RunBusyAsync("Publication de la version officielle…", async () =>
            {
                var info = await _team.PublishAsync();
                RefreshState();
                StatusMessage = info != null
                    ? $"Version officielle publiée ({info.ProjectCount} projet(s)). Les membres la recevront à leur prochaine mise à jour."
                    : "Aucun changement depuis la dernière publication.";
            });
        }

        [RelayCommand]
        private async Task RefreshFromOfficialAsync()
        {
            if (!_team.HasBaseSnapshot)
            {
                var first = MessageBox.Show(
                    "Première mise à jour sur ce poste : les projets enregistrés ici seront remplacés par la version officielle de l'équipe.\n\n" +
                    "Une sauvegarde complète de votre base est faite automatiquement avant (dossier Backups). Vos comptes et réglages sont conservés.\n\nContinuer ?",
                    "Mettre à jour mes projets", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (first != MessageBoxResult.Yes) return;
            }

            await RunBusyAsync("Téléchargement de la version officielle…", async () =>
            {
                var result = await _team.RefreshFromOfficialAsync(discardLocalChanges: false);
                if (result.Outcome == RefreshOutcome.PendingLocalChanges)
                {
                    var choice = MessageBox.Show(
                        result.Message + "\n\nOui : les envoyer maintenant comme propositions, puis mettre à jour.\nNon : les abandonner et mettre à jour.\nAnnuler : ne rien faire.",
                        "Modifications non envoyées", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (choice == MessageBoxResult.Cancel) { StatusMessage = "Mise à jour annulée."; return; }
                    if (choice == MessageBoxResult.Yes)
                    {
                        var pending = await _team.GetUnsentLocalChangesAsync();
                        await ContributionService.Instance.SubmitDetectedChangesAsync(pending);
                    }
                    result = await _team.RefreshFromOfficialAsync(discardLocalChanges: true);
                }

                StatusMessage = result.Message;
                if (result.Outcome == RefreshOutcome.Updated)
                {
                    NewVersionText = string.Empty;
                    RefreshState();
                    LoadProjects();
                    DetectedChanges.Clear();
                    ChangesSummary = string.Empty;
                    _onProjectsChanged?.Invoke();
                }
            });
        }

        #endregion

        #region Contributions

        [RelayCommand]
        private async Task DetectChangesAsync()
        {
            await RunBusyAsync("Recherche de vos modifications…", async () =>
            {
                DetectedChanges.Clear();
                if (!_team.HasBaseSnapshot)
                {
                    ChangesSummary = "Recevez d'abord la version officielle (« Mettre à jour mes projets »), puis travaillez dans vos projets.";
                    return;
                }
                var changes = await _team.GetUnsentLocalChangesAsync();
                foreach (var c in changes) DetectedChanges.Add(new SelectableChange(c));
                ChangesSummary = changes.Count == 0
                    ? "Aucune modification non envoyée."
                    : $"{changes.Count} modification(s) détectée(s). Décochez celles à ne pas envoyer.";
                StatusMessage = string.Empty;
            });
        }

        [RelayCommand]
        private async Task SubmitChangesAsync()
        {
            var selected = DetectedChanges.Where(c => c.IsSelected).Select(c => c.Change).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "Aucune modification sélectionnée.";
                return;
            }

            await RunBusyAsync("Envoi des propositions…", async () =>
            {
                int sent = await ContributionService.Instance.SubmitDetectedChangesAsync(selected);
                foreach (var row in DetectedChanges.Where(c => c.IsSelected).ToList()) DetectedChanges.Remove(row);
                ChangesSummary = DetectedChanges.Count == 0 ? "Toutes les modifications sélectionnées ont été envoyées." : $"{DetectedChanges.Count} modification(s) non envoyée(s).";
                StatusMessage = $"{sent} proposition(s) envoyée(s) aux administrateurs.";
                await LoadMyProposalsAsync();
            });
        }

        [RelayCommand]
        private async Task SendNoteAsync()
        {
            if (SelectedNoteProject == null || string.IsNullOrWhiteSpace(NoteText))
            {
                StatusMessage = "Choisissez un projet et rédigez votre suggestion.";
                return;
            }

            await RunBusyAsync("Envoi de la suggestion…", async () =>
            {
                await ContributionService.Instance.CreateNoteAsync(SelectedNoteProject.GlobalId, SelectedNoteProject.Title ?? "", NoteText);
                NoteText = string.Empty;
                StatusMessage = "Suggestion envoyée aux administrateurs.";
                await LoadMyProposalsAsync();
            });
        }

        [RelayCommand]
        private async Task LoadMyProposalsAsync()
        {
            MyProposals.Clear();
            if (string.IsNullOrEmpty(UserEmail)) return;
            try
            {
                foreach (var c in await ContributionService.Instance.GetProposalsByAuthorAsync(UserEmail))
                    MyProposals.Add(new MyProposalRow(c));
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TeamWorkspaceViewModel), "Chargement des propositions impossible.", ex);
            }
        }

        #endregion

        #region Équipe

        [RelayCommand]
        private async Task InviteMemberAsync()
        {
            var email = NewMemberEmail.Trim();
            if (!email.Contains('@') || email.Contains(' '))
            {
                StatusMessage = "Saisissez une adresse Google valide (ex. prenom.nom@gmail.com).";
                return;
            }

            await RunBusyAsync($"Invitation de {email}…", async () =>
            {
                bool ok = await _auth.InviteMemberAsync(email, NewMemberRole);
                RefreshState();
                StatusMessage = ok
                    ? $"{email} est invité(e) : Google lui envoie un e-mail. Transmettez-lui aussi le code d'accès de l'équipe."
                    : "Invitation impossible (vérifiez l'adresse et la connexion ; détails dans le journal).";
                if (ok) NewMemberEmail = string.Empty;
            });
        }

        [RelayCommand]
        private async Task RemoveMemberAsync(TeamMemberDto? member)
        {
            if (member == null) return;
            if (MessageBox.Show($"Retirer {member.Email} de l'équipe ? Son accès au dossier Drive de l'association sera révoqué.",
                    "Retirer un membre", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            await RunBusyAsync($"Retrait de {member.Email}…", async () =>
            {
                bool ok = await _auth.RemoveMemberAsync(member.Email);
                RefreshState();
                StatusMessage = ok ? $"{member.Email} a été retiré(e) de l'équipe." : "Retrait impossible (détails dans le journal).";
            });
        }

        #endregion

        private async Task RunBusyAsync(string message, Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = message;
            try
            {
                await action();
            }
            catch (WorkspaceAccessException ex)
            {
                StatusMessage = ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TeamWorkspaceViewModel), message, ex);
                StatusMessage = "Erreur : " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
