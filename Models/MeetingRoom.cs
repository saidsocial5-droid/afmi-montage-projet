using System;

namespace AFMI_Montage_projet_2.Models
{
    public class MeetingRoom : BaseEntity
    {
        public int Id { get; set; }
        
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public string RoomName { get; set; } = string.Empty;
        public string RoomUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Platform { get; set; } = "Jitsi"; // ou "Google Meet"
    }
}



