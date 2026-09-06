using Model.Dtos.WorkFlowDtos.WorkFlowArchive;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAttachment;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbArchive
{
    public class EkbWorkFlowArchiveSnapshotDto
    {
        public Concrete.Ekb.EkbServicesRequest? ServicesRequest { get; set; }
        public List<Concrete.Ekb.EkbServicesRequestProduct> Products { get; set; } = new();
        public Concrete.Customer? Customer { get; set; }
        public Concrete.User? ApproverTechnician { get; set; }
        public Concrete.ProgressApprover? CustomerApprover { get; set; }
        public Concrete.Ekb.EkbWorkFlow? WorkFlow { get; set; }
        public List<Concrete.Ekb.EkbWorkFlowReviewLog> WorkFlowReviewLogs { get; set; } = new();
        public Concrete.Ekb.EkbTechnicalService? TechnicalService { get; set; }
        public List<ArchiveImageDto> ServiceImages { get; set; } = new();
        public List<ArchiveImageDto> FormImages { get; set; } = new();
        public Concrete.Ekb.EkbWarehouse? Warehouse { get; set; }
        public Concrete.Ekb.EkbPricing? Pricing { get; set; }
        public Concrete.Ekb.EkbFinalApproval? FinalApproval { get; set; }
        public List<EkbWorkflowAttachmentGetDto> Attachments { get; set; } = new();
    }
}
