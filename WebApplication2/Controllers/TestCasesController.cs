using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Extensions;
using WebApplication2.Models;
using WebApplication2.Models.DTO;

namespace WebApplication2.Controllers;

[Authorize]
[ApiController]
[Route("api/projects")]
public class TestCasesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ActivityLogger _logger;

    public TestCasesController(ApplicationDbContext context, ActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>True when the signed-in user is an accepted member of the project.</summary>
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

    public class UpdateTestCaseStatusDto { public string? Status { get; set; } }

    [HttpGet("{projectId}/testcases")]
    public async Task<IActionResult> GetProjectTestCases(int projectId)
    {
        var testCases = await _context.TestCases
            .Where(tc => tc.ProjectId == projectId)
            .Select(tc => new
            {
                tc.Id,
                tc.Title,
                tc.ExpectedResult,
                tc.Steps,
                tc.Status,
                LinkedBugs = tc.LinkedBugs.Select(b => new { b.Id, b.BugId, b.Status }).ToList()
            }).ToListAsync();

        return Ok(testCases);
    }

    [HttpGet("{projectId}/testcases/{id}")]
    public async Task<IActionResult> GetTestCaseDetails(int projectId, int id)
    {
        var testCase = await _context.TestCases
            .Include(t => t.LinkedBugs)
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);

        return testCase == null ? NotFound("Test case not found in this project") : Ok(testCase);
    }

    [HttpPost("{projectId}/testcases")]
    public async Task<IActionResult> CreateTestCase(int projectId, [FromBody] CreateTestCaseDto dto)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(dto.ExpectedResult)) return BadRequest("The required standard is required.");

        var userId = User.GetUserId();
        var testCase = new TestCase
        {
            Title = dto.Title.Trim(),
            Steps = (dto.Steps ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList(),
            ExpectedResult = dto.ExpectedResult.Trim(),
            ProjectId = projectId
        };

        _context.TestCases.Add(testCase);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, userId, "Create", "TestCase", testCase.Id, $"Created test case: {testCase.Title}");
        return Ok(testCase);
    }

    /// <summary>Pass / fail result for a clause that has no linked finding ("passed", "failed" or null to clear).</summary>
    [HttpPatch("{projectId}/testcases/{id}/status")]
    public async Task<IActionResult> UpdateStatus(int projectId, int id, [FromBody] UpdateTestCaseStatusDto dto)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();
        if (dto.Status is not (null or "passed" or "failed")) return BadRequest("Status must be passed, failed or null.");

        var testCase = await _context.TestCases.FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);
        if (testCase == null) return NotFound();

        testCase.Status = dto.Status;
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, User.GetUserId(), "UpdateStatus", "TestCase", id, $"Marked '{testCase.Title}' as {dto.Status ?? "not checked"}");
        return Ok(new { testCase.Id, testCase.Status });
    }

    /// <summary>Removes a clause from the project; findings linked to it are kept and simply unlinked.</summary>
    [HttpDelete("{projectId}/testcases/{id}")]
    public async Task<IActionResult> DeleteTestCase(int projectId, int id)
    {
        if (!await IsMemberAsync(projectId, editorsOnly: true)) return Forbid();

        var testCase = await _context.TestCases
            .Include(t => t.LinkedBugs)
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);
        if (testCase == null) return NotFound();

        foreach (var bug in testCase.LinkedBugs) bug.TestCaseId = null;
        _context.TestCases.Remove(testCase);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, User.GetUserId(), "Delete", "TestCase", id, $"Deleted test case: {testCase.Title}");
        return NoContent();
    }
}