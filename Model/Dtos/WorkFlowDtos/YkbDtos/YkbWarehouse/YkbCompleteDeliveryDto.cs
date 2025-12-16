using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbWarehouse
{
    public class YkbCompleteDeliveryDto
    {
        [Required]
        public required string RequestNo { get; set; }
        public DateTime DeliveryDate { get; set; } = DateTime.Now;
        public string Description { get; set; } = string.Empty;
        public List<ServicesRequestProductCreateDto> DeliveredProducts { get; set; } = new();
    }
}
