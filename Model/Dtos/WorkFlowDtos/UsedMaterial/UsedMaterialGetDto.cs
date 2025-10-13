using Model.Concrete.WorkFlows;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.UsedMaterial
{
    public class UsedMaterialGetDto
    {
        public long Id { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public long TechnicalServiceId { get; set; }
    }
}
