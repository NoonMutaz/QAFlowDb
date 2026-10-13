namespace WebApplication2.Models
{
    /// <summary>
    /// A verified Saudi Building Code requirement in the shared clause library.
    /// The AI suggests clauses only from this library and the project's own clauses.
    /// </summary>
    public class SbcClause
    {
        public int Id { get; set; }

        /// <summary>Code document, e.g. "SBC 201".</summary>
        public string CodeType { get; set; } = "";

        /// <summary>Code edition, e.g. "2024".</summary>
        public string CodeVersion { get; set; } = "";

        /// <summary>Section / clause number, e.g. "1010.1.2".</summary>
        public string SectionNumber { get; set; } = "";

        public string Title { get; set; } = "";

        /// <summary>The verified requirement (official text or the engineer's summary of it).</summary>
        public string RequirementText { get; set; } = "";

        public string? SourceUrl { get; set; }

        /// <summary>Engineer who checked the clause against the official code.</summary>
        public string? VerifiedBy { get; set; }
        public long? LastVerifiedAt { get; set; }

        public int? CreatedById { get; set; }
        public long CreatedAt { get; set; }
    }
}
