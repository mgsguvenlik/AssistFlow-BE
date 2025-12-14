using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.User
{
    public class UpdateUserPasswordDto
    {
        public long UserId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
}
