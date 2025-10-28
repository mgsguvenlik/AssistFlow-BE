using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class WarehouseCreateDto
    {
        [Required, StringLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset DeliveryDate { get; set; }

        public string? Description { get; set; }

        // Depoya gidecek ürünler
        public List<long>? ProductIds { get; set; }
    }
}
