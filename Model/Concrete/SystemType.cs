using Model.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Concrete
{
    public class SystemType:SoftDeleteEntity
    {
        public long Id { get; set; }

        /// <summary>Sistem tipi adı (örn: ARIZA, ŞUBE TADİLAT).</summary>
        public string Name { get; set; } = null!;

        /// <summary>Kısa kod (örn: 85887, 85889, 85892).</summary>
        public string? Code { get; set; }
    }
}
