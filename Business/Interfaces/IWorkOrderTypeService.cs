using Model.Dtos.WorkOrderType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    namespace Business.Interfaces
    {
        public interface IWorkOrderTypeService
            : ICrudService<
                WorkOrderTypeCreateDto,
                WorkOrderTypeUpdateDto,
                WorkOrderTypeGetDto,
                long>
        {
        }
    }
}
