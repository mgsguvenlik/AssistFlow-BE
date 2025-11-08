using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;

namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class WarehouseGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public DateTimeOffset DeliveryDate { get; set; }
        public string? Description { get; set; } // Warehouse.Description
        public WarehouseStatus WarehouseStatus { get; set; }

        // Yeni alanlar (JOIN ile gelecek)
        public string? WorkFlowRequestTitle { get; set; }        // WorkFlow.RequestTitle
        public WorkFlowPriority WorkFlowPriority { get; set; }   // WorkFlow.Priority
        public string? ServicesRequestDescription { get; set; }  // ServicesRequest.Description


        // Ekranlar için yalnızca ürün Id listesi
        public List<ServicesRequestProductGetDto> WarehouseProducts { get; set; } = new();
        public List<WorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
    }

}
