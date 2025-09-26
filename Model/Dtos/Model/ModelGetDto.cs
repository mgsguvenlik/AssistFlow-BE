using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Model
{
    public class ModelGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Desc { get; set; }
    }
}
