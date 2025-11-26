using Model.Dtos.CustomerSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface ICustomerSystemService
        : ICrudService<CustomerSystemCreateDto, CustomerSystemUpdateDto, CustomerSystemGetDto, long>
    {
        // İleride CustomerSystem’e özel ekstra metot gerekirse buraya ekleyebilirsin
    }
}
