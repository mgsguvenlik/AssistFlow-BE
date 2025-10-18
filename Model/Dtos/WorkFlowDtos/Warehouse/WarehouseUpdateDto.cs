using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class WarehouseUpdateDto
    {
        [Required]
        public long Id { get; set; }
        [Required, StringLength(100)]
        public string RequestNo { get; set; } = string.Empty;
        [Required]
        public DateTimeOffset DeliveryDate { get; set; }
        public string? Description { get; set; }
        public bool IsSended { get; set; }
        public List<long>? ProductIds { get; set; }
    }
}
