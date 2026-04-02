namespace Core.Enums
{
    /// <summary>
    /// Kullanıcı geri bildirimlerinin durumunu belirtir
    /// </summary>
    public enum FeedbackStatus
    {
        /// <summary>
        /// Geri bildirim oluşturuldu, henüz işleme alınmadı
        /// </summary>
        Created = 0,

        /// <summary>
        /// Geri bildirim inceleniyor
        /// </summary>
        UnderReview = 1,

        /// <summary>
        /// Geri bildirim kabul edildi, geliştirme süreci başladı
        /// </summary>
        InProgress = 2,

        /// <summary>
        /// Geri bildirim tamamlandı/çözüldü
        /// </summary>
        Completed = 3,

        /// <summary>
        /// Geri bildirim reddedildi
        /// </summary>
        Rejected = 4,

        /// <summary>
        /// Geri bildirim kapatıldı
        /// </summary>
        Closed = 5
    }
}