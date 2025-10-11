using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class CustomerGroup : BaseEntity
    {
        /// <summary>Birincil anahtar (PK).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Grup adı (ör. “Kurumsal”, “Bayi”, “VIP”).</summary>
        [Required, MaxLength(200)]
        public string GroupName { get; set; } = string.Empty;

        /// <summary>Gruba ait kısa kod (örn. CORP, DIST, VIP).</summary>
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        // Navigations (grup fiyatları)
        public ICollection<CustomerGroupProductPrice> GroupProductPrices { get; set; } = new List<CustomerGroupProductPrice>();
    }
}
