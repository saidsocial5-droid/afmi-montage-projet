using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_projet_2.ViewModels.Radar
{
    public partial class OpportunityItemViewModel : ObservableObject
    {
        public RssItemCache Model { get; }

        public string ExternalId => Model.ExternalId;
        public string Title => Model.Title;
        public string CleanDescription => Model.CleanDescription;
        public string Donor => Model.Donor;
        public string FeedName => Model.FeedName;
        public string Link => Model.Link;
        public DateTime? PublishDate => Model.PublishDate;
        public DateTime FetchedAt => Model.FetchedAt;

        [ObservableProperty]
        private bool _isInPipeline;

        [ObservableProperty]
        private bool _isRead;

        [ObservableProperty]
        private bool _isHidden;

        [ObservableProperty]
        private bool _isScored;

        [ObservableProperty]
        private double? _aiScore;

        [ObservableProperty]
        private string? _aiScoreRationale;

        [ObservableProperty]
        private string? _aiMatchedAxes;

        [ObservableProperty]
        private string? _aiSummaryAr;

        [ObservableProperty]
        private string? _aiSummaryFr;

        [ObservableProperty]
        private decimal? _aiEstimatedBudget;

        [ObservableProperty]
        private string? _aiCurrency;

        [ObservableProperty]
        private DateTime? _aiDeadline;

        [ObservableProperty]
        private bool _isArDisplay;

        [ObservableProperty]
        private bool _isScoringInProgress;

        public OpportunityItemViewModel(RssItemCache model)
        {
            Model = model;
            _isInPipeline = model.IsInPipeline;
            _isRead = model.IsRead;
            _isHidden = model.IsHidden;
            _isScored = model.IsScored;
            _aiScore = model.AiScore;
            _aiScoreRationale = model.AiScoreRationale;
            _aiMatchedAxes = model.AiMatchedAxes;
            _aiSummaryAr = model.AiSummaryAr;
            _aiSummaryFr = model.AiSummaryFr;
            _aiEstimatedBudget = model.AiEstimatedBudget;
            _aiCurrency = model.AiCurrency;
            _aiDeadline = model.AiDeadline;
            _isArDisplay = false; // Par défaut Français
        }

        public string CurrentSummary
        {
            get
            {
                if (IsArDisplay && !string.IsNullOrWhiteSpace(AiSummaryAr))
                    return AiSummaryAr;

                if (!string.IsNullOrWhiteSpace(AiSummaryFr))
                    return AiSummaryFr;

                return CleanDescription;
            }
        }

        public string ScoreBadgeColor
        {
            get
            {
                if (!AiScore.HasValue) return "#94A3B8"; // رمادي (لم تُحلل بعد)
                if (AiScore.Value >= 8.0) return "#10B981"; // أخضر (≥8)
                if (AiScore.Value >= 5.0) return "#F59E0B"; // برتقالي (5-7)
                return "#94A3B8"; // رمادي (<5)
            }
        }

        public string ScoreBadgeText => AiScore.HasValue ? $"{AiScore.Value:0.0} / 10" : "Non évalué";

        public bool IsExpired => AiDeadline.HasValue && AiDeadline.Value.Date < DateTime.UtcNow.Date;

        public int? DaysRemaining => AiDeadline.HasValue ? (int)Math.Ceiling((AiDeadline.Value.Date - DateTime.UtcNow.Date).TotalDays) : null;

        public bool IsUrgentDeadline
        {
            get
            {
                if (!AiDeadline.HasValue) return false;
                var remaining = (AiDeadline.Value.Date - DateTime.UtcNow.Date).TotalDays;
                return remaining >= 0 && remaining <= 15;
            }
        }

        public string DeadlineStatusColor
        {
            get
            {
                if (!AiDeadline.HasValue) return "#64748B";
                if (IsExpired) return "#DC2626"; // Rouge
                if (IsUrgentDeadline) return "#D97706"; // Ambre
                return "#059669"; // Vert
            }
        }

        public string DeadlineStatusBadgeBackground
        {
            get
            {
                if (!AiDeadline.HasValue) return "#F1F5F9";
                if (IsExpired) return "#FEE2E2";
                if (IsUrgentDeadline) return "#FEF3C7";
                return "#ECFDF5";
            }
        }

        public string DeadlineStatusBadgeBorder
        {
            get
            {
                if (!AiDeadline.HasValue) return "#CBD5E1";
                if (IsExpired) return "#FCA5A5";
                if (IsUrgentDeadline) return "#FCD34D";
                return "#6EE7B7";
            }
        }

        public string DeadlineStatusText
        {
            get
            {
                if (!AiDeadline.HasValue) return "Non spécifié";
                if (IsExpired) return $"Expiré le {AiDeadline.Value:dd/MM/yyyy}";
                if (DaysRemaining <= 15) return $"Échéance dans {DaysRemaining} j ({AiDeadline.Value:dd/MM/yyyy})";
                return $"Ouvert jusqu'au {AiDeadline.Value:dd/MM/yyyy} ({DaysRemaining} j)";
            }
        }

        public string DeadlineStatusTextAr
        {
            get
            {
                if (!AiDeadline.HasValue) return "غير محدد";
                if (IsExpired) return $"منتهية الصلاحية ({AiDeadline.Value:dd/MM/yyyy})";
                if (DaysRemaining <= 15) return $"وشيك الإغلاق ({DaysRemaining} يوم - {AiDeadline.Value:dd/MM/yyyy})";
                return $"جارية حتى {AiDeadline.Value:dd/MM/yyyy} (متبقي {DaysRemaining} يوم)";
            }
        }

        public string FormattedBudget => AiEstimatedBudget.HasValue
            ? $"{AiEstimatedBudget.Value:N0} {AiCurrency ?? "EUR"}"
            : "Non spécifié";

        public string FormattedDeadline => AiDeadline.HasValue
            ? AiDeadline.Value.ToString("dd/MM/yyyy")
            : "Non spécifié";

        public void ApplyScoringResult(OpportunityScoringResult result)
        {
            AiScore = result.RelevanceScore;
            AiScoreRationale = result.Rationale;
            AiMatchedAxes = result.MatchedAxes != null ? string.Join(", ", result.MatchedAxes) : string.Empty;
            AiSummaryAr = result.SummaryAr;
            AiSummaryFr = result.SummaryFr;
            AiEstimatedBudget = result.EstimatedBudget;
            AiCurrency = result.Currency;
            AiDeadline = result.Deadline;
            IsScored = true;

            // Sync with underlying model
            Model.AiScore = AiScore;
            Model.AiScoreRationale = AiScoreRationale;
            Model.AiMatchedAxes = AiMatchedAxes;
            Model.AiSummaryAr = AiSummaryAr;
            Model.AiSummaryFr = AiSummaryFr;
            Model.AiEstimatedBudget = AiEstimatedBudget;
            Model.AiCurrency = AiCurrency;
            Model.AiDeadline = AiDeadline;
            Model.IsScored = true;

            OnPropertyChanged(nameof(CurrentSummary));
            OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(ScoreBadgeColor));
            OnPropertyChanged(nameof(ScoreColor));
            OnPropertyChanged(nameof(ScoreBadgeText));
            OnPropertyChanged(nameof(FormattedBudget));
            OnPropertyChanged(nameof(FormattedDeadline));
            OnPropertyChanged(nameof(IsUrgentDeadline));
            OnPropertyChanged(nameof(IsDeadlineNear));
            OnPropertyChanged(nameof(IsExpired));
            OnPropertyChanged(nameof(DaysRemaining));
            OnPropertyChanged(nameof(DeadlineStatusColor));
            OnPropertyChanged(nameof(DeadlineStatusText));
            OnPropertyChanged(nameof(DeadlineStatusTextAr));
            OnPropertyChanged(nameof(DeadlineStatusBadgeBackground));
            OnPropertyChanged(nameof(DeadlineStatusBadgeBorder));
        }

        public bool IsArabicView => IsArDisplay;
        public string DisplaySummary => CurrentSummary;
        public string ScoreColor => ScoreBadgeColor;
        public bool IsDeadlineNear => IsUrgentDeadline;
        public FlowDirection DisplaySummaryDirection => IsArDisplay ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        public string DisplaySummaryFontFamily => IsArDisplay ? "Cairo, Tahoma, Arial" : "Inter, Segoe UI, Arial";

        public void ToggleSummaryLanguage()
        {
            IsArDisplay = !IsArDisplay;
            OnPropertyChanged(nameof(IsArDisplay));
            OnPropertyChanged(nameof(IsArabicView));
            OnPropertyChanged(nameof(CurrentSummary));
            OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(DisplaySummaryDirection));
            OnPropertyChanged(nameof(DisplaySummaryFontFamily));
        }
    }
}
