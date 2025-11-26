using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.TechnicalServiceImage
{
    public class TechnicalServiceImageGetDto
    {
        public long Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }

}
