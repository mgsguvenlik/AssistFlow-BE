using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.CustomerSystem
{
    public class CustomerSystemCreateDto
    {
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
    }
}
