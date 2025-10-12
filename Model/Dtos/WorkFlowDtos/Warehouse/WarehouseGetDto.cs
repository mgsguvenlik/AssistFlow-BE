using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class WarehouseGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public DateTimeOffset DeliveryDate { get; set; }

        public long? ApproverTechnicianId { get; set; }
        public string? ApproverTechnicianName { get; set; }   // opsiyonel gösterim alanı
        public string? ApproverTechnicianEmail { get; set; }  // opsiyonel gösterim alanı

        public string? Description { get; set; }
        public bool IsSended { get; set; }

        // Ekranlar için yalnızca ürün Id listesi
        public List<long> ProductIds { get; set; } = new();
    }

}
