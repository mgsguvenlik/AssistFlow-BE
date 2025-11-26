namespace Model.Dtos.WorkFlowDtos.WorkFlowArchive
{
    public class WorkFlowArchiveSnapshotDto
    {
        public Concrete.WorkFlows.ServicesRequest? ServicesRequest { get; set; }
        public List<Concrete.WorkFlows.ServicesRequestProduct> Products { get; set; } = new();
        public Concrete.Customer? Customer { get; set; }
        public Concrete.User? ApproverTechnician { get; set; }
        public Concrete.ProgressApprover? CustomerApprover { get; set; }
        public Concrete.WorkFlows.WorkFlow? WorkFlow { get; set; }
        public List<Concrete.WorkFlows.WorkFlowReviewLog> WorkFlowReviewLogs { get; set; } = new();
        public Concrete.WorkFlows.TechnicalService? TechnicalService { get; set; }
        public List<ArchiveImageDto> ServiceImages { get; set; } = new();
        public List<ArchiveImageDto> FormImages { get; set; } = new();
        public Concrete.WorkFlows.Warehouse? Warehouse { get; set; }
        public Concrete.WorkFlows.Pricing? Pricing { get; set; }
        public Concrete.WorkFlows.FinalApproval? FinalApproval { get; set; }
    }
}
