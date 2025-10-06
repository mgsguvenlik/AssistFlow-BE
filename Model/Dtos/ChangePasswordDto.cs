namespace Model.Dtos
{
    public class ChangePasswordDto
    {
        public required string RecoveryCode { get; set; }
        public required string NewPassword { get; set; }
        public required string NewPasswordConfirm { get; set; }
    }
}
