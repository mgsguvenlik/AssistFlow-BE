using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos
{
    public sealed class FinishWorkingDto
    {
        [Required]
        public string RequestNo { get; set; } = string.Empty;

        public bool ForceFinish { get; set; }
    }
}
