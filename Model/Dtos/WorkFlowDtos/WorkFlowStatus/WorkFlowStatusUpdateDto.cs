using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlowStatus
{
    public class WorkFlowStatusUpdateDto
    {
        public long Id { get; set; }
        public string? Name { get; set; }   // null gelirse mevcut değer kalsın (Mapster IgnoreNullValues)
        public string? Code { get; set; }
    }
}
