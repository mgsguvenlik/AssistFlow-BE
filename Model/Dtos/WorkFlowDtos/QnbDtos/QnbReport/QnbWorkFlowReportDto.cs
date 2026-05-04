namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbReport
{
    // KÖK DTO
    public class QnbWorkFlowReportDto
    {
        public string RequestNo { get; set; } = string.Empty;

        public HeaderSectionDto Header { get; set; } = new();
        public CustomerSectionDto Customer { get; set; } = new();
        public ServiceRequestSectionDto ServiceRequest { get; set; } = new();
        public TechnicalServiceSectionDto? TechnicalService { get; set; }
        public WarehouseSectionDto? Warehouse { get; set; }
        public PricingSectionDto? Pricing { get; set; }
        public FinalApprovalSectionDto? FinalApproval { get; set; }

        public List<ProductLineDto> Products { get; set; } = new();
        public List<ReviewLogDto> ReviewLogs { get; set; } = new();

        public string Currency { get; set; } = "TRY";
        public decimal Subtotal { get; set; }
        public decimal? DiscountTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public string? ArchiveChecksum { get; set; }
        public int? ArchiveVersion { get; set; }
    }

    public class HeaderSectionDto
    {
        public string Title { get; set; } = "Servis Talebi";
        public string WorkFlowStatus { get; set; } = "Pending";
        public long? CurrentStepId { get; set; }
        public string? CurrentStepCode { get; set; }
        public bool? IsAgreement { get; set; }
        public bool IsLocationValid { get; set; }
        public string? CustomerApproverName { get; set; }

        public long? ApproverTechnicianId { get; set; }
        public string? ApproverTechnicianName { get; set; }
        public string? ApproverTechnicianEmail { get; set; }
        public string? ApproverTechnicianCode { get; set; }
        public int Priority { get; set; }
    }

    public class CustomerSectionDto
    {
        public long Id { get; set; }
        public string? SubscriberCode { get; set; }
        public string? SubscriberCompany { get; set; }
        public string? SubscriberAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? LocationCode { get; set; }
        public string? ContactName1 { get; set; }
        public string? Phone1 { get; set; }
        public string? Email1 { get; set; }
        public string? CustomerShortCode { get; set; }
        public string? CorporateLocationId { get; set; }
        public string? Longitude { get; set; }
        public string? Latitude { get; set; }
        public DateTimeOffset? InstallationDate { get; set; }
        public int? WarrantyYears { get; set; }

        public CustomerGroupLiteDto? CustomerGroup { get; set; }
    }

    public class CustomerGroupLiteDto
    {
        public long Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public long? ParentGroupId { get; set; }

        public List<ProgressApproverLiteDto> ProgressApprovers { get; set; } = new();
    }

    public class ProgressApproverLiteDto
    {
        public long Id { get; set; }
        public string? StepCode { get; set; }
        public int OrderNo { get; set; }
        public long? ApproverUserId { get; set; }
        public string? ApproverUserName { get; set; }
        public bool IsActive { get; set; }
    }

    public class ServiceRequestSectionDto
    {
        public long Id { get; set; }
        public string? OracleNo { get; set; }
        public DateTimeOffset ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }
        public string ServicesCostStatus { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsProductRequirement { get; set; }
        public long? WorkFlowStepId { get; set; }
        public long? CustomerApproverId { get; set; }
        public long? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }
        public string Priority { get; set; } = "Normal";
        public string ServicesRequestStatus { get; set; } = string.Empty;
    }

    public class TechnicalServiceSectionDto
    {
        public long Id { get; set; }
        public long? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public bool IsLocationCheckRequired { get; set; }
        public string ServicesStatus { get; set; } = string.Empty;
        public string ServicesCostStatus { get; set; } = string.Empty;

        public List<ImageDto> ServiceImages { get; set; } = new();
        public List<ImageDto> FormImages { get; set; } = new();
    }

    public class ImageDto
    {
        public long Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }

    public class WarehouseSectionDto
    {
        public long Id { get; set; }
        public DateTimeOffset DeliveryDate { get; set; }
        public string? Description { get; set; }
        public string WarehouseStatus { get; set; } = string.Empty;
    }

    public class PricingSectionDto
    {
        public long Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Currency { get; set; } = "TRY";
        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class FinalApprovalSectionDto
    {
        public long Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public long? DecidedBy { get; set; }
        public string? DecidedByUserName { get; set; }
    }

    public class ProductLineDto
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }

        public int Quantity { get; set; }

        public bool IsPriceCaptured { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "TRY";
        public decimal LineTotal { get; set; }

        public string PriceSource { get; set; } = "Standard";
    }

    public class ReviewLogDto
    {
        public long Id { get; set; }
        public long? FromStepId { get; set; }
        public string FromStepCode { get; set; } = string.Empty;
        public long? ToStepId { get; set; }
        public string ToStepCode { get; set; } = string.Empty;
        public string ReviewNotes { get; set; } = string.Empty;
        public long CreatedUser { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}