using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    /// <summary>
    /// ServicesRequest ile WorkOrderType arasındaki çoktan-çoğa ilişki tablosu.
    /// </summary>
    [Table("ServicesRequestWorkOrderTypes")]
    public class ServicesRequestWorkOrderType
    {
        public long ServicesRequestId { get; set; }

        public ServicesRequest ServicesRequest { get; set; } = default!;

        public long WorkOrderTypeId { get; set; }

        public WorkOrderType WorkOrderType { get; set; } = default!;
    }
}