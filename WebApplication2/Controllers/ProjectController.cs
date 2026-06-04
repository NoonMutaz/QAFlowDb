using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Models.DTO;

namespace WebApplication2.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProjectsController(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Scaffolding & Helpers

    private int GetUserId()
    {
        // Consolidating to the most common standard OIDC/JWT claim types
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

        // 1. Normalized check for existing project name
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

        // 2. Database Transaction to guarantee both actions succeed or fail together
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
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");

        if (!isOwner) return Forbid();

        var project = await _context.Projects.FindAsync(id);
        if (project == null) return NotFound();

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }

    #endregion

    #region Invites & Membership

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
            // Clean up old declined invite seamlessly
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
        return Ok();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetProjectMembers(int id)
    {
        var userId = GetUserId();
        
        // FIXED: Check if user is associated with the project at all (Owner, Member, or Viewer)
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

        membership.Role = dto.Role;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Role updated successfully",
            memberId,
            newRole = dto.Role
        });
    }

    #endregion
}