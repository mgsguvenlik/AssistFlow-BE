using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Customer
{
    public class CustomerUpdateDto : CustomerCreateDto
    {
        public long Id { get; set; }
    }

}
