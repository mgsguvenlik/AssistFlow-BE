using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.ServicesRequest
{
    public class ServicesRequestGetDto
    {
        public long Id { get; set; }

        public string RequestNo { get; set; } = string.Empty;
        public string? OracleNo { get; set; }

        public DateTimeOffset ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }

        public ServicesCostStatus ServicesCostStatus { get; set; }
        public string ServicesCostStatusText => ServicesCostStatus.ToString();

        public string? Description { get; set; }

        /// <summary>Ürün/Parça ihtiyacı var mı?</summary>
        public bool IsProductRequirement { get; set; }   // <-- yeni

        public bool IsSended { get; set; }
        public long? SendedStatusId { get; set; }
        public bool IsReview { get; set; }
        public bool IsMailSended { get; set; }

        public long? CustomerApproverId { get; set; }
        public string? CustomerApproverName { get; set; }

        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public long ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public long? WorkFlowStatusId => SendedStatusId;
        public string? WorkFlowStatusName { get; set; }

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }
        public bool IsDeleted { get; set; }
        public List<long> ProductIds { get; set; } = new();
    }
}
