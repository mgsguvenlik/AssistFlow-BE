namespace Model.Dtos.ProgressApprover
{
    public class ProgressApproverCreateDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public long CustomerId { get; set; }
    }
}
