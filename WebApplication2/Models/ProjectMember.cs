namespace WebApplication2.Models
{

    public class ProjectMember
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Role { get; set; } = "member";
        public string Status { get; set; } = "pending";
        public int? InvitedById { get; set; }  //  
        public User? InvitedBy { get; set; }

    }
}
