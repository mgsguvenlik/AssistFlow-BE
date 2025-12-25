namespace Model.Dtos.User
{
    public class UpdateUserPasswordDto
    {
        public long UserId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
}
