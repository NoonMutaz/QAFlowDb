using System.Text.Json.Serialization; //  
using System.Collections.Generic;

namespace WebApplication2.Models
{
    public class TestCase
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public List<string> Steps { get; set; } = new List<string>();

        public string ExpectedResult { get; set; } = "";

        public int ProjectId { get; set; }

        [JsonIgnore] //   Prevents fetching the project parent data infinitely
        public Project Project { get; set; } = null!;

        [JsonIgnore] //   Breaks the infinite loop (Bug -> TestCase -> LinkedBugs -> Bug)
        public ICollection<Bug> LinkedBugs { get; set; } = new List<Bug>();
    }
}