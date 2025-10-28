namespace Core.Enums
{
    public enum WarehouseStatus
    {
        Pending = 1,          // Sevkiyat Bekliyor / Aktif
        Shipped = 2,          // Sevk Edildi / Tamamlandı
        AwaitingReview = 3,   // Revizyon Bekliyor (Geri dönüş durumu)
    }
}
