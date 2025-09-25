using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos
{
    public class ChangePasswordDto
    {
        public required string RecoveryCode { get; set; }
        public required string NewPassword { get; set; }
        public required string NewPasswordConfirm { get; set; }
    }
}
