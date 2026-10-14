using System.Text.Json.Serialization;

namespace WebApplication2.Models
{
    /// <summary>
    /// A stage of the project schedule: a date range and the SBC clauses (project test cases)
    /// the team works on during it.
    /// </summary>
    public class ProjectPhase
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }

        [JsonIgnore]
        public Project Project { get; set; } = null!;

        public string Name { get; set; } = "";
        public string? Description { get; set; }

        /// <summary>Dates as "YYYY-MM-DD" (inclusive).</summary>
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";

        /// <summary>Project test case (SBC clause) ids covered by this phase.</summary>
        public List<int> ClauseIds { get; set; } = new();

        public long CreatedAt { get; set; }

        /// <summary>Set when the owner closes the phase; a phase can close only when all its clauses are done.</summary>
        public long? ClosedAt { get; set; }
        public string? ClosedByName { get; set; }
    }
}
