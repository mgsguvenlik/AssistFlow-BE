using Model.Abstractions;

namespace Model.Concrete
{
    public class WorkFlowStatus:SoftDeleteEntity
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;

        // Navigations
        public long RoleId { get; set; } // Bu statüyü kim/hangi rol işlem yapar?
        public Role? Role { get; set; }
        public ICollection<WorkFlow> WorkFlows { get; set; } = new List<WorkFlow>();
        public ICollection<WorkflowHistory> Histories { get; set; } = new List<WorkflowHistory>();
    }
}
