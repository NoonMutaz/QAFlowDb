using WebApplication2.Models;

public class ActivityLog
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public int UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
}