namespace WebApplication2.Models.DTO
{
    public class AuthDtos
    {
        public class RegisterDto
        {
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class LoginDto
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class AuthResponseDto
        {
            public string Token { get; set; } = "";
            public UserDto User { get; set; } = new();
        }

        public class UserDto
        {
            public int Id { get; set; }
            public string Email { get; set; } = "";
            public string Name { get; set; } = "";
        }

        public class UpdateProfileDto
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }


    }
}
