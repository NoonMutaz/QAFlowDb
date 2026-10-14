using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.DTO;
using WebApplication2.Extensions;

namespace WebApplication2.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/bugs")]
public class BugsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ActivityLogger _logger;

    // Fixed & Configured Constructor Dependency Injection
    public BugsController(ApplicationDbContext context, ActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Helpers & Authentication

    private int GetUserId()
    {
        var idClaim = User.FindFirst("id") ??
                      User.FindFirst(ClaimTypes.NameIdentifier) ??
                      User.FindFirst("sub") ??
                      User.FindFirst(JwtRegisteredClaimNames.Sub);

        var allClaimsDebug = string.Join(" | ",
            User.Claims.Select(c => $"{c.Type}={c.Value}"));

        if (idClaim?.Value == null || !int.TryParse(idClaim.Value, out int userId))
        {
            throw new UnauthorizedAccessException(
                $"No valid ID claim. Available: [{allClaimsDebug}]");
        }

        return userId;
    }

    #region Helpers & Authentication

    private async Task<bool> HasAccessAsync(int projectId)
    {
        try
        {
            // Use the extension method directly
            var userId = User.GetUserId();

            return await _context.ProjectMembers.AnyAsync(m =>
                m.ProjectId == projectId &&
                m.UserId == userId &&
                m.Status == "accepted");
        }
        catch (Exception)
        {
            // If the token is invalid or missing, HasAccess is false
            return false;
        }
    }

    private async Task<bool> IsOwnerAsync(int projectId)
    {
        try
        {
            var userId = User.GetUserId();
            return await _context.ProjectMembers.AnyAsync(m =>
                m.ProjectId == projectId &&
                m.UserId == userId &&
                m.Status == "accepted" &&
                m.Role == "owner");
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static readonly string[] AllowedUploadTypes =
        { "image/jpeg", "image/png", "image/gif", "image/webp", "video/mp4", "video/webm" };

    /// <summary>Saves an uploaded file under wwwroot/uploads and returns its public path.</summary>
    private static async Task<string> SaveUploadAsync(IFormFile file)
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
            await file.CopyToAsync(stream);

        return $"/uploads/{fileName}";
    }
    #endregion

    #region Endpoints

    // Example of the updated endpoint pattern:
    [HttpGet]
    public async Task<IActionResult> Get(int projectId)
    {
        if (!await HasAccessAsync(projectId)) return Forbid();

        var bugs = await _context.Bugs
            .Include(b => b.TestCase)
            .Where(b => b.ProjectId == projectId)
            .ToListAsync();

        return Ok(bugs);
    }

    #endregion
    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateBugDto dto)
    {
        var userId = GetUserId(); // userId from token

        //   Fetch the user's details to populate the missing 'CreatedByName' property
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        var existingBugs = await _context.Bugs
            .Where(b => b.ProjectId == projectId)
            .ToListAsync();

        var lastNumber = existingBugs.Count > 0
            ? existingBugs
                .Select(b =>
                {
                    var parts = b.BugId.Split("-");
                    return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                })
                .DefaultIfEmpty(0)
                .Max()
            : 0;

        var bug = new Bug
        {
            BugId = $"SBC-{(lastNumber + 1).ToString().PadLeft(3, '0')}",
            Name = dto.Name,
            Description = dto.Description,
            Url = dto.Url,
            ExpectedResult = dto.ExpectedResult,
            ActualResult = dto.ActualResult,
            Note = dto.Note,
            Priority = dto.Priority,
            Status = "notFixed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ProjectId = projectId,
            CreatedById = userId.ToString(),
            CreatedByName = user?.Name ?? user?.Email ?? "Unknown Tester", //   Saved so analytics/tables can read it
            TestCaseId = dto.TestCaseId //  Links the Test Case from the frontend payload
        };

        _context.Bugs.Add(bug);
        await _context.SaveChangesAsync();

        //  
        await _logger.LogAsync(
            projectId,
            userId,
            "Create",
            "Bug",
            bug.Id,
            $"{bug.Name} Reported a new bug: {bug.BugId} "
        );

        return Ok(bug);
    }
    [HttpPatch("{bugId}")]
    public async Task<IActionResult> UpdateField(int projectId, int bugId, [FromBody] UpdateFieldDto dto)
    {
        try
        {
            if (!await HasAccessAsync(projectId)) return Forbid();

            var userId = User.GetUserId();

            var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
                b.Id == bugId && b.ProjectId == projectId);

            if (bug == null) return NotFound();

            string logDetails = $"Updated field '{dto.Field}'";

            switch (dto.Field.ToLower())
            {
                case "url":
                    bug.Url = dto.Value;
                    break;
                case "expectedresult":
                    bug.ExpectedResult = dto.Value;
                    break;
                case "actualresult":
                    bug.ActualResult = dto.Value;
                    break;
                case "description":
                    bug.Description = dto.Value;
                    break;
                case "note":
                    bug.Note = dto.Value;
                    break;
                case "deadline":
                    if (string.IsNullOrWhiteSpace(dto.Value))
                    {
                        bug.Deadline = null;
                        logDetails = "Cleared the deadline";
                    }
                    else
                    {
                        var day = dto.Value.Length >= 10 ? dto.Value[..10] : dto.Value;
                        if (!DateOnly.TryParseExact(day, "yyyy-MM-dd", out _))
                            return BadRequest("Deadline must be a date (YYYY-MM-DD).");
                        bug.Deadline = day;
                        logDetails = $"Set the deadline to {day}";
                    }
                    break;
                case "assignedtouserid":
                    if (string.IsNullOrWhiteSpace(dto.Value))
                    {
                        bug.AssignedToUserId = null;
                        bug.AssignedToEmail = null;
                        bug.AssignedById = null;
                        bug.AssignedByName = null;
                        bug.AssignedAt = null;
                        logDetails = "Unassigned bug";
                    }
                    else
                    {
                        if (!int.TryParse(dto.Value, out int targetUserId))
                            return BadRequest("Invalid User ID format");

                        var projectMember = await _context.ProjectMembers
                            .Include(m => m.User)
                            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == targetUserId && m.Status == "accepted");

                        if (projectMember == null)
                            return BadRequest("Target assignment user is not an active team member.");

                        var assignerId = GetUserId();
                        var assigner = await _context.Users.FirstOrDefaultAsync(u => u.Id == assignerId);

                        bug.AssignedToUserId = targetUserId;
                        bug.AssignedToEmail = projectMember.User.Email;
                        bug.AssignedById = assignerId;
                        bug.AssignedByName = assigner?.Name ?? assigner?.Email ?? "System";
                        bug.AssignedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        logDetails = $"Assigned bug to {bug.AssignedToEmail}";
                    }
                    break;
                default:
                    return BadRequest($"Field '{dto.Field}' is invalid or protected.");
            }

            await _context.SaveChangesAsync();

            // LOGGING: Dynamic Field / Assignment Updates
            await _logger.LogAsync(projectId, userId, "Update", "Bug", bug.Id, $"{logDetails} on {bug.BugId}");

            return Ok(new
            {
                bug.Id,
                bug.BugId,
                bug.ExpectedResult,
                bug.ActualResult,
                bug.Description,
                bug.Note,
                bug.Url,
                bug.Status,
                bug.Priority,
                bug.AssignedToUserId,
                bug.AssignedToEmail,
                bug.AssignedById,
                bug.AssignedByName,
                bug.AssignedAt,
                bug.Deadline
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateField error: {ex}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPatch("{bugId}/status")]
    public async Task<IActionResult> Status(int projectId, int bugId, [FromBody] UpdateStatusDto dto)
    {
        if (!await HasAccessAsync(projectId)) return Forbid();

        var userId = GetUserId();
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        // Closing goes through review: assignees submit evidence via /resolve and the owner approves.
        if (dto.Status == "pendingReview")
            return BadRequest("Use the resolve endpoint with an evidence photo to submit a fix for review.");
        if (dto.Status == "fixed" && !await IsOwnerAsync(projectId))
            return BadRequest("Upload a photo of the fix and submit it for the owner's review.");

        var oldStatus = bug.Status;
        bug.Status = dto.Status;
        await _context.SaveChangesAsync();

        // LOGGING: Status Change
        await _logger.LogAsync(projectId, userId, "UpdateStatus", "Bug", bug.Id, $"Changed status of {bug.BugId} from '{oldStatus}' to '{dto.Status}'");

        return Ok(bug);
    }

    [HttpPatch("{bugId}/priority")]
    public async Task<IActionResult> Priority(int projectId, int bugId, [FromBody] UpdatePriorityDto dto)
    {
        if (!await HasAccessAsync(projectId)) return Forbid();

        var userId = GetUserId();
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        var oldPriority = bug.Priority;
        bug.Priority = dto.Priority;
        await _context.SaveChangesAsync();

        // LOGGING: Priority Change
        await _logger.LogAsync(projectId, userId, "UpdatePriority", "Bug", bug.Id, $"Changed priority of {bug.BugId} from '{oldPriority}' to '{dto.Priority}'");

        return Ok(bug);
    }

    [HttpDelete("{bugId}")]
    public async Task<IActionResult> Delete(int projectId, int bugId)
    {
        if (!await HasAccessAsync(projectId)) return Forbid();

        var userId = GetUserId();
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        _context.Bugs.Remove(bug);
        await _context.SaveChangesAsync();

        // LOGGING: Bug Deletion
        await _logger.LogAsync(projectId, userId, "Delete", "Bug", bugId, $"Deleted bug {bug.BugId}: {bug.Name}");

        return NoContent();
    }

    [HttpPost("{bugId}/upload")]
    public async Task<IActionResult> Upload(int projectId, int bugId, IFormFile file)
    {
        if (!await HasAccessAsync(projectId)) return Forbid();

        var userId = User.GetUserId();
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("No file uploaded");
        if (!AllowedUploadTypes.Contains(file.ContentType)) return BadRequest("Invalid file type");

        bug.AttachmentUrl = await SaveUploadAsync(file);
        await _context.SaveChangesAsync();

        // LOGGING: File Attachment Upload
        await _logger.LogAsync(projectId, userId, "UploadAttachment", "Bug", bug.Id, $"Uploaded attachment context for {bug.BugId}");

        // attachmentUrl is what the dashboard table reads; url kept for older callers
        return Ok(new { url = bug.AttachmentUrl, attachmentUrl = bug.AttachmentUrl });
    }

    /// <summary>
    /// The assignee submits a fix: a photo of the corrected work is required.
    /// The bug moves to "pendingReview" until the project owner approves or rejects it.
    /// </summary>
    [HttpPost("{bugId}/resolve")]
    public async Task<IActionResult> Resolve(int projectId, int bugId, IFormFile file)
    {
        if (!await HasAccessAsync(projectId)) return Forbid();

        var userId = User.GetUserId();
        var bug = await _context.Bugs.FirstOrDefaultAsync(b => b.Id == bugId && b.ProjectId == projectId);
        if (bug == null) return NotFound();

        if (bug.AssignedToUserId != userId && !await IsOwnerAsync(projectId))
            return Forbid();
        if (bug.Status == "fixed") return BadRequest("This finding is already closed.");
        if (file == null || file.Length == 0) return BadRequest("A photo of the fix is required.");
        if (!file.ContentType.StartsWith("image/") || !AllowedUploadTypes.Contains(file.ContentType))
            return BadRequest("The evidence must be an image (JPG, PNG, GIF or WebP).");

        bug.ResolutionEvidenceUrl = await SaveUploadAsync(file);
        bug.Status = "pendingReview";
        bug.SubmittedForReviewAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bug.ReviewComment = null;
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, userId, "SubmitForReview", "Bug", bug.Id, $"Submitted a fix for {bug.BugId} for review");

        return Ok(bug);
    }

    /// <summary>
    /// The project owner reviews a submitted fix. Approve closes it ("fixed");
    /// reject sends it back to the assignee ("notFixed") with the reason.
    /// </summary>
    [HttpPost("{bugId}/review")]
    public async Task<IActionResult> Review(int projectId, int bugId, [FromBody] ReviewFixDto dto)
    {
        if (!await IsOwnerAsync(projectId)) return Forbid();

        var userId = User.GetUserId();
        var bug = await _context.Bugs.FirstOrDefaultAsync(b => b.Id == bugId && b.ProjectId == projectId);
        if (bug == null) return NotFound();
        if (bug.Status != "pendingReview") return BadRequest("This finding is not waiting for review.");

        var comment = dto.Comment?.Trim();
        if (!dto.Approve && string.IsNullOrEmpty(comment))
            return BadRequest("Please give a reason for rejecting the fix.");

        bug.Status = dto.Approve ? "fixed" : "notFixed";
        bug.ReviewComment = dto.Approve ? null : comment;
        if (dto.Approve)
        {
            var reviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            bug.ReviewedByName = reviewer?.Name ?? reviewer?.Email;
            bug.ReviewedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        await _context.SaveChangesAsync();

        await _logger.LogAsync(
            projectId, userId, dto.Approve ? "ApproveFix" : "RejectFix", "Bug", bug.Id,
            dto.Approve ? $"Approved the fix for {bug.BugId}" : $"Rejected the fix for {bug.BugId}: {comment}");

        return Ok(bug);
    }




    //[HttpGet("/api/bugs/me")]
    //public async Task<IActionResult> GetMyAssignedBugs()
    //{
    //    try
    //    {
    //        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
    //                         User.FindFirstValue("id") ??
    //                         User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    //        if (string.IsNullOrEmpty(claimValue) || !int.TryParse(claimValue, out int userId))
    //        {
    //            return Unauthorized(new { message = "User identification token is invalid or missing." });
    //        }

    //        var myBugs = await _context.Bugs
    //            .Where(b => b.AssignedToUserId == userId)
    //            .OrderByDescending(b => b.Priority == "High")
    //            .ThenByDescending(b => b.Id)
    //            .ToListAsync();

    //        return Ok(myBugs);
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"GetMyAssignedBugs error: {ex}");
    //        return StatusCode(500, "Internal server error");
    //    }
    //}
    #endregion
}