using Model.Dtos.WorkFlowDtos.WorkFlowArchive;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive
{
    public class YkbWorkFlowArchiveSnapshotDto
    {
        public Concrete.Ykb.YkbServicesRequest? ServicesRequest { get; set; }
        public List<Concrete.Ykb.YkbServicesRequestProduct> Products { get; set; } = new();
        public Concrete.Customer? Customer { get; set; }
        public Concrete.User? ApproverTechnician { get; set; }
        public Concrete.ProgressApprover? CustomerApprover { get; set; }
        public Concrete.Ykb.YkbWorkFlow? WorkFlow { get; set; }
        public List<Concrete.Ykb.YkbWorkFlowReviewLog> WorkFlowReviewLogs { get; set; } = new();
        public Concrete.Ykb.YkbTechnicalService? TechnicalService { get; set; }
        public List<ArchiveImageDto> ServiceImages { get; set; } = new();
        public List<ArchiveImageDto> FormImages { get; set; } = new();
        public Concrete.Ykb.YkbWarehouse? Warehouse { get; set; }
        public Concrete.Ykb.YkbPricing? Pricing { get; set; }
        public Concrete.Ykb.YkbFinalApproval? FinalApproval { get; set; }
    }
}
