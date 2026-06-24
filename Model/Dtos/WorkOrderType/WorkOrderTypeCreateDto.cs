using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkOrderType
{
    public class WorkOrderTypeCreateDto
    {
        [Required(ErrorMessage = "İş emri türü adı zorunludur.")]
        [StringLength(
            120,
            MinimumLength = 2,
            ErrorMessage = "İş emri türü adı 2 ile 120 karakter arasında olmalıdır."
        )]
        [RegularExpression(
            @".*\S.*",
            ErrorMessage = "İş emri türü adı yalnızca boşluklardan oluşamaz."
        )]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "İş emri türü kodu zorunludur.")]
        [StringLength(
            64,
            MinimumLength = 2,
            ErrorMessage = "İş emri türü kodu 2 ile 64 karakter arasında olmalıdır."
        )]
        [RegularExpression(
            @"^[A-Za-z0-9._/-]+$",
            ErrorMessage = "Kod yalnızca harf, rakam, nokta, alt çizgi, tire ve slash içerebilir."
        )]
        public string Code { get; set; } = string.Empty;
    }
}