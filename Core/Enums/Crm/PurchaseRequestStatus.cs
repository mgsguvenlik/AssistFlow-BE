namespace Core.Enums.Crm
{
    public enum PurchaseRequestStatus
    {
        /// <summary>
        /// Henüz workflow başlatılmadı.
        /// </summary>
        Draft = 1,

        /// <summary>
        /// Satın alma süreci devam ediyor.
        /// </summary>
        InProgress = 2,

        /// <summary>
        /// Talep sahibinden revizyon bekleniyor.
        /// </summary>
        RevisionRequired = 3,

        /// <summary>
        /// Süreç başarıyla tamamlandı.
        /// </summary>
        Completed = 4,

        /// <summary>
        /// Talep reddedilerek kapatıldı.
        /// </summary>
        Rejected = 5,

        /// <summary>
        /// Talep iptal edildi.
        /// </summary>
        Cancelled = 6
    }
}