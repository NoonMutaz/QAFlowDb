using WebApplication2.Data;
using WebApplication2.Models;

public class ActivityLogger
{
    private readonly ApplicationDbContext _context;

    public ActivityLogger(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        int projectId,
        int userId,
        string action,
        string entityType,
        int? entityId = null,
        string? details = null)
    {
        var log = new ActivityLog
        {
            ProjectId = projectId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        _context.ActivityLogs.Add(log);

        await _context.SaveChangesAsync();
    }
}