using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.ProductPriceAdjustmentRule
{
    public class ProductPriceAdjustmentRuleUpdateDto
        : ProductPriceAdjustmentRuleCreateDto
    {
        [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir kayıt Id değeri gönderilmelidir.")]
        public long Id { get; set; }
    }
}