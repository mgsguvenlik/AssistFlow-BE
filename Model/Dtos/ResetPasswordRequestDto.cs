using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos
{
    public class ResetPasswordRequestDto
    {
        public required string Email { get; set; }
    }
}
