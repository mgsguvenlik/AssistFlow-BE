using Model.Dtos.Manitou;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public sealed class StartWorkingResultDto
    {
        public string RequestNo { get; set; } = string.Empty;

        public int SerialNo { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; }

        public DateTimeOffset PlannedEndAtUtc { get; set; }

        public List<ManitouSystemTestZoneResult> Zones { get; set; } = new();
    }
}
