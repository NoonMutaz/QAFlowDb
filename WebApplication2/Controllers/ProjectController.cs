using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
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

    // DEBUG ENDPOINT , REMOVE IN PRODUCTION
    [HttpGet("debug")]
    [AllowAnonymous]
    public IActionResult Debug()
    {
        var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray();
        try
        {
            var userId = GetUserId();
            return Ok(new
            {
                authenticated = User.Identity?.IsAuthenticated ?? false,
                userId,
                claims = allClaims,
                message = " Auth working!"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                claims = allClaims,
                message = " Auth failed"
            });
        }
    }

    private int GetUserId()
    {
        //Try all possible claim names
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
    public async Task<IActionResult> CreateProject(CreateProjectDto dto)
    {
        var userId = GetUserId();

        var projectExists = await _context.Projects
            .AnyAsync(p => p.Name.ToLower() == dto.Name.Trim().ToLower());

        if (projectExists)
        {
            return Conflict(new
            {
                message = "Project name already exists"
            });
        }
        // Verify user exists
        if (!await _context.Users.AnyAsync(u => u.Id == userId))
            return BadRequest($"User {userId} not found");

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
        return Ok(new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Type = project.Type,
            Role = "owner"
        });
    }
    [HttpGet("check-name")]
    public async Task<IActionResult> CheckName([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Name query parameter is required" });
        }

        // Fix: Query globally matching the exact logic used inside CreateProject
        var exists = await _context.Projects
            .AnyAsync(p => p.Name.ToLower() == name.Trim().ToLower());

        return Ok(new { exists });
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
    [HttpPost("{id}/invite")]
    public async Task<IActionResult> InviteUser(int id, InviteDto dto)
    {
        var userId = GetUserId();

        //  Check owner permission
        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");
        if (!isOwner) return Forbid();

        //   Find invited user
        var invitedUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (invitedUser == null) return NotFound("User not found");

        //  Only block PENDING or ACCEPTED - ALLOW RE-INVITE DECLINED!
        var existingPendingOrAccepted = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id
                && m.UserId == invitedUser.Id
                && m.Status != "declined");  

        if (existingPendingOrAccepted)
            return BadRequest("User already has active membership or pending invite");

        //   Clean up old declined invite (optional but recommended)
        var oldDeclined = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id
                && m.UserId == invitedUser.Id
                && m.Status == "declined");

        if (oldDeclined != null)
        {
            _context.ProjectMembers.Remove(oldDeclined); //  Delete old declined
        }

        //  CREATE NEW INVITE
        var newMembership = new ProjectMember
        {
            ProjectId = id,
            UserId = invitedUser.Id,
            Role = "member",
            Status = "pending" // Fresh pending invite
        };

        _context.ProjectMembers.Add(newMembership);
        await _context.SaveChangesAsync();

        //  Send email notification  
        return Ok(new
        {
            message = "Invite sent successfully",
            memberId = newMembership.Id
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectDto dto)
    {
        var userId = GetUserId();

        // Use Problem() instead of anonymous object
        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner") ||
            await _context.Projects.AnyAsync(p => p.Id == id && p.UserId == userId);

        if (!isOwner)
            return Problem("Only project owners can edit", statusCode: 403);

        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound("Project not found");

        //  ModelState validation
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Update project
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
                Sender = m.Project.Members  // 👈 Uses your Members collection!
                    .Where(pm => pm.Role == "owner" && pm.Status == "accepted")
                    .OrderByDescending(pm => pm.Id)
                    .Select(pm => new
                    {
                        Name = pm.User.Name ?? pm.User.Email,
                        Email = pm.User.Email
                    })
                    .FirstOrDefault() ?? new
                    {
                        Name = "Project Owner",
                        Email = "owner@project.com"
                    }
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
        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner" && m.Role == "member");

        //if (!isOwner) return Forbid("Only owners can view members");

        var members = await _context.ProjectMembers
            .Where(m => m.ProjectId == id && m.Status == "accepted")
            .Select(m => new
            {
                m.Id,
                UserId = m.UserId,
                Email = m.User.Email,
                Name = m.User.Name ?? m.User.Email,
                Role = m.Role,

            })
            .OrderByDescending(m => m.Role == "owner") // Owners first
            .ThenBy(m => m.Name)
            .ToListAsync();

        return Ok(members);
    }

    //   DELETE Member (Owner Only - Can't remove self)
    [HttpDelete("{id}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        var userId = GetUserId();

        // Check if owner
        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");

        if (!isOwner) return Forbid("Only owners can remove members");

        // Can't remove self
        if (memberId == userId) return BadRequest("Cannot remove yourself");

        var membership = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == memberId && m.Status == "accepted");

        if (membership == null) return NotFound("Member not found");

        _context.ProjectMembers.Remove(membership);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Member removed successfully" });
    }

    // UPDATE Member Role (Owner Only)
    [HttpPatch("{id}/members/{memberId}")]
    public async Task<IActionResult> UpdateMemberRole(int id, int memberId, [FromBody] UpdateMemberRoleDto dto)
    {
        var userId = GetUserId();

        // Check if owner
        var isOwner = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");

        if (!isOwner) return Forbid("Only owners can update roles");

        // Can't change own role
        if (memberId == userId) return BadRequest("Cannot change your own role");

        var membership = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == memberId && m.Status == "accepted");

        if (membership == null) return NotFound("Member not found");

        // Validate role
        if (dto.Role != "owner" && dto.Role != "member" && dto.Role != "viewer")
        return BadRequest("Invalid role");
        membership.Role = dto.Role;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Role updated successfully",
            memberId,
            newRole = dto.Role
        });
    }

    public class InviteDto
    {
        public string Email { get; set; } = "";
    }
}