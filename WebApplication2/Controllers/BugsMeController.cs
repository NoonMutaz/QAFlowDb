using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Data;
using WebApplication2.Extensions;
using WebApplication2.Models;
using Microsoft.EntityFrameworkCore;

[Authorize]
[ApiController]
[Route("api/bugs")]
public class BugsMeController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BugsMeController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyAssignedBugs()
    {
        var userId = User.GetUserId();

        var myBugs = await _context.Bugs
            .Where(b => b.AssignedToUserId == userId)
            .OrderByDescending(b => b.Priority == "High")
            .ThenByDescending(b => b.Id)
            .ToListAsync();

        return Ok(myBugs);
    }

    /// <summary>
    /// Fixes waiting for this user's approval: bugs in "pendingReview" in projects the user owns.
    /// Polled by the frontend to show the owner's review notifications.
    /// </summary>
    [HttpGet("reviews")]
    public async Task<IActionResult> GetPendingReviews()
    {
        var userId = User.GetUserId();

        var ownedProjectIds = _context.ProjectMembers
            .Where(m => m.UserId == userId && m.Role == "owner" && m.Status == "accepted")
            .Select(m => m.ProjectId);

        var reviews = await _context.Bugs
            .Where(b => b.Status == "pendingReview" && ownedProjectIds.Contains(b.ProjectId))
            .OrderBy(b => b.SubmittedForReviewAt)
            .Select(b => new
            {
                b.Id,
                b.BugId,
                b.ProjectId,
                ProjectName = b.Project.Name,
                b.Priority,
                b.Description,
                b.ActualResult,
                b.ExpectedResult,
                b.Url,
                b.AttachmentUrl,
                b.ResolutionEvidenceUrl,
                b.SubmittedForReviewAt,
                b.AssignedToEmail,
                TestCaseTitle = b.TestCase != null ? b.TestCase.Title : null,
            })
            .ToListAsync();

        return Ok(reviews);
    }
}