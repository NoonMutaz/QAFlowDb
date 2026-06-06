namespace WebApplication2.DTOs;

public class ProjectAnalyticsDto
{
    public int OpenBugs { get; set; }
    public int FixedBugs { get; set; }
    public int CriticalBugs { get; set; }
    public string TopContributor { get; set; } = "N/A";
    public string MostActiveTester { get; set; } = "N/A";
}