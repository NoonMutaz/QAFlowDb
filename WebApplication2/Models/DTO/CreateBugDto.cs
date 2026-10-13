namespace WebApplication2.Models.DTO
{
    public class CreateBugDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string ExpectedResult { get; set; } = "";
        public string ActualResult { get; set; } = "";
        public string Note { get; set; } = "";
        public string Priority { get; set; } = "Medium";
        public string? AttachmentUrl { get; set; }

        public int? TestCaseId { get; set; }
    }
    public class UpdateFieldDto
    {
        public string Field { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
    public class UpdateStatusDto { public string Status { get; set; } = ""; }
    public class UpdatePriorityDto { public string Priority { get; set; } = ""; }

    /// <summary>Owner's decision on a fix submitted for review.</summary>
    public class ReviewFixDto
    {
        public bool Approve { get; set; }
        public string? Comment { get; set; }
    }
}
