using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// Fazla mesai detay bilgisi (hangi politikaya göre, ne kadar süre)
    /// </summary>
    public class OvertimeBreakdownDto
    {
        /// <summary>
        /// Politika adı (WorkingHourPolicy.Name)
        /// Örn: "Cumartesi Günü", "Pazar Günü", "Mesai Sonrası", "Resmi Tatil - Yılbaşı"
        /// </summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>
        /// Politika tipi metni
        /// Örn: "Hafta Günü", "Resmi Tatil"
        /// </summary>
        public string PolicyTypeText { get; set; } = string.Empty;

        /// <summary>
        /// Bu politikaya göre yapılan fazla mesai süresi (saat)
        /// </summary>
        public double Hours { get; set; }

        /// <summary>
        /// Başlangıç zamanı
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Bitiş zamanı
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Açıklama (opsiyonel)
        /// Örn: "Cumartesi günü tüm gün fazla mesai"
        /// </summary>
        public string? Description { get; set; }
    }
}
