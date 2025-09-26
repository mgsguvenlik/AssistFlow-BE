using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.CustomerType
{
    public class CustomerTypeUpdateDto : CustomerTypeCreateDto
    {
        public long Id { get; set; }
    }
}
