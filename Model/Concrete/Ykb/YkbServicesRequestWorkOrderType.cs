using Model.Concrete.WorkFlows;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    /// <summary>
    /// YkbServicesRequest ile WorkOrderType arasýndaki çoktan-çoða iliþki tablosu.
    /// </summary>
    [Table("YkbServicesRequestWorkOrderTypes", Schema = "ykb")]
    public class YkbServicesRequestWorkOrderType
    {
        public long YkbServicesRequestId { get; set; }

        public YkbServicesRequest YkbServicesRequest { get; set; } = default!;

        public long WorkOrderTypeId { get; set; }

        public WorkOrderType WorkOrderType { get; set; } = default!;
    }
}
