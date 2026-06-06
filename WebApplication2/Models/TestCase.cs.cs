// Models/TestCase.cs
using WebApplication2.Models;

public class TestCase
{
    public int Id { get; set; }
    public string Code { get; set; } // e.g., "TC-001"
    public string Title { get; set; } // e.g., "Verify Login"
    public string Steps { get; set; } // Store steps as markdown text or a list string
    public string ExpectedResult { get; set; }
    public int ProjectId { get; set; }

    // Navigation property: One test case can track multiple linked bugs
    public ICollection<Bug> LinkedBugs { get; set; } = new List<Bug>();

}