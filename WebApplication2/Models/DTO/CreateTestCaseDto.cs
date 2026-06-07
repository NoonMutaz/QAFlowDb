namespace WebApplication2.Models.DTO
{
    public class CreateTestCaseDto
    {
        public string Title { get; set; }

        public List<string> Steps { get; set; } = new();

        public string ExpectedResult { get; set; }
    }
}
