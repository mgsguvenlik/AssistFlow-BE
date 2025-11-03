using Core.Enums;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;

namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class WarehouseGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public DateTimeOffset DeliveryDate { get; set; }
        public string? Description { get; set; }
        public WarehouseStatus WarehouseStatus { get; set; }

        // Ekranlar için yalnızca ürün Id listesi
        public List<ServicesRequestProductGetDto> WarehouseProducts { get; set; } = new();
        public List<WorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
    }

}
