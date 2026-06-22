using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public sealed class ExtendWorkingDto
    {
        [Required]
        public string RequestNo { get; set; } = string.Empty;

        public int ExtendMinutes { get; set; } = 30;
    }   
}
