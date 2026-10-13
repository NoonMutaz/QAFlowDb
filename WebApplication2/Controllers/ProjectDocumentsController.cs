using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Extensions;
using WebApplication2.Models;

namespace WebApplication2.Controllers;

/// <summary>Project drawings / plans that the AI reads and uses when analysing findings.</summary>
[Authorize]
[ApiController]
[Route("api/projects/{projectId}/documents")]
public class ProjectDocumentsController : ControllerBase
{
    private const long MaxBytes = 20 * 1024 * 1024;
    private static readonly string[] AllowedTypes = { "application/pdf", "image/jpeg", "image/png", "image/webp" };

    private readonly ApplicationDbContext _context;
    private readonly ActivityLogger _logger;

    public ProjectDocumentsController(ApplicationDbContext context, ActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public class SummaryDto { public string? Summary { get; set; } }

    private async Task<bool> IsMemberAsync(int projectId, bool editorsOnly = false)
    {
        try
        {
            var userId = User.GetUserId();
            return await _context.ProjectMembers.AnyAsync(m =>
                m.ProjectId == projectId &&
                m.UserId == userId &&
                m.Status == "accepted" &&
                (!editorsOnly || m.Role == "owner" || m.Role == "member"));
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(int projectId)
    {
        if (!await IsMemberAsync(projectId)) return Forbid();

        var docs = await _context.ProjectDocuments
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => d.UploadedAt)
            .ToListAsync();
        return Ok(docs);
    }

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(int projectId, IFormFile file)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        if (file.Length > MaxBytes) return BadRequest("The file is larger than 20 MB.");
        if (!AllowedTypes.Contains(file.ContentType)) return BadRequest("Upload a PDF or an image (JPG, PNG, WebP).");

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "drawings");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
            await file.CopyToAsync(stream);

        var userId = User.GetUserId();
        var doc = new ProjectDocument
        {
            ProjectId = projectId,
            FileName = Path.GetFileName(file.FileName),
            Url = $"/uploads/drawings/{fileName}",
            ContentType = file.ContentType,
            Size = file.Length,
            UploadedById = userId,
            UploadedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        _context.ProjectDocuments.Add(doc);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, userId, "Upload", "Document", doc.Id, $"Uploaded drawing {doc.FileName}");
        return Ok(doc);
    }

    /// <summary>Stores the AI's design summary (written by the Next.js AI route after reading the file).</summary>
    [HttpPut("{docId}/summary")]
    public async Task<IActionResult> SaveSummary(int projectId, int docId, [FromBody] SummaryDto dto)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();

        var doc = await _context.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.ProjectId == projectId);
        if (doc == null) return NotFound();

        doc.AiSummary = string.IsNullOrWhiteSpace(dto.Summary) ? null : dto.Summary.Trim();
        await _context.SaveChangesAsync();
        return Ok(doc);
    }

    [HttpDelete("{docId}")]
    public async Task<IActionResult> Delete(int projectId, int docId)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();

        var doc = await _context.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.ProjectId == projectId);
        if (doc == null) return NotFound();

        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

        _context.ProjectDocuments.Remove(doc);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, User.GetUserId(), "Delete", "Document", docId, $"Deleted drawing {doc.FileName}");
        return NoContent();
    }
}
