using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbTechnicalService", Schema = "ykb")]
    public class YkbTechnicalService : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        // Hizmet türü (lookup tabloyu FK ile bağlıyorum — projende ServicesType tablosu varsa ona uyar)
        public long? ServiceTypeId { get; set; }
        public ServiceType? ServiceType { get; set; }

        // Gerçekleşen zaman damgaları
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        // Teknik alanlar
        public string? ProblemDescription { get; set; } //Problem Tanımı
        public string? ResolutionAndActions { get; set; }//Alınan Aksiyonlar ve Çözüm
        public string? Latitude { get; set; } //Gidilen müşteri enlem Lokasyonu
        public string? Longitude { get; set; }//Gidilen müşteri boylam Lokasyonu
        public string? StartLocation { get; set; }//Örn: "41.01224, 28.976018"
        public string? EndLocation { get; set; }//Örn: "41.01224, 28.976018"
        public bool IsLocationCheckRequired { get; set; } = true;//Lokasyon kontrolü gerekli mi?

        // Durumlar
        public TechnicalServiceStatus ServicesStatus { get; set; }

        // Maliyet durumu
        public ServicesCostStatus ServicesCostStatus { get; set; }

        // Görseller
        public ICollection<YkbTechnicalServiceImage> YkbServicesImages { get; set; } = new List<YkbTechnicalServiceImage>();
        public ICollection<YkbTechnicalServiceFormImage> YkbServiceRequestFormImages { get; set; } = new List<YkbTechnicalServiceFormImage>();
    }
}
