using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Tenant
{
    public class TenantFilterDto
    {
        public string? SearchText { get; set; }   // name, code üzerinde search
        public bool? IsActive { get; set; }
        public int PageIndex { get; set; } = 0;
        public int PageSize { get; set; } = 20;
    }
}
