using Model.Concrete.Ykb;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Concrete.Qnb
{
    [Table("QnbServicesRequestWorkOrderTypes", Schema = "qnb")]
    public class QnbServicesRequestWorkOrderType
    {
        public long QnbServicesRequestId { get; set; }

        public QnbServicesRequest QnbServicesRequest { get; set; } = default!;

        public long WorkOrderTypeId { get; set; }

        public WorkOrderType WorkOrderType { get; set; } = default!;
    }
}
