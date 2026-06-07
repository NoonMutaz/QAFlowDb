using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication2.Data;
using WebApplication2.DTOs;
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

    #region Scaffolding & Helpers

    private int GetUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");

        if (string.IsNullOrEmpty(claimValue) || !int.TryParse(claimValue, out int userId))
        {
            throw new UnauthorizedAccessException("User identification token is invalid or missing.");
        }

        return userId;
    }

    #endregion

    #region Project CRUD

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetUserId();

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
        var userId = GetUserId();

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
        var userId = GetUserId();

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

            // LOGGING: Project Created
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
        var userId = GetUserId();

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

        // LOGGING: Project Metadata Updates
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
        var userId = GetUserId();

        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id &&
                           m.UserId == userId &&
                           m.Role == "owner");

        if (!isOwner)
            return Forbid();

        var project = await _context.Projects.FindAsync(id);

        if (project == null)
            return NotFound();

        // 🟢 FIXED: Removed the duplicated log registration statement block
        await _logger.LogAsync(
            id,
            userId,
            "Delete",
            "Project",
            id,
            $"Deleted project: {project.Name}");

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return NoContent();
    }
    #endregion

    #region Invites, Membership & Analytics

    [HttpPost("{id}/invite")]
    public async Task<IActionResult> InviteUser(int id, [FromBody] InviteDto dto)
    {
        var userId = GetUserId();

        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");
        if (!isOwner) return Forbid();

        var invitedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (invitedUser == null) return NotFound("User not found");

        var existingMembership = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == invitedUser.Id);

        if (existingMembership != null)
        {
            if (existingMembership.Status != "declined")
            {
                return BadRequest("User already has active membership or pending invite");
            }
            _context.ProjectMembers.Remove(existingMembership);
        }

        var newMembership = new ProjectMember
        {
            ProjectId = id,
            UserId = invitedUser.Id,
            Role = "member",
            Status = "pending"
        };

        _context.ProjectMembers.Add(newMembership);
        await _context.SaveChangesAsync();

        // LOGGING: User Invited
        await _logger.LogAsync(id, userId, "InviteMember", "ProjectMember", newMembership.Id, $"Sent pending project invitation context to {dto.Email}");

        return Ok(new { message = "Invite sent successfully", memberId = newMembership.Id });
    }

    [HttpGet("invites")]
    public async Task<IActionResult> GetInvites()
    {
        var userId = GetUserId();

        var invites = await _context.ProjectMembers
            .Where(m => m.UserId == userId && m.Status == "pending")
            .Select(m => new
            {
                m.Id,
                m.Role,
                Project = new
                {
                    m.Project.Id,
                    m.Project.Name,
                    m.Project.Description,
                },
                Sender = m.Project.Members
                    .Where(pm => pm.Role == "owner" && pm.Status == "accepted")
                    .OrderByDescending(pm => pm.Id)
                    .Select(pm => new
                    {
                        Name = pm.User.Name ?? pm.User.Email,
                        Email = pm.User.Email
                    })
                    .FirstOrDefault() ?? new { Name = "Project Owner", Email = "owner@project.com" }
            })
            .ToListAsync();

        return Ok(invites);
    }

    [HttpPost("invites/{id}/accept")]
    public async Task<IActionResult> AcceptInvite(int id)
    {
        var userId = GetUserId();
        var invite = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.Status == "pending");

        if (invite == null) return NotFound();

        invite.Status = "accepted";
        await _context.SaveChangesAsync();

        // LOGGING: User Accepted Invitation
        await _logger.LogAsync(invite.ProjectId, userId, "AcceptInvite", "ProjectMember", invite.Id, "Accepted invitation request parameters.");

        return Ok();
    }

    [HttpPost("invites/{id}/decline")]
    public async Task<IActionResult> DeclineInvite(int id)
    {
        var userId = GetUserId();
        var invite = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.Status == "pending");

        if (invite == null) return NotFound();

        invite.Status = "declined";
        await _context.SaveChangesAsync();

        // LOGGING: User Declined Invitation
        await _logger.LogAsync(invite.ProjectId, userId, "DeclineInvite", "ProjectMember", invite.Id, "Declined entry request criteria.");

        return Ok();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetProjectMembers(int id)
    {
        var userId = GetUserId();

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Status == "accepted");

        if (!hasAccess) return Forbid();

        var members = await _context.ProjectMembers
            .Where(m => m.ProjectId == id && m.Status == "accepted")
            .Select(m => new
            {
                m.Id,
                m.UserId,
                Email = m.User.Email,
                Name = m.User.Name ?? m.User.Email,
                m.Role,
            })
            .OrderByDescending(m => m.Role == "owner")
            .ThenBy(m => m.Name)
            .ToListAsync();

        return Ok(members);
    }

    [HttpDelete("{id}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        var userId = GetUserId();
        if (memberId == userId) return BadRequest("Cannot remove yourself");

        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");

        if (!isOwner) return Forbid("Only owners can remove members");

        var membership = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == memberId && m.Status == "accepted");

        if (membership == null) return NotFound("Member not found");

        _context.ProjectMembers.Remove(membership);
        await _context.SaveChangesAsync();

        // LOGGING: Member Kicked/Removed
        await _logger.LogAsync(id, userId, "RemoveMember", "ProjectMember", membership.Id, $"Removed user identity id {memberId} from workspace access privileges.");

        return Ok(new { message = "Member removed successfully" });
    }

    [HttpPatch("{id}/members/{memberId}")]
    public async Task<IActionResult> UpdateMemberRole(int id, int memberId, [FromBody] UpdateMemberRoleDto dto)
    {
        var userId = GetUserId();
        if (memberId == userId) return BadRequest("Cannot change your own role");

        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");

        if (!isOwner) return Forbid("Only owners can update roles");

        if (dto.Role != "owner" && dto.Role != "member" && dto.Role != "viewer")
            return BadRequest("Invalid role");

        var membership = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == memberId && m.Status == "accepted");

        if (membership == null) return NotFound("Member not found");

        var oldRole = membership.Role;
        membership.Role = dto.Role;
        await _context.SaveChangesAsync();

        // LOGGING: Member Role Context Mutations
        await _logger.LogAsync(id, userId, "UpdateMemberRole", "ProjectMember", membership.Id, $"Changed user {memberId} permission tier tracking from '{oldRole}' to '{dto.Role}'");

        return Ok(new
        {
            message = "Role updated successfully",
            memberId,
            newRole = dto.Role
        });
    }

    [HttpGet("{id}/activity")]
    public async Task<IActionResult> GetProjectActivity(int id)
    {
        var userId = GetUserId();

        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id &&
                           m.UserId == userId &&
                           m.Status == "accepted" &&
                           m.Role == "owner");

        if (!isOwner) return Forbid();

        var activities = await _context.ActivityLogs
            .Where(a => a.ProjectId == id)
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new
            {
                a.Id,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.CreatedAt,
                User = new
                {
                    Name = a.User.Name ?? a.User.Email,
                    Email = a.User.Email
                }
            })
            .ToListAsync();

        return Ok(activities);
    }

    [HttpGet("{id}/analytics")]
    public async Task<ActionResult<ProjectAnalyticsDto>> GetProjectAnalytics(int id)
    {
        var userId = GetUserId();

        // Check project access
        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m =>
                m.ProjectId == id &&
                m.UserId == userId &&
                m.Status == "accepted");

        if (!hasAccess)
            return Forbid();

        // Check project exists
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == id);

        if (!projectExists)
            return NotFound();

        // Basic statistics
        var openBugs = await _context.Bugs
            .CountAsync(b => b.ProjectId == id && b.Status == "notFixed");

        var fixedBugs = await _context.Bugs
            .CountAsync(b => b.ProjectId == id && b.Status == "Fixed");

        var criticalBugs = await _context.Bugs
            .CountAsync(b =>
                b.ProjectId == id &&
                b.Priority == "High" &&
                b.Status != "Fixed");

        // Top developer (most fixed bugs)
        var topContributor = await _context.Bugs
            .Where(b =>
                b.ProjectId == id &&
                b.Status == "Fixed" &&
                b.AssignedById != null)
            .GroupBy(b => new
            {
                b.AssignedById,
                b.AssignedByName
            })
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key.AssignedByName)
            .FirstOrDefaultAsync() ?? "None";

        // Most active tester (most created bugs)
        var mostActiveTester = await _context.Bugs
            .Where(b =>
                b.ProjectId == id &&
                b.CreatedById != null)
            .GroupBy(b => new
            {
                b.CreatedById,
                b.CreatedByName
            })
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key.CreatedByName)
            .FirstOrDefaultAsync() ?? "None";

        return Ok(new ProjectAnalyticsDto
        {
            OpenBugs = openBugs,
            FixedBugs = fixedBugs,
            CriticalBugs = criticalBugs,
            TopContributor = topContributor,
            MostActiveTester = mostActiveTester
        });
    }

    [HttpGet("api/projects/{projectId}/testcases/{id}")]
    public async Task<IActionResult> GetTestCaseDetails(int id)
    {
        var testCase = await _context.TestCases
            .Include(t => t.LinkedBugs)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (testCase == null) return NotFound();
        return Ok(testCase);
    }
    #endregion
}