using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Brand
{
    public class BrandCreateDto
    {
        public string Name { get; set; } = null!;
        public string? Desc { get; set; }
    }
}
