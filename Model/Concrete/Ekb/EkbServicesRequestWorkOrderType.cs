using Model.Concrete.WorkFlows;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{
    /// <summary>
    /// EkbServicesRequest ile WorkOrderType arasındaki çoktan-çoğa ilişki tablosu.
    /// </summary>
    [Table("EkbServicesRequestWorkOrderTypes", Schema = "ekb")]
    public class EkbServicesRequestWorkOrderType
    {
        public long EkbServicesRequestId { get; set; }

        public EkbServicesRequest EkbServicesRequest { get; set; } = default!;

        public long WorkOrderTypeId { get; set; }

        public WorkOrderType WorkOrderType { get; set; } = default!;
    }
}
