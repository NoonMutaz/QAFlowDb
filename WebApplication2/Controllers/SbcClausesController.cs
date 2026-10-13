using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Extensions;
using WebApplication2.Models;

namespace WebApplication2.Controllers;

/// <summary>Shared library of verified SBC clauses that the AI suggests from.</summary>
[Authorize]
[ApiController]
[Route("api/sbc-clauses")]
public class SbcClausesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SbcClausesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public class SbcClauseDto
    {
        public string CodeType { get; set; } = "";
        public string CodeVersion { get; set; } = "";
        public string SectionNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string RequirementText { get; set; } = "";
        public string? SourceUrl { get; set; }
        public string? VerifiedBy { get; set; }
    }

    private static string? Validate(SbcClauseDto dto) =>
        string.IsNullOrWhiteSpace(dto.CodeType) ? "Code type is required (e.g. SBC 201)."
        : string.IsNullOrWhiteSpace(dto.SectionNumber) ? "Section number is required."
        : string.IsNullOrWhiteSpace(dto.Title) ? "Title is required."
        : string.IsNullOrWhiteSpace(dto.RequirementText) ? "Requirement text is required."
        : null;

    /// <summary>Copies the DTO onto the entity; the verifier defaults to the signed-in user.</summary>
    private async Task Apply(SbcClause clause, SbcClauseDto dto)
    {
        var userId = User.GetUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        clause.CodeType = dto.CodeType.Trim();
        clause.CodeVersion = dto.CodeVersion.Trim();
        clause.SectionNumber = dto.SectionNumber.Trim();
        clause.Title = dto.Title.Trim();
        clause.RequirementText = dto.RequirementText.Trim();
        clause.SourceUrl = string.IsNullOrWhiteSpace(dto.SourceUrl) ? null : dto.SourceUrl.Trim();
        clause.VerifiedBy = string.IsNullOrWhiteSpace(dto.VerifiedBy) ? user?.Name ?? user?.Email : dto.VerifiedBy.Trim();
        clause.LastVerifiedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var clauses = await _context.SbcClauses
            .OrderBy(c => c.CodeType).ThenBy(c => c.SectionNumber)
            .ToListAsync();
        return Ok(clauses);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SbcClauseDto dto)
    {
        var error = Validate(dto);
        if (error != null) return BadRequest(error);

        var clause = new SbcClause
        {
            CreatedById = User.GetUserId(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        await Apply(clause, dto);
        _context.SbcClauses.Add(clause);
        await _context.SaveChangesAsync();
        return Ok(clause);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SbcClauseDto dto)
    {
        var error = Validate(dto);
        if (error != null) return BadRequest(error);

        var clause = await _context.SbcClauses.FindAsync(id);
        if (clause == null) return NotFound();

        await Apply(clause, dto);
        await _context.SaveChangesAsync();
        return Ok(clause);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var clause = await _context.SbcClauses.FindAsync(id);
        if (clause == null) return NotFound();

        _context.SbcClauses.Remove(clause);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Bulk import (from an Excel/CSV sheet parsed in the browser).
    /// A row with the same code, edition and section updates the existing clause instead of duplicating it.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] List<SbcClauseDto> rows)
    {
        if (rows == null || rows.Count == 0) return BadRequest("The file has no rows.");
        if (rows.Count > 2000) return BadRequest("Import at most 2000 clauses at a time.");

        var existing = await _context.SbcClauses.ToListAsync();
        var errors = new List<string>();
        int created = 0, updated = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var error = Validate(row);
            if (error != null)
            {
                errors.Add($"Row {i + 2}: {error}"); // +2: header row and 1-based numbering
                continue;
            }

            var match = existing.FirstOrDefault(c =>
                string.Equals(c.CodeType, row.CodeType.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.CodeVersion, row.CodeVersion.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.SectionNumber, row.SectionNumber.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                match = new SbcClause
                {
                    CreatedById = User.GetUserId(),
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                _context.SbcClauses.Add(match);
                existing.Add(match);
                created++;
            }
            else
            {
                updated++;
            }
            await Apply(match, row);
        }

        await _context.SaveChangesAsync();
        return Ok(new { created, updated, errors });
    }
}
