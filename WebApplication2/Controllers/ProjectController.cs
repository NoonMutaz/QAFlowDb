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
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ActivityLogger _logger;

    public ProjectsController(ApplicationDbContext context, ActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = User.GetUserId();

        var projects = await _context.ProjectMembers
            .Where(m => m.UserId == userId && m.Status == "accepted")
            .Select(m => new ProjectDto
            {
                Id = m.Project.Id,
                Name = m.Project.Name,
                Description = m.Project.Description,
                Role = m.Role,
                Type = m.Project.Type
            })
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(int id)
    {
        var userId = User.GetUserId();

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Status == "accepted");

        if (!hasAccess) return Forbid();

        var project = await _context.Projects
            .Where(p => p.Id == id)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type
            })
            .FirstOrDefaultAsync();

        return project == null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
    {
        var userId = User.GetUserId();
        var normalizedName = dto.Name.Trim().ToLower();

        var projectExists = await _context.Projects.AnyAsync(p => p.Name.ToLower() == normalizedName);
        if (projectExists)
        {
            return Conflict(new { message = "Project name already exists" });
        }

        if (!await _context.Users.AnyAsync(u => u.Id == userId))
        {
            return BadRequest($"User {userId} not found");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var project = new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = userId,
                Role = "owner",
                Status = "accepted"
            });
            await _context.SaveChangesAsync();

            await _logger.LogAsync(project.Id, userId, "Create", "Project", project.Id, $"Created project {project.Name}");
            await transaction.CommitAsync();

            return Ok(new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                Type = project.Type,
                Role = "owner"
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            return Problem("An error occurred while creating the project.");
        }
    }

    [HttpGet("check-name")]
    public async Task<IActionResult> CheckName([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Name query parameter is required" });
        }

        var exists = await _context.Projects
            .AnyAsync(p => p.Name.ToLower() == name.Trim().ToLower());

        return Ok(new { exists });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectDto dto)
    {
        var userId = User.GetUserId();
        var normalizedName = dto.Name.Trim().ToLower();

        var exists = await _context.Projects.AnyAsync(p =>
            p.Id != id && p.Name.ToLower() == normalizedName);

        if (exists)
        {
            return Conflict(new { message = "Project name already exists" });
        }

        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");

        if (!isOwner)
            return Problem("Only project owners can edit", statusCode: 403);

        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound("Project not found");

        project.Name = dto.Name;
        project.Description = dto.Description;
        project.Type = dto.Type;

        await _context.SaveChangesAsync();
        await _logger.LogAsync(id, userId, "Update", "Project", id, $"Updated metadata configurations for project {project.Name}");

        return Ok(new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Type = project.Type,
            Role = "owner"
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var userId = User.GetUserId();
        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");

        if (!isOwner) return Forbid();

        var project = await _context.Projects.FindAsync(id);
        if (project == null) return NotFound();

        await _logger.LogAsync(id, userId, "Delete", "Project", id, $"Deleted project: {project.Name}");

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}