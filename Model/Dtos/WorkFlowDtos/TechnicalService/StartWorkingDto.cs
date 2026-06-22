using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public sealed class StartWorkingDto
    {
        [Required(ErrorMessage = "Talep numarası zorunludur.")]
        public string RequestNo { get; set; } = string.Empty;

    }
}
