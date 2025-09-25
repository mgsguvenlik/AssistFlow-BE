using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Interfaces
{
    /// <summary>Oluşturan / güncelleyen kullanıcı alanları için arayüz.</summary>
    public interface IAuditedByUser
    {
        string? CreatedUser { get; set; }
        string? UpdatedUser { get; set; }
    }
}
