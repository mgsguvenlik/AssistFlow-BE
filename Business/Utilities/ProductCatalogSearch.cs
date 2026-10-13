using System.Globalization;
using Model.Concrete;

namespace Business.Utilities;

public static class ProductCatalogSearch
{
    public static decimal[] NumericValues(string search)
    {
        var values = new List<decimal>();
        foreach (var culture in new[] { CultureInfo.GetCultureInfo("tr-TR"), CultureInfo.InvariantCulture })
            if (decimal.TryParse(search, NumberStyles.Number, culture, out var number) && !values.Contains(number))
                values.Add(number);
        return values.ToArray();
    }

    public static long[] EffectivePriceMatches(
        IEnumerable<(long ProductId, decimal Price, string? Currency)> prices, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return [];
        var term = search.Trim();
        var numbers = NumericValues(term);
        // The caller supplies overrides in group > customer > tenant order,
        // matching the price actually returned by the product catalogue.
        return prices.GroupBy(x => x.ProductId).Select(x => x.First())
            .Where(x => numbers.Contains(x.Price) ||
                (x.Currency?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(x => x.ProductId).ToArray();
    }

    public static IQueryable<Product> Apply(
        IQueryable<Product> products, IQueryable<SystemType> systemTypes,
        string? search, long[]? effectivePriceMatches = null, bool includeInternalIdentifiers = false)
    {
        products = products.Where(x => !x.IsDeleted);
        if (string.IsNullOrWhiteSpace(search)) return products;
        var term = search.Trim();
        var numbers = NumericValues(term);
        var identifiers = numbers.Where(x => x >= long.MinValue && x <= long.MaxValue && decimal.Truncate(x) == x)
            .Select(decimal.ToInt64).ToArray();
        var effectiveIds = effectivePriceMatches ?? [];
        var compare = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
        var serviceFee = compare.IndexOf("hizmet bedeli", term, CompareOptions.IgnoreCase) >= 0;
        var ordinaryProduct = compare.IndexOf("normal ürün", term, CompareOptions.IgnoreCase) >= 0;

        return products.Where(x =>
            (x.ProductCode != null && x.ProductCode.Contains(term)) ||
            (x.OracleProductCode != null && x.OracleProductCode.Contains(term)) ||
            (x.Description != null && x.Description.Contains(term)) ||
            (x.SystemType != null && (x.SystemType.Contains(term) ||
                systemTypes.Any(s => (s.Name.Contains(term) || (s.Code != null && s.Code.Contains(term))) &&
                    (x.SystemType == s.Name || (s.Code != null && s.Code != "" &&
                        (x.SystemType == s.Code || x.SystemType.EndsWith(" (" + s.Code + ")"))))))) ||
            (x.CorporateCustomerShortCode != null && x.CorporateCustomerShortCode.Contains(term)) ||
            (x.OracleCustomerCode != null && x.OracleCustomerCode.Contains(term)) ||
            (x.PriceCurrency != null && x.PriceCurrency.Contains(term)) ||
            (x.Brand != null && (x.Brand.Name.Contains(term) || (x.Brand.Desc != null && x.Brand.Desc.Contains(term)))) ||
            (x.Model != null && (x.Model.Name.Contains(term) || (x.Model.Desc != null && x.Model.Desc.Contains(term)))) ||
            (x.ProductType != null && (x.ProductType.Type.Contains(term) || (x.ProductType.Code != null && x.ProductType.Code.Contains(term)))) ||
            (x.CurrencyType != null && (x.CurrencyType.Code.Contains(term) || (x.CurrencyType.Name != null && x.CurrencyType.Name.Contains(term)))) ||
            (x.Price.HasValue && numbers.Contains(x.Price.Value)) ||
            (x.ServiceFeePercentage.HasValue && numbers.Contains(x.ServiceFeePercentage.Value)) ||
            (serviceFee && x.IsServiceFeeProduct == true) ||
            (ordinaryProduct && x.IsServiceFeeProduct != true) ||
            effectiveIds.Contains(x.Id) ||
            // Preserve the general product list's pre-existing numeric ID search.
            (includeInternalIdentifiers && (identifiers.Contains(x.Id) || identifiers.Contains(x.CreatedUser) ||
                (x.UpdatedUser.HasValue && identifiers.Contains(x.UpdatedUser.Value)) ||
                (x.BrandId.HasValue && identifiers.Contains(x.BrandId.Value)) ||
                (x.ModelId.HasValue && identifiers.Contains(x.ModelId.Value)) ||
                (x.ProductTypeId.HasValue && identifiers.Contains(x.ProductTypeId.Value)) ||
                (x.CurrencyTypeId.HasValue && identifiers.Contains(x.CurrencyTypeId.Value)))));
    }
}
