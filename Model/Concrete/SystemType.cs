using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class SystemType : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>Sistem tipi adı (örn: ARIZA, ŞUBE TADİLAT).</summary>
        public string Name { get; set; } = null!;

        /// <summary>Kısa kod (örn: 85887, 85889, 85892).</summary>
        public string? Code { get; set; }
    }
}
