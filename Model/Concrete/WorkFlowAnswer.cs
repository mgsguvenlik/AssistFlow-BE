using Model.Abstractions;

namespace Model.Concrete
{
    public class WorkFlowAnswer : AuditableWithUserEntity
    {
        public long Id { get; set; }
        public string Value { get; set; } = null!;

        public long WorkFlowId { get; set; }
        public WorkFlow? WorkFlow { get; set; }
        public long FormFieldId { get; set; }
        public FormField? FormField { get; set; } // Önceki şemanızdan
    }
}
