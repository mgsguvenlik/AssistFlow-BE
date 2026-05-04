using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequest;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbWarehouse
{
    public class QnbWarehouseGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public DateTimeOffset DeliveryDate { get; set; }
        public string? Description { get; set; }
        public WarehouseStatus WarehouseStatus { get; set; }

        // JOIN ile gelecek alanlar
        public string? WorkFlowRequestTitle { get; set; }
        public WorkFlowPriority WorkFlowPriority { get; set; }

        public List<QnbServicesRequestProductGetDto> WarehouseProducts { get; set; } = new();
        public List<QnbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
        public UserGetDto? User { get; set; }
        public UserGetDto? CreatedUser { get; set; }
        public QnbServicesRequestGetDto? ServicesRequest { get; set; }
    }
}