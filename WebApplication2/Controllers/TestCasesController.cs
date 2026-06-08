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
        var userId = User.GetUserId();
        var testCase = new TestCase
        {
            Title = dto.Title,
            Steps = dto.Steps,
            ExpectedResult = dto.ExpectedResult,
            ProjectId = projectId
        };

        _context.TestCases.Add(testCase);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(projectId, userId, "Create", "TestCase", testCase.Id, $"Created test case: {testCase.Title}");
        return Ok(testCase);
    }
}