using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequest;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbWarehouse
{
    public class EkbWarehouseGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public DateTimeOffset DeliveryDate { get; set; }
        public string? Description { get; set; }
        public WarehouseStatus WarehouseStatus { get; set; }


        // Yeni alanlar (JOIN ile gelecek)
        public string? WorkFlowRequestTitle { get; set; }        // WorkFlow.RequestTitle
        public WorkFlowPriority WorkFlowPriority { get; set; }   // WorkFlow.Priority
        //public string? ServicesRequestDescription { get; set; }  // ServicesRequest.Description


        // Ekranlar için yalnızca ürün Id listesi
        public List<EkbServicesRequestProductGetDto> WarehouseProducts { get; set; } = new();
        public List<EkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
        public UserGetDto? User { get; set; }
        public UserGetDto? CreatedUser { get; set; }
        public EkbServicesRequestGetDto?  ServicesRequest { get; set; }
    }
}
