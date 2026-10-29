using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AFMI_Montage_projet_2.Models.Radar
{
    public enum SourceType
    {
        Document,
        WebLink,
        SocialMedia,
        PastedText
    }

    public enum SourceAnalysisStatus
    {
        Pending,
        Analyzing,
        Success,
        Error
    }

    /// <summary>
    /// Représente une source documentaire, un lien web ou un réseau social pouvant être analysé individuellement ou globalement.
    /// </summary>
    public partial class AnalysisSourceItem : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString("N");

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _pathOrUrl = string.Empty;

        [ObservableProperty]
        private SourceType _type = SourceType.Document;

        [ObservableProperty]
        private SourceAnalysisStatus _status = SourceAnalysisStatus.Pending;

        [ObservableProperty]
        private string _statusMessage = "Prêt pour analyse";

        [ObservableProperty]
        private string _extractedText = string.Empty;

        [ObservableProperty]
        private string _fileSizeText = string.Empty;

        [ObservableProperty]
        private int _proposalsCount;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private DateTime? _analyzedAt;

        public string Icon => Type switch
        {
            SourceType.Document => GetDocumentIcon(PathOrUrl),
            SourceType.SocialMedia => GetSocialIcon(PathOrUrl),
            SourceType.WebLink => GetSocialIcon(PathOrUrl),
            SourceType.PastedText => "📝",
            _ => "📄"
        };

        public string StatusColor => Status switch
        {
            SourceAnalysisStatus.Pending => "#64748B",
            SourceAnalysisStatus.Analyzing => "#2563EB",
            SourceAnalysisStatus.Success => "#059669",
            SourceAnalysisStatus.Error => "#DC2626",
            _ => "#64748B"
        };

        public string StatusBadgeBackground => Status switch
        {
            SourceAnalysisStatus.Pending => "#F1F5F9",
            SourceAnalysisStatus.Analyzing => "#EFF6FF",
            SourceAnalysisStatus.Success => "#ECFDF5",
            SourceAnalysisStatus.Error => "#FEF2F2",
            _ => "#F1F5F9"
        };

        public string StatusBadgeText => Status switch
        {
            SourceAnalysisStatus.Pending => "En attente",
            SourceAnalysisStatus.Analyzing => "Analyse en cours...",
            SourceAnalysisStatus.Success => $"{ProposalsCount} suggestion(s)",
            SourceAnalysisStatus.Error => "Erreur",
            _ => "Prêt"
        };

        private static string GetDocumentIcon(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "📑",
                ".docx" or ".doc" => "📘",
                ".xlsx" or ".xls" => "📊",
                ".txt" or ".md" => "📝",
                _ => "📄"
            };
        }

        private static string GetSocialIcon(string url)
        {
            string lower = url.ToLowerInvariant();
            if (lower.Contains("facebook.com")) return "📘";
            if (lower.Contains("linkedin.com")) return "💼";
            if (lower.Contains("twitter.com") || lower.Contains("x.com")) return "🐦";
            if (lower.Contains("instagram.com")) return "📸";
            if (lower.Contains("youtube.com") || lower.Contains("youtu.be")) return "🎥";
            return "🌐";
        }
    }
}
