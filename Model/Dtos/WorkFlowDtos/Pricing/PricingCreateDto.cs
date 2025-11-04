using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.Pricing
{
    public class PricingCreateDto
    {
        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        [MaxLength(3)]
        public string Currency { get; set; } = "TRY";

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // TotalAmount’ı server tarafında hesaplıyorsan göndermeyebilirsin.
        // Gönderilecekse negatif olmasın:
        [Range(0, (double)decimal.MaxValue)]
        public decimal TotalAmount { get; set; } = 0m;

        // İstersen dışarıdan status kabul etme; default Pending kalsın.
        // public PricingStatus Status { get; set; } = PricingStatus.Pending;
        public List<ServicesRequestProductCreateDto>? Products { get; set; }
    }
}
