namespace WebApplication2.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";

        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>(); 
        public int UserId { get; set; }


    }
}
