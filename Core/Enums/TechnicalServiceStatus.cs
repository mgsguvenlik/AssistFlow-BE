namespace Core.Enums
{
    public enum TechnicalServiceStatus
    {
        Pending = 1,          // İşleme başlanmadı / Beklemede
        InProgress = 2,       // Başlandı / Aktif İşlemde
        Completed = 3,        // Tamamlandı
        AwaitingReview = 4,   // Revizyon Bekliyor (Geri dönüş durumu)
        Cancelled = 5         // İptal Edildi
    }
}
