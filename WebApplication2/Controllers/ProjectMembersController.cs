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
public class ProjectMembersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ActivityLogger _logger;

    public ProjectMembersController(ApplicationDbContext context, ActivityLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("{id}/invite")]
    public async Task<IActionResult> InviteUser(int id, [FromBody] InviteDto dto)
    {
        var userId = User.GetUserId();
        var isOwner = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");
        if (!isOwner) return Forbid();

        var invitedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (invitedUser == null) return NotFound("User not found");

        var existingMembership = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == invitedUser.Id);
        if (existingMembership != null)
        {
            if (existingMembership.Status != "declined") return BadRequest("User already has active membership or pending invite");
            _context.ProjectMembers.Remove(existingMembership);
        }

        var newMembership = new ProjectMember { ProjectId = id, UserId = invitedUser.Id, Role = "member", Status = "pending" };
        _context.ProjectMembers.Add(newMembership);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(id, userId, "InviteMember", "ProjectMember", newMembership.Id, $"Sent pending project invitation context to {dto.Email}");
        return Ok(new { message = "Invite sent successfully", memberId = newMembership.Id });
    }

    [HttpGet("invites")]
    public async Task<IActionResult> GetInvites()
    {
        var userId = User.GetUserId();
        var invites = await _context.ProjectMembers
            .Where(m => m.UserId == userId && m.Status == "pending")
            .Select(m => new
            {
                m.Id,
                m.Role,
                Project = new { m.Project.Id, m.Project.Name, m.Project.Description },
                Sender = m.Project.Members.Where(pm => pm.Role == "owner" && pm.Status == "accepted")
                    .OrderByDescending(pm => pm.Id)
                    .Select(pm => new { Name = pm.User.Name ?? pm.User.Email, Email = pm.User.Email })
                    .FirstOrDefault() ?? new { Name = "Project Owner", Email = "owner@project.com" }
            }).ToListAsync();

        return Ok(invites);
    }

    [HttpPost("invites/{id}/accept")]
    public async Task<IActionResult> AcceptInvite(int id)
    {
        var userId = User.GetUserId();
        var invite = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.Status == "pending");
        if (invite == null) return NotFound();

        invite.Status = "accepted";
        await _context.SaveChangesAsync();

        await _logger.LogAsync(invite.ProjectId, userId, "AcceptInvite", "ProjectMember", invite.Id, "Accepted invitation request parameters.");
        return Ok();
    }

    [HttpPost("invites/{id}/decline")]
    public async Task<IActionResult> DeclineInvite(int id)
    {
        var userId = User.GetUserId();
        var invite = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.Status == "pending");
        if (invite == null) return NotFound();

        invite.Status = "declined";
        await _context.SaveChangesAsync();

        await _logger.LogAsync(invite.ProjectId, userId, "DeclineInvite", "ProjectMember", invite.Id, "Declined entry request criteria.");
        return Ok();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetProjectMembers(int id)
    {
        var userId = User.GetUserId();
        var hasAccess = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Status == "accepted");
        if (!hasAccess) return Forbid();

        var members = await _context.ProjectMembers
            .Where(m => m.ProjectId == id && m.Status == "accepted")
            .Select(m => new { m.Id, m.UserId, Email = m.User.Email, Name = m.User.Name ?? m.User.Email, m.Role })
            .OrderByDescending(m => m.Role == "owner")
            .ThenBy(m => m.Name).ToListAsync();

        return Ok(members);
    }

    [HttpDelete("{id}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        var userId = User.GetUserId();
        if (memberId == userId) return BadRequest("Cannot remove yourself");

        var isOwner = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");
        if (!isOwner) return Forbid("Only owners can remove members");

        var membership = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == memberId && m.Status == "accepted");
        if (membership == null) return NotFound("Member not found");

        _context.ProjectMembers.Remove(membership);
        await _context.SaveChangesAsync();

        await _logger.LogAsync(id, userId, "RemoveMember", "ProjectMember", membership.Id, $"Removed user identity id {memberId} from workspace access privileges.");
        return Ok(new { message = "Member removed successfully" });
    }

    [HttpPatch("{id}/members/{memberId}")]
    public async Task<IActionResult> UpdateMemberRole(int id, int memberId, [FromBody] UpdateMemberRoleDto dto)
    {
        var userId = User.GetUserId();
        if (memberId == userId) return BadRequest("Cannot change your own role");

        var isOwner = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == userId && m.Role == "owner");
        if (!isOwner) return Forbid("Only owners can update roles");
        if (dto.Role != "owner" && dto.Role != "member" && dto.Role != "viewer") return BadRequest("Invalid role");

        var membership = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == memberId && m.Status == "accepted");
        if (membership == null) return NotFound("Member not found");

        var oldRole = membership.Role;
        membership.Role = dto.Role;
        await _context.SaveChangesAsync();

        await _logger.LogAsync(id, userId, "UpdateMemberRole", "ProjectMember", membership.Id, $"Changed user {memberId} permission tier tracking from '{oldRole}' to '{dto.Role}'");
        return Ok(new { message = "Role updated successfully", memberId, newRole = dto.Role });
    }
}