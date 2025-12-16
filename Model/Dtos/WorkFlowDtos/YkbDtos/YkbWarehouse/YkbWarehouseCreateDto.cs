using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbWarehouse
{
    public class YkbWarehouseCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public DateTimeOffset DeliveryDate { get; set; }
        public string? Description { get; set; }
        public WarehouseStatus WarehouseStatus { get; set; }
    }

}
