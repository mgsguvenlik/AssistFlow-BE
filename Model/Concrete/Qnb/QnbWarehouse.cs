using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbWarehouse", Schema = "qnb")]
    public class QnbWarehouse : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset DeliveryDate { get; set; }

        public string? Description { get; set; }

        public WarehouseStatus WarehouseStatus { get; set; }
    }
}