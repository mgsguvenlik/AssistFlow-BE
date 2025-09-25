using Model.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Concrete
{
    // Model
    public class Model:SoftDeleteEntity
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Desc { get; set; }

        public long BrandId { get; set; }
        public Brand Brand { get; set; } = null!;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
