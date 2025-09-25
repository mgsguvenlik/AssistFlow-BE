using Model.Abstractions;

namespace Model.Concrete
{
    public class WorkflowHistory:AuditableWithUserEntity
    {
        public long Id { get; set; }
        public DateOnly Date { get; set; }
        public long StatusId { get; set; }

        // Navigations (öneri):
        public long WorkFlowId { get; set; }
        public WorkFlow? WorkFlow { get; set; }          
        public WorkFlowStatus? Status { get; set; }
        // Not: Eğer WfHistory, WorkFlow kaydına FK ile bağlansın istiyorsanız ayrıca WorkFlowId ekleyin.
    }
}
