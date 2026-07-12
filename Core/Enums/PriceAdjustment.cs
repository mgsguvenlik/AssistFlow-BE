namespace Core.Enums
{
    /// <summary>
    /// Fiyat düzenleme değerinin yüzde mi yoksa sabit tutar mı olduğunu belirtir.
    /// </summary>
    public enum PriceAdjustmentType
    {
        Percentage = 1,
        FixedAmount = 2
    }

    /// <summary>
    /// Fiyata ekleme mi, fiyattan çıkarma mı yapılacağını belirtir.
    /// </summary>
    public enum PriceAdjustmentDirection
    {
        Increase = 1,
        Decrease = 2
    }

    /// <summary>
    /// Sabit tutarın satır toplamına bir defa mı,
    /// yoksa ürün adedi başına mı uygulanacağını belirtir.
    /// </summary>
    public enum PriceAdjustmentCalculationBasis
    {
        LineTotal = 1,
        UnitPrice = 2
    }
}