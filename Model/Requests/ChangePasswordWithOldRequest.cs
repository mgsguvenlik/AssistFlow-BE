using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Requests
{
    public sealed class ChangePasswordWithOldRequest
    {
        [Required] public string OldPassword { get; set; } = string.Empty;
        [Required] public string NewPassword { get; set; } = string.Empty;
        [Required] public string NewPasswordConfirm { get; set; } = string.Empty;
    }
}
