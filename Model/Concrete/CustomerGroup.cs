using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class CustomerGroup : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(200)]
        public string GroupName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Üst grup ID’si (self-referencing)
        /// </summary>
        public long? ParentGroupId { get; set; }
        public CustomerGroup? ParentGroup { get; set; }

        /// <summary>
        /// Bu grubun alt grupları
        /// </summary>
        public ICollection<CustomerGroup> SubGroups { get; set; } = new List<CustomerGroup>();

        // Navigations (grup fiyatları)
        public ICollection<CustomerGroupProductPrice> GroupProductPrices { get; set; } = new List<CustomerGroupProductPrice>();

        public ICollection<ProgressApprover> ProgressApprovers { get; set; } = new List<ProgressApprover>();
    }
}
