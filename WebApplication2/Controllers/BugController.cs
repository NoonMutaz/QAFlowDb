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
[Route("api/projects/{projectId}/bugs")]  // Keep this route
public class BugsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BugsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ✅ COPY THIS EXACT METHOD FROM ProjectsController
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

    // ✅ UPDATED HasAccess using GetUserId()
    private bool HasAccess(int projectId)
    {
        var userId = GetUserId();
        return _context.ProjectMembers.Any(m =>
            m.ProjectId == projectId &&
            m.UserId == userId &&
            m.Status == "accepted");
    }

    // ✅ YOUR NEW UPDATE FIELD ENDPOINT
    [HttpPatch("{bugId}")]
    public async Task<IActionResult> UpdateField(int projectId, int bugId, [FromBody] UpdateFieldDto dto)
    {
        try
        {
            if (!HasAccess(projectId))  // Now uses GetUserId()!
                return Forbid();

            var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
                b.Id == bugId && b.ProjectId == projectId);

            if (bug == null) return NotFound();

            // Only allow editable fields for owner/member
            switch (dto.Field.ToLower())
            {
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
                default:
                    return BadRequest("Invalid field");
            }

            await _context.SaveChangesAsync();
            return Ok(bug);
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
        [HttpGet]
    public async Task<IActionResult> Get(int projectId)
    {
        var bugs = await _context.Bugs
            .Where(b => b.ProjectId == projectId)
            .ToListAsync();

        return Ok(bugs);
    }

    [HttpPatch("{bugId}/status")]
    public async Task<IActionResult> Status(int projectId, int bugId, [FromBody] UpdateStatusDto dto)
    {
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        bug.Status = dto.Status;
        await _context.SaveChangesAsync();

        return Ok(bug);
    }

    [HttpDelete("{bugId}")]
    public async Task<IActionResult> Delete(int projectId, int bugId)
    {
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        _context.Bugs.Remove(bug);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateBugDto dto)
    {
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

    [HttpPatch("{bugId}/priority")]
    public async Task<IActionResult> Priority(int projectId, int bugId, [FromBody] UpdatePriorityDto dto)
    {
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();

        bug.Priority = dto.Priority;
        await _context.SaveChangesAsync();

        return Ok(bug);
    }
    //[HttpPatch("{bugId}")]
    //public async Task<IActionResult> UpdateField(int projectId, int bugId, [FromBody] UpdateFieldDto dto)
    //{
    //    var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");

    //    if (!HasAccess(projectId, userId))
    //        return Forbid();

    //    var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
    //        b.Id == bugId && b.ProjectId == projectId);

    //    if (bug == null) return NotFound();

    //    // Only allow specific editable fields
    //    switch (dto.Field.ToLower())
    //    {
    //        case "expectedresult":
    //            bug.ExpectedResult = dto.Value;
    //            break;
    //        case "actualresult":
    //            bug.ActualResult = dto.Value;
    //            break;
    //        case "description":
    //            bug.Description = dto.Value;
    //            break;
    //        case "note":
    //            bug.Note = dto.Value;
    //            break;
    //        default:
    //            return BadRequest("Invalid field");
    //    }

    //    await _context.SaveChangesAsync();
    //    return Ok(bug);
    //}
    [HttpPost("{bugId}/upload")]
    public async Task<IActionResult> Upload(int projectId, int bugId, IFormFile file)
    {
        var bug = await _context.Bugs.FirstOrDefaultAsync(b =>
            b.Id == bugId && b.ProjectId == projectId);

        if (bug == null) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("No file");

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


}