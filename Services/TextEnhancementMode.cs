namespace AFMI_Montage_projet_2.Services
{
    /// <summary>
    /// Modes historiques d'amélioration de texte, conservés pour les appels existants
    /// (AiTextAssistant.OpenEnhancementDialog, AiTextEnhancementDialog) qui les convertissent
    /// en TextAssistActionType. Le traitement passe par TextAssistService.
    /// </summary>
    public enum TextEnhancementMode
    {
        Ameliorer,
        Enrichir,
        Detailler,
        Reformuler,
        Generer,
        Corriger
    }

    /// <summary>Action choisie par l'utilisateur dans AiTextEnhancementDialog.</summary>
    public enum AiEnhancementAction
    {
        None,
        Replace,
        Append
    }
}
