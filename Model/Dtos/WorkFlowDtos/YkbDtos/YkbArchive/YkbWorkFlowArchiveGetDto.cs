namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive
{
    public class YkbWorkFlowArchiveGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
        public string ArchiveReason { get; set; } = default!;

        public string YkbServicesRequestJson { get; set; } = default!;
        public string YkbServicesRequestProductsJson { get; set; } = default!;
        public string CustomerJson { get; set; } = default!;
        public string ApproverTechnicianJson { get; set; } = default!;
        public string CustomerApproverJson { get; set; } = default!;
        public string YkbWorkFlowJson { get; set; } = default!;
        public string YkbWorkFlowReviewLogsJson { get; set; } = default!;
        public string YkbTechnicalServiceJson { get; set; } = default!;
        public string YkbTechnicalServiceImagesJson { get; set; } = default!;
        public string YkbTechnicalServiceFormImagesJson { get; set; } = default!;
        public string YkbWarehouseJson { get; set; } = default!;
        public string YkbPricingJson { get; set; } = default!;
        public string YkbFinalApprovalJson { get; set; } = default!;
    }
}
