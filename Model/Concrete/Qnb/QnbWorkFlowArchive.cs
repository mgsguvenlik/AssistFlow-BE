using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbWorkFlowArchive", Schema = "qnb")]
    public class QnbWorkFlowArchive
    {
        public long Id { get; set; }

        public string RequestNo { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
        public string ArchiveReason { get; set; } = default!;

        public string QnbServicesRequestJson { get; set; } = default!;
        public string QnbServicesRequestProductsJson { get; set; } = default!;
        public string CustomerJson { get; set; } = default!;
        public string ApproverTechnicianJson { get; set; } = default!;
        public string CustomerApproverJson { get; set; } = default!;
        public string QnbWorkFlowJson { get; set; } = default!;
        public string QnbWorkFlowReviewLogsJson { get; set; } = default!;
        public string QnbTechnicalServiceJson { get; set; } = default!;
        public string QnbTechnicalServiceImagesJson { get; set; } = default!;
        public string QnbTechnicalServiceFormImagesJson { get; set; } = default!;
        public string QnbWarehouseJson { get; set; } = default!;
        public string QnbPricingJson { get; set; } = default!;
        public string QnbFinalApprovalJson { get; set; } = default!;

        public string? QnbWorkflowAttachmentsJson { get; set; }
    }
}