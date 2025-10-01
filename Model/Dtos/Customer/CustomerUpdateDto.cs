using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Customer
{
    public class CustomerUpdateDto 
    {
        public long Id { get; set; }


        // Kodlar: harf, rakam, ., _, - (opsiyonel)
        [StringLength(64, ErrorMessage = "Abone Kodu en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Abone Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? SubscriberCode { get; set; }

        [StringLength(200, ErrorMessage = "Abone Firma en fazla 200 karakter olabilir.")]
        [NotWhitespaceIfNotEmpty(ErrorMessage = "Abone Firma yalnızca boşluklardan oluşamaz.")]
        public string? SubscriberCompany { get; set; }

        [StringLength(120, ErrorMessage = "Müşteri Ana Grup Adı en fazla 120 karakter olabilir.")]
        public string? CustomerMainGroupName { get; set; }

        [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
        public string? SubscriberAddress { get; set; }

        [StringLength(100, ErrorMessage = "İl en fazla 100 karakter olabilir.")]
        public string? City { get; set; }

        [StringLength(64, ErrorMessage = "Lokasyon Kodu en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Lokasyon Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? LocationCode { get; set; }

        [StringLength(64, ErrorMessage = "Oracle Kodu en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Oracle Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? OracleCode { get; set; }

        [StringLength(120, ErrorMessage = "1. Kişi adı en fazla 120 karakter olabilir.")]
        [NotWhitespaceIfNotEmpty(ErrorMessage = "1. Kişi adı yalnızca boşluklardan oluşamaz.")]
        public string? ContactName1 { get; set; }

        // Telefon: +905551112233 veya 05551112233 gibi (7-15 rakam, isteğe bağlı +)
        [RegexIfNotEmpty(@"^\+?[0-9]{7,15}$", ErrorMessage = "Telefon 7-15 haneli olmalı ve sadece rakam (isteğe bağlı +) içermelidir.")]
        public string? Phone1 { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        [StringLength(200, ErrorMessage = "E-posta en fazla 200 karakter olabilir.")]
        public string? Email1 { get; set; }

        public string? ContactName2 { get; set; }

        public string? Phone2 { get; set; }

        public string? Email2 { get; set; }

        [StringLength(32, ErrorMessage = "Müşteri Kısa Kodu en fazla 32 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Müşteri Kısa Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? CustomerShortCode { get; set; }

        [StringLength(64, ErrorMessage = "Kurumsal Lokasyon ID en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Kurumsal Lokasyon ID yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? CorporateLocationId { get; set; }

        // Nullable olduğu için boş geçilebilir; değer girilirse 1 ve üzeri olmalı
        [Range(1, long.MaxValue, ErrorMessage = "Müşteri Tipi geçersiz.")]
        public long? CustomerTypeId { get; set; }
    }




}
