using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Extensions;
using WebApplication2.Models;

namespace WebApplication2.Controllers;

/// <summary>Project schedule: phases with dates and the SBC clauses worked on in each.</summary>
[Authorize]
[ApiController]
[Route("api/projects/{projectId}/phases")]
public class ProjectPhasesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ActivityLogger _logger;

    public ProjectPhasesController(ApplicationDbContext context, ActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public class PhaseDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public List<int> ClauseIds { get; set; } = new();
    }

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

    private static bool IsDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>Validates the DTO and keeps only clause ids that belong to the project.</summary>
    private async Task<(string? error, List<int> clauseIds)> ValidateAsync(int projectId, PhaseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return ("Phase name is required.", new());
        if (!IsDate(dto.StartDate) || !IsDate(dto.EndDate)) return ("Start and end dates are required (YYYY-MM-DD).", new());
        if (string.CompareOrdinal(dto.EndDate, dto.StartDate) < 0) return ("The end date must be on or after the start date.", new());

        var requested = (dto.ClauseIds ?? new()).Distinct().ToList();
        var valid = await _context.TestCases
            .Where(t => t.ProjectId == projectId && requested.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();
        return (null, requested.Where(valid.Contains).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> List(int projectId)
    {
        if (!await IsMemberAsync(projectId)) return Forbid();

        var phases = await _context.ProjectPhases
            .Where(p => p.ProjectId == projectId)
            .OrderBy(p => p.StartDate).ThenBy(p => p.Id)
            .ToListAsync();
        return Ok(phases);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] PhaseDto dto)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();
        var (error, clauseIds) = await ValidateAsync(projectId, dto);
        if (error != null) return BadRequest(error);

        var phase = new ProjectPhase
        {
            ProjectId = projectId,
            Name = dto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ClauseIds = clauseIds,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        _context.ProjectPhases.Add(phase);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, User.GetUserId(), "Create", "Phase", phase.Id, $"Added phase {phase.Name} ({phase.StartDate} – {phase.EndDate})");
        return Ok(phase);
    }

    [HttpPut("{phaseId}")]
    public async Task<IActionResult> Update(int projectId, int phaseId, [FromBody] PhaseDto dto)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();
        var phase = await _context.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.ProjectId == projectId);
        if (phase == null) return NotFound();

        var (error, clauseIds) = await ValidateAsync(projectId, dto);
        if (error != null) return BadRequest(error);

        phase.Name = dto.Name.Trim();
        phase.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        phase.StartDate = dto.StartDate;
        phase.EndDate = dto.EndDate;
        phase.ClauseIds = clauseIds;
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, User.GetUserId(), "Update", "Phase", phase.Id, $"Updated phase {phase.Name}");
        return Ok(phase);
    }

    [HttpDelete("{phaseId}")]
    public async Task<IActionResult> Delete(int projectId, int phaseId)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();
        var phase = await _context.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.ProjectId == projectId);
        if (phase == null) return NotFound();

        _context.ProjectPhases.Remove(phase);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, User.GetUserId(), "Delete", "Phase", phaseId, $"Deleted phase {phase.Name}");
        return NoContent();
    }

    private async Task<bool> IsOwnerAsync(int projectId)
    {
        try
        {
            var userId = User.GetUserId();
            return await _context.ProjectMembers.AnyAsync(m =>
                m.ProjectId == projectId && m.UserId == userId && m.Status == "accepted" && m.Role == "owner");
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// A clause is done when it has findings and all are fixed, or it has none and was marked compliant.
    /// Returns the titles of the clauses that are still open.
    /// </summary>
    private async Task<List<string>> OpenClausesAsync(int projectId, List<int> clauseIds)
    {
        var clauses = await _context.TestCases
            .Where(t => t.ProjectId == projectId && clauseIds.Contains(t.Id))
            .Select(t => new { t.Title, t.Status, Bugs = t.LinkedBugs.Select(b => b.Status).ToList() })
            .ToListAsync();

        return clauses
            .Where(c => c.Bugs.Count > 0 ? c.Bugs.Any(s => s != "fixed") : c.Status != "passed")
            .Select(c => c.Title)
            .ToList();
    }

    /// <summary>The owner closes a phase. Refused while any of its clauses is still open (the phase stays locked).</summary>
    [HttpPost("{phaseId}/close")]
    public async Task<IActionResult> Close(int projectId, int phaseId)
    {
        if (!await IsOwnerAsync(projectId)) return Forbid();
        var phase = await _context.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.ProjectId == projectId);
        if (phase == null) return NotFound();
        if (phase.ClosedAt != null) return BadRequest("This phase is already closed.");
        if (phase.ClauseIds.Count == 0) return BadRequest("Add SBC clauses to the phase before closing it.");

        var open = await OpenClausesAsync(projectId, phase.ClauseIds);
        if (open.Count > 0)
            return Conflict(new { message = "The phase stays locked until all its clauses are done.", openClauses = open });

        var userId = User.GetUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        phase.ClosedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        phase.ClosedByName = user?.Name ?? user?.Email;
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, userId, "Close", "Phase", phase.Id, $"Closed phase {phase.Name}");
        return Ok(phase);
    }

    [HttpPost("{phaseId}/reopen")]
    public async Task<IActionResult> Reopen(int projectId, int phaseId)
    {
        if (!await IsOwnerAsync(projectId)) return Forbid();
        var phase = await _context.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.ProjectId == projectId);
        if (phase == null) return NotFound();

        phase.ClosedAt = null;
        phase.ClosedByName = null;
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, User.GetUserId(), "Reopen", "Phase", phase.Id, $"Reopened phase {phase.Name}");
        return Ok(phase);
    }

    /// <summary>Everything the phase report shows: the phase, its clauses, and each clause's findings with before/after evidence.</summary>
    [HttpGet("{phaseId}/report")]
    public async Task<IActionResult> Report(int projectId, int phaseId)
    {
        if (!await IsMemberAsync(projectId)) return Forbid();
        var phase = await _context.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.ProjectId == projectId);
        if (phase == null) return NotFound();

        var project = await _context.Projects.Where(p => p.Id == projectId).Select(p => new { p.Id, p.Name }).FirstOrDefaultAsync();
        var clauses = await _context.TestCases
            .Where(t => t.ProjectId == projectId && phase.ClauseIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.ExpectedResult,
                t.Status,
                Findings = t.LinkedBugs.Select(b => new
                {
                    b.Id,
                    b.BugId,
                    b.Status,
                    b.Priority,
                    b.Url,
                    b.ActualResult,
                    b.Description,
                    b.CreatedAt,
                    b.CreatedByName,
                    b.AssignedToEmail,
                    b.Deadline,
                    b.AttachmentUrl,
                    b.ResolutionEvidenceUrl,
                    b.ReviewedByName,
                    b.ReviewedAt,
                }).ToList(),
            })
            .ToListAsync();

        return Ok(new { project, phase, clauses });
    }
}
