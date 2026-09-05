using Core.Enums;
using Model.Concrete.WorkFlows;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using Model.Dtos.WorkOrderType;

namespace Model.Dtos.WorkFlowDtos.ServicesRequest
{
    public class  ServicesRequestGetDto
    {
        public long Id { get; set; }

        public string RequestNo { get; set; } = string.Empty;
        public string? OracleNo { get; set; }

        public DateTimeOffset ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }

        public ServicesCostStatus ServicesCostStatus { get; set; }
        public string ServicesCostStatusText => ServicesCostStatus.ToString();
        public string? Title { get; set; }
        public string? Description { get; set; }

        /// <summary>Ürün/Parça ihtiyacı var mı?</summary>
        public bool IsProductRequirement { get; set; }   // <-- yeni

        public bool IsMailSended { get; set; }
        public bool IsLocationValid { get; set; }
        public long? CustomerApproverId { get; set; }
        public string? CustomerApproverName { get; set; }

        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public long ServiceTypeId { get; set; }
        public List<global::Model.Dtos.ServiceType.ServiceTypeGetDto> ServiceTypes { get; set; } = new();
        public List<long> ServiceTypeIds => ServiceTypes.Select(x => x.Id).ToList();
        private string? serviceTypeName;
        public string? ServiceTypeName
        {
            get => ServiceTypes.Count > 0 ? string.Join(", ", ServiceTypes.Select(x => x.Name)) : serviceTypeName;
            set => serviceTypeName = value;
        }
        public string? WorkFlowStepName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }
        public bool IsDeleted { get; set; }
        public long? ApproverTechnicianId { get; set; }

        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public ServicesRequestStatus ServicesRequestStatus { get; set; }
     
        public List<ServicesRequestProductGetDto> ServicesRequestProducts { get; set; } = new();
        public List<WorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
        public List<long>? WorkOrderTypeIds { get; set; }
        public List<WorkOrderTypeGetDto> WorkOrderTypes { get; set; } = new();
    }
}
