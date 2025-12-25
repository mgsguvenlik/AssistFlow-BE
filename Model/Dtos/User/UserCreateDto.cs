using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.User
{
    public class UserCreateDto : IValidatableObject
    {
      

        [Required(ErrorMessage = Messages.TechnicianCodeRequired)]
        [MaxLength(50)]
        [RegularExpression(@"^\S+$", ErrorMessage = Messages.TechnicianCodeNoSpaces)] // hiç boşluk yok (baş/son/arası)
        public string TechnicianCode { get; set; } = string.Empty;


        [MaxLength(200)]
        public string? TechnicianCompany { get; set; }

        [MaxLength(500)]
        public string? TechnicianAddress { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? District { get; set; }

        [Required, MaxLength(150)]
        public string TechnicianName { get; set; } = string.Empty;

        [MaxLength(30), Phone]
        public string? TechnicianPhone { get; set; }

        [MaxLength(254), EmailAddress]
        public string? TechnicianEmail { get; set; }
        public long? TenantId { get; set; }

        // Şifre: min 8, en az 1 büyük, 1 küçük harf ve 1 rakam
        [Required, MinLength(8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = Messages.PasswordComplexity)]
        public string Password { get; set; } = string.Empty;

        // Opsiyonel; verilirse kurallara uysun
        public List<long>? RoleIds { get; set; }

        // Ek kurallar (RoleIds pozitif ve benzersiz olsun; boş liste kabul edilmesin)
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (RoleIds != null)
            {
                if (RoleIds.Count == 0)
                    yield return new ValidationResult(Messages.AtLeastOneRoleRequired, new[] { nameof(RoleIds) });

                if (RoleIds.Any(id => id <= 0))
                    yield return new ValidationResult(Messages.RoleIdsMustBePositive, new[] { nameof(RoleIds) });

                var duplicates = RoleIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (duplicates.Count > 0)
                    yield return new ValidationResult(
                        $"{Messages.DuplicateRoleIds} {string.Join(", ", duplicates)}",
                        new[] { nameof(RoleIds) });
            }
        }
    }
}
