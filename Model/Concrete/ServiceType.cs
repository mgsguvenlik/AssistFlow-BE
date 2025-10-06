using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    // Servis Türü
    public class ServiceType : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>Servis türü adı (örn: Kurulum, Keşif).</summary>
        public string Name { get; set; } = null!;

        /// <summary>Sözleşme numarası / kod (örn: 001, 002, 003).</summary>
        public string? ContractNumber { get; set; }
    }
}
