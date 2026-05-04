using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.DTO
{
    // DTO used for sending project data to the client
    public class ProjectDto
    {
        public int Id { get; set; }               // Project ID
        public string Name { get; set; } = "";    // Project name
        public string Description { get; set; } = ""; // Project description
        public string Type { get; set; } = "";
        public string Role { get; set; } = "member";
        // Project type (QA Dashboard, Bug Tracking, etc.)
    }

    // DTO used when creating a new project
    public class CreateProjectDto
    {
        [Required]
        [MinLength(3)]
        public string Name { get; set; } = "";

        [MaxLength(100)]
        public string Description { get; set; } = "";

        [Required]
        public string Type { get; set; } = "";
    }

    // DTO used when updating an existing project
    public class UpdateProjectDto
    {
        [Required]
        [MinLength(3)]
        public string Name { get; set; } = "";

        [MaxLength(100)]
        public string Description { get; set; } = "";

        [Required]
        public string Type { get; set; } = "";

       
    }

    //  ADD: DTO for member role updates
    public class UpdateMemberRoleDto
    {
        public string Role { get; set; } = "viewer";
    }
}