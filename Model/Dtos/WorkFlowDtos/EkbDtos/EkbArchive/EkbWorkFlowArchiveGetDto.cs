namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbArchive
{
    public class EkbWorkFlowArchiveGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
        public string ArchiveReason { get; set; } = default!;

        public string EkbServicesRequestJson { get; set; } = default!;
        public string EkbServicesRequestProductsJson { get; set; } = default!;
        public string CustomerJson { get; set; } = default!;
        public string ApproverTechnicianJson { get; set; } = default!;
        public string CustomerApproverJson { get; set; } = default!;
        public string EkbWorkFlowJson { get; set; } = default!;
        public string EkbWorkFlowReviewLogsJson { get; set; } = default!;
        public string EkbTechnicalServiceJson { get; set; } = default!;
        public string EkbTechnicalServiceImagesJson { get; set; } = default!;
        public string EkbTechnicalServiceFormImagesJson { get; set; } = default!;
        public string EkbWarehouseJson { get; set; } = default!;
        public string EkbPricingJson { get; set; } = default!;
        public string EkbFinalApprovalJson { get; set; } = default!;
    }
}
