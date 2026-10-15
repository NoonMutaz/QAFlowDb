using System.Text.Json.Serialization;

namespace WebApplication2.Models
{
    /// <summary>
    /// A design file of a project (drawing / plan, PDF or image). The AI reads it once on upload and
    /// stores a short design summary that it later compares site findings against.
    /// </summary>
    public class ProjectDocument
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }

        [JsonIgnore]
        public Project Project { get; set; } = null!;

        public string FileName { get; set; } = "";
        public string Url { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long Size { get; set; }

        /// <summary>Design facts an inspector can check on site, written by the AI from the file.</summary>
        public string? AiSummary { get; set; }

        /// <summary>AI comparison of the drawing with the SBC (JSON written by the Next.js AI route).</summary>
        public string? AiReview { get; set; }
        public long? AiReviewedAt { get; set; }

        public int? UploadedById { get; set; }
        public long UploadedAt { get; set; }
    }
}
