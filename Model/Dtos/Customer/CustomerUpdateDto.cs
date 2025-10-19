using Core.Utilities.Constants;
using Model.Dtos.ProgressApprover;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Customer
{
    public class CustomerUpdateDto
    {
        public long Id { get; set; }


        // Kodlar: harf, rakam, ., _, - (opsiyonel)
        [StringLength(64, ErrorMessage = Messages.SubscriberCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.SubscriberCodeInvalidChars)]
        public string? SubscriberCode { get; set; }

        [StringLength(200, ErrorMessage = Messages.SubscriberCompanyMaxLength)]
        [NotWhitespaceIfNotEmpty(ErrorMessage = Messages.SubscriberCompanyCannotBeWhitespace)]
        public string? SubscriberCompany { get; set; }

        [StringLength(500, ErrorMessage = Messages.AddressMaxLength)]
        public string? SubscriberAddress { get; set; }

        [StringLength(100, ErrorMessage = Messages.CityMaxLength)]
        public string? City { get; set; }

        [StringLength(100, ErrorMessage = Messages.CityMaxLength)]
        public string? District { get; set; }

        [StringLength(64, ErrorMessage = Messages.LocationCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.LocationCodeInvalidChars)]
        public string? LocationCode { get; set; }

  
        [StringLength(120, ErrorMessage = Messages.FirstPersonNameMaxLength)]
        [NotWhitespaceIfNotEmpty(ErrorMessage = Messages.FirstPersonNameCannotBeWhitespace)]
        public string? ContactName1 { get; set; }

        // Telefon: +905551112233 veya 05551112233 gibi (7-15 rakam, isteğe bağlı +)
        [RegexIfNotEmpty(@"^\+?[0-9]{7,15}$", ErrorMessage = Messages.PhoneNumberFormat)]
        public string? Phone1 { get; set; }

        [EmailAddress(ErrorMessage = Messages.EnterValidEmail)]
        [StringLength(200, ErrorMessage = Messages.EmailMaxLength)]
        public string? Email1 { get; set; }

        public string? ContactName2 { get; set; }

        public string? Phone2 { get; set; }

        public string? Email2 { get; set; }

        [StringLength(32, ErrorMessage = Messages.CustomerShortCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.CustomerShortCodeInvalidChars)]
        public string? CustomerShortCode { get; set; }

        [StringLength(64, ErrorMessage = Messages.CorporateLocationIdMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.CorporateLocationIdInvalidChars)]
        public string? CorporateLocationId { get; set; }

        // Nullable olduğu için boş geçilebilir; değer girilirse 1 ve üzeri olmalı
        [Range(1, long.MaxValue, ErrorMessage = Messages.CustomerTypeInvalid)]
        public long? CustomerTypeId { get; set; }
        public string? Longitude { get; set; }
        public string? Latitude { get; set; }
        public DateTimeOffset? InstallationDate { get; set; }

    }

}
