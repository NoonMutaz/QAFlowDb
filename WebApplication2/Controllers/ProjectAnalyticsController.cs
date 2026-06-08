using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.DTOs;
using WebApplication2.Extensions;

namespace WebApplication2.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{id}")]
public class ProjectAnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProjectAnalyticsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetProjectActivity(int id)
    {
        var userId = User.GetUserId();
        var isOwner = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Status == "accepted" && m.Role == "owner");
        if (!isOwner) return Forbid();

        var activities = await _context.ActivityLogs
            .Where(a => a.ProjectId == id)
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt).Take(50)
            .Select(a => new
            {
                a.Id,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.CreatedAt,
                User = new { Name = a.User.Name ?? a.User.Email, Email = a.User.Email }
            }).ToListAsync();

        return Ok(activities);
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<ProjectAnalyticsDto>> GetProjectAnalytics(int id)
    {
        var userId = User.GetUserId();
        var hasAccess = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Status == "accepted");
        if (!hasAccess) return Forbid();

        if (!await _context.Projects.AnyAsync(p => p.Id == id)) return NotFound();

        var openBugs = await _context.Bugs.CountAsync(b => b.ProjectId == id && b.Status == "notFixed");
        var fixedBugs = await _context.Bugs.CountAsync(b => b.ProjectId == id && b.Status == "Fixed");
        var criticalBugs = await _context.Bugs.CountAsync(b => b.ProjectId == id && b.Priority == "High" && b.Status != "Fixed");

        var topContributor = await _context.Bugs
            .Where(b => b.ProjectId == id && b.Status == "Fixed" && b.AssignedById != null)
            .GroupBy(b => new { b.AssignedById, b.AssignedByName })
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key.AssignedByName).FirstOrDefaultAsync() ?? "None";

        var mostActiveTester = await _context.Bugs
            .Where(b => b.ProjectId == id && b.CreatedById != null)
            .GroupBy(b => new { b.CreatedById, b.CreatedByName })
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key.CreatedByName).FirstOrDefaultAsync() ?? "None";

        return Ok(new ProjectAnalyticsDto
        {
            OpenBugs = openBugs,
            FixedBugs = fixedBugs,
            CriticalBugs = criticalBugs,
            TopContributor = topContributor,
            MostActiveTester = mostActiveTester
        });
    }
}