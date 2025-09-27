namespace Model.Dtos.Auth
{
    public class LoginRequestDto
    {
        public string Identifier { get; set; } = string.Empty; // email veya TechnicianCode
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = true;
    }
}
