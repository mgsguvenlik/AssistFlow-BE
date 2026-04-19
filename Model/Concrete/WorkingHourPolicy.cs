using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    /// <summary>
    /// Mesai saatleri ve fazla mesai politikasý yönetimi
    /// Hafta içi, hafta sonu, resmi tatiller ve özel günler için mesai kurallarý
    /// </summary>
    public class WorkingHourPolicy : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Politika adý (Türkçe)
        /// Örn: "Yýlbaþý", "Cumartesi Günü", "Hafta Ýçi Normal Mesai"
        /// </summary>
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Politika açýklamasý
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Politika tipi
        /// </summary>
        [Required]
        public WorkingHourPolicyType PolicyType { get; set; }

        /// <summary>
        /// Belirli bir tarih (PolicyType = SpecificDate için)
        /// Örn: 2026-01-01 (Yýlbaþý)
        /// </summary>
        public DateOnly? SpecificDate { get; set; }

        /// <summary>
        /// Yýl (PolicyType = PublicHoliday veya SpecificDate için)
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Haftanýn günü (PolicyType = WeekDay için)
        /// 0=Pazar, 1=Pazartesi, ..., 6=Cumartesi
        /// </summary>
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>
        /// Normal mesai baþlangýç saati (null ise tüm gün fazla mesai)
        /// Örn: 09:00
        /// </summary>
        public TimeOnly? WorkStartTime { get; set; }

        /// <summary>
        /// Normal mesai bitiþ saati (null ise tüm gün fazla mesai)
        /// Örn: 18:00
        /// </summary>
        public TimeOnly? WorkEndTime { get; set; }

        /// <summary>
        /// Þirket bu politikayý uyguluyor mu?
        /// TRUE = Uyguluyor (fazla mesai hesabýna dahil)
        /// FALSE = Uygulamýyor (normal çalýþma günü)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Öncelik seviyesi (çakýþan politikalarda hangisi geçerli olacak)
        /// Yüksek deðer = yüksek öncelik
        /// Örn: Belirli tarih (100) > Hafta sonu (50) > Hafta içi (10)
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Ülke kodu (çok uluslu þirketler için)
        /// </summary>
        [MaxLength(10)]
        public string CountryCode { get; set; } = "TR";

        /// <summary>
        /// Resmi tatil mi? (raporlama için)
        /// </summary>
        public bool IsPublicHoliday { get; set; } = false;

        /// <summary>
        /// Tatil türleri (JSON array olarak saklanýr)
        /// Örn: ["Public", "National"]
        /// </summary>
        public string? HolidayTypes { get; set; }

        public long? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }

    /// <summary>
    /// Çalýþma saati politika tipleri
    /// </summary>
    public enum WorkingHourPolicyType
    {
        /// <summary>Hafta içi default (Pazartesi-Cuma)</summary>
        WeekdayDefault = 1,

        /// <summary>Hafta sonu default (Cumartesi-Pazar)</summary>
        WeekendDefault = 2,

        /// <summary>Belirli bir haftanýn günü (Örn: Her Cumartesi)</summary>
        WeekDay = 3,

        /// <summary>Resmi tatil (Her yýl tekrar eden)</summary>
        PublicHoliday = 4,

        /// <summary>Belirli bir tarih (Tek seferlik veya yýllýk)</summary>
        SpecificDate = 5,

        /// <summary>Özel gün (Þirket özel günleri)</summary>
        CustomDay = 6
    }
}