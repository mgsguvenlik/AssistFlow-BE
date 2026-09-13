using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public enum CollectionDefinitionKind { PaymentFrequency, ContractStatus, SubscriptionStatus, GroupStatus, PaymentMethod }

public sealed class CollectionDefinitionQuery
{
    [EnumDataType(typeof(CollectionDefinitionKind), ErrorMessage = "Tanım türü geçersiz.")]
    public CollectionDefinitionKind Kind { get; set; }
    [Range(1, 1000000, ErrorMessage = "Sayfa numarası 1 ile 1000000 arasında olmalıdır.")]
    public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 25;
    [StringLength(200, ErrorMessage = "Arama metni en fazla 200 karakter olabilir.")]
    public string? Search { get; set; }
}

public sealed class CollectionDefinitionItem
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public short? IntervalMonths { get; set; }
}
