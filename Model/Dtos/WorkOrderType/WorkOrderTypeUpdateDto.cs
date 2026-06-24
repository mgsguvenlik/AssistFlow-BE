using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkOrderType
{
    public class WorkOrderTypeUpdateDto : WorkOrderTypeCreateDto
    {
        public long Id { get; set; }
    }
}
