namespace WebApplication2.Models
{
    public class Bug
    {
        public int Id { get; set; }
        public string BugId { get; set; } = ""; // "BUG-001"
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string ExpectedResult { get; set; } = "";
        public string ActualResult { get; set; } = "";
        public string Note { get; set; } = "";
        public string Priority { get; set; } = "Medium";
        public string Status { get; set; } = "notFixed";
        public long CreatedAt { get; set; }
        public int ProjectId { get; set; }

        public string? AttachmentUrl { get; set; }
        public Project Project { get; set; } = null!;
    }
}