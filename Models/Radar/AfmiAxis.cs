namespace AFMI_Montage_projet_2.Models.Radar
{
    /// <summary>
    /// المحور الاستراتيجي لـ AFMI (ضمن المحاور العشرة البيئية والترابية).
    /// يُحفظ في ملف afmi_axes.json ويكون قابلاً للتعديل من الواجهة.
    /// </summary>
    public class AfmiAxis
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
    }
}
