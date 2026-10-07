using Model.Dtos.Menu;
using Model.Dtos.User;

namespace Model.Dtos.Auth
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public bool RequiresPasswordChange { get; set; }
        public string? PasswordChangeToken { get; set; }
        public DateTimeOffset? PasswordChangeTokenExpires { get; set; }
        public string? PasswordChangeReason { get; set; }
        public DateTime Expires { get; set; }
        public required UserGetDto User { get; set; }
        public int Status { get; set; } = 200;
        public List<MenuWithPermissionsDto> Menus { get; set; } = new();
    }
}
