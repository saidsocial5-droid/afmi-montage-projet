using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AFMI_Montage_projet_2.Models.Radar
{
    public enum EnrichmentMergeMode
    {
        Append,
        Replace,
        Prepend
    }

    /// <summary>
    /// Représente une proposition d'enrichissement d'un champ du profil de l'Association issue de l'analyse IA de documents ou de liens web/réseaux sociaux.
    /// </summary>
    public partial class ProfileEnrichmentProposal : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _fieldName = string.Empty;

        [ObservableProperty]
        private string _fieldDisplayName = string.Empty;

        [ObservableProperty]
        private string _category = "Général";

        [ObservableProperty]
        private string _currentValue = string.Empty;

        [ObservableProperty]
        private string _suggestedValue = string.Empty;

        [ObservableProperty]
        private EnrichmentMergeMode _mergeMode = EnrichmentMergeMode.Append;

        [ObservableProperty]
        private string _sourceOrigin = string.Empty;

        [ObservableProperty]
        private string _rationale = string.Empty;

        public string MergeModeDisplay => MergeMode switch
        {
            EnrichmentMergeMode.Append => "Ajouter à la suite (Fusionner)",
            EnrichmentMergeMode.Replace => "Remplacer la valeur actuelle",
            EnrichmentMergeMode.Prepend => "Insérer au début",
            _ => "Modifier"
        };
    }
}
