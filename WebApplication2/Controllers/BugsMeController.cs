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
}