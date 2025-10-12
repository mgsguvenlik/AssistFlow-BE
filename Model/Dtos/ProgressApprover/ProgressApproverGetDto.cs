using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.ProgressApprover
{
    public class ProgressApproverGetDto
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public long CustomerId { get; set; }

        // Görsellik/kolaylık //MZK:  Mapping ile doldurulacak
        public string? CustomerName { get; set; }   // örn: SubscriberCompany
        public string? CustomerCode { get; set; }   // örn: SubscriberCode

        public string Phone { get; set; } = string.Empty;
    }
}
