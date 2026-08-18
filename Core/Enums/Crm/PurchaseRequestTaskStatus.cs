namespace Core.Enums.Crm
{
    public enum PurchaseRequestTaskStatus
    {
        /// <summary>
        /// Kullanıcı veya rol tarafından işlem bekliyor.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Görev tamamlandı.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Workflow başka yöne ilerlediği için görev iptal edildi.
        /// </summary>
        Cancelled = 3
    }
}