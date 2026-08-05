using Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class TechnicalServiceStatusCountDto
    {
        public TechnicalServiceStatus ServicesStatus { get; set; }

        public string ServicesStatusName { get; set; } = string.Empty;

        public int RecordCount { get; set; }
    }
}
