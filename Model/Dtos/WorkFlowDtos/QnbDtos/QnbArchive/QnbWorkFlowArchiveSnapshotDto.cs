using Model.Dtos.WorkFlowDtos.WorkFlowArchive;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive
{
    public class QnbWorkFlowArchiveSnapshotDto
    {
        public Concrete.Qnb.QnbServicesRequest? ServicesRequest { get; set; }
        public List<Concrete.Qnb.QnbServicesRequestProduct> Products { get; set; } = new();
        public Concrete.Customer? Customer { get; set; }
        public Concrete.User? ApproverTechnician { get; set; }
        public Concrete.ProgressApprover? CustomerApprover { get; set; }
        public Concrete.Qnb.QnbWorkFlow? WorkFlow { get; set; }
        public List<Concrete.Qnb.QnbWorkFlowReviewLog> WorkFlowReviewLogs { get; set; } = new();
        public Concrete.Qnb.QnbTechnicalService? TechnicalService { get; set; }
        public List<ArchiveImageDto> ServiceImages { get; set; } = new();
        public List<ArchiveImageDto> FormImages { get; set; } = new();
        public Concrete.Qnb.QnbWarehouse? Warehouse { get; set; }
        public Concrete.Qnb.QnbPricing? Pricing { get; set; }
        public Concrete.Qnb.QnbFinalApproval? FinalApproval { get; set; }
    }
}