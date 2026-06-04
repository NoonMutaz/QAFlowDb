using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebApplication2.Data;
using WebApplication2.Models.DTO;

namespace WebApplication2.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/bugs")]
public class BugsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BugsController(ApplicationDbContext context)
    {
        _context = context;
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

    private bool HasAccess(int projectId)
    {
        var userId = GetUserId();
        return _context.ProjectMembers.Any(m =>
            m.ProjectId == projectId &&
            m.UserId == userId &&
            m.Status == "accepted");
    }

    #endregion

    #region Endpoints

    [HttpGet]
    public async Task<IActionResult> Get(int projectId)
    {
        if (!HasAccess(projectId)) return Forbid();

        var bugs = await _context.Bugs
            .Where(b => b.ProjectId == projectId)
            .ToListAsync();

        return Ok(bugs);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateBugDto dto)
    {
        if (!HasAccess(projectId)) return Forbid();

        var existingBugs = await _context.Bugs
            .Where(b => b.ProjectId == projectId)
            .ToListAsync();

        var lastNumber = existingBugs.Count > 0
            ? existingBugs
                .Select(b => {
                    var parts = b.BugId.Split("-");
                    return parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                })
                .DefaultIfEmpty(0)
                .Max()
            : 0;

        var bug = new WebApplication2.Models.Bug
        {
            BugId = $"BUG-{(lastNumber + 1).ToString().PadLeft(3, '0')}",
            Name = dto.Name,
            Description = dto.Description,
            Url = dto.Url,
            ExpectedResult = dto.ExpectedResult,
            ActualResult = dto.ActualResult,
            Note = dto.Note,
            Priority = dto.Priority,
            Status = "notFixed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ProjectId = projectId
        };

        _context.Bugs.Add(bug);
        await _context.SaveChangesAsync();

        return Ok(bug);
    }

    /// <summary>
    /// Handles inline text updates (Expected, Actual, Description, Note, URL) 
    /// AND custom assignment parameters dynamically!
    /// </summary>
    [HttpPatch("{bugId}")]
    public async Task<IActionResult> UpdateField(int projectId, int bugId, [FromBody] UpdateFieldDto dto)
    {
        try
        {
            if (!HasAccess(projectId)) return Forbid();

            var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
                b.Id == bugId && b.ProjectId == projectId);

            if (bug == null) return NotFound();

            string assignedEmail = null;

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
                case "assignedtouserid":
                    if (string.IsNullOrWhiteSpace(dto.Value))
                    {
                        bug.AssignedToUserId = null;
                        bug.AssignedToEmail = null;
                        bug.AssignedById = null;
                        bug.AssignedByName = null;
                        bug.AssignedAt = null;
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

                        // Get the ID of the person making the change (the logged-in user)
                        var assignerId = GetUserId();
                        var assigner = await _context.Users.FirstOrDefaultAsync(u => u.Id == assignerId);

                        // Update target assignee info
                        bug.AssignedToUserId = targetUserId;
                        bug.AssignedToEmail = projectMember.User.Email;

                        // Automatically stamp assignment tracking context metadata
                        bug.AssignedById = assignerId;
                        bug.AssignedByName = assigner?.Name ?? assigner?.Email ?? "System";
                        bug.AssignedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                    break;
                default:
                    return BadRequest($"Field '{dto.Field}' is invalid or protected.");
            }

            await _context.SaveChangesAsync();

            // Return back the updated bug, plus computed fields for assignment
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
                bug.AssignedAt
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
        if (!HasAccess(projectId)) return Forbid();

        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        bug.Status = dto.Status;
        await _context.SaveChangesAsync();

        return Ok(bug);
    }

    [HttpPatch("{bugId}/priority")]
    public async Task<IActionResult> Priority(int projectId, int bugId, [FromBody] UpdatePriorityDto dto)
    {
        if (!HasAccess(projectId)) return Forbid();

        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        bug.Priority = dto.Priority;
        await _context.SaveChangesAsync();

        return Ok(bug);
    }

    [HttpDelete("{bugId}")]
    public async Task<IActionResult> Delete(int projectId, int bugId)
    {
        if (!HasAccess(projectId)) return Forbid();

        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        _context.Bugs.Remove(bug);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{bugId}/upload")]
    public async Task<IActionResult> Upload(int projectId, int bugId, IFormFile file)
    {
        if (!HasAccess(projectId)) return Forbid();

        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("No file uploaded");

        var allowed = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "video/mp4", "video/webm" };
        if (!allowed.Contains(file.ContentType)) return BadRequest("Invalid file type");

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(folder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        bug.AttachmentUrl = $"/uploads/{fileName}";
        await _context.SaveChangesAsync();

        return Ok(new { url = bug.AttachmentUrl });
    }

    [HttpGet("/api/bugs/me")] // Route: GET api/projects/all/bugs/me  
    public async Task<IActionResult> GetMyAssignedBugs()
    {
        try
        {
            // 1. Resolve user ID using the clean parsing standard from ProjectsController
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                             User.FindFirstValue("id") ??
                             User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(claimValue) || !int.TryParse(claimValue, out int userId))
            {
                return Unauthorized(new { message = "User identification token is invalid or missing." });
            }

            // 2. Query bugs assigned to this user directly without blocking project owners/creators
            var myBugs = await _context.Bugs
                .Where(b => b.AssignedToUserId == userId)
                .OrderByDescending(b => b.Priority == "High")
                .ThenByDescending(b => b.Id)
                .ToListAsync();

            return Ok(myBugs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMyAssignedBugs error: {ex}");
            return StatusCode(500, "Internal server error");
        }
    }
    #endregion
}