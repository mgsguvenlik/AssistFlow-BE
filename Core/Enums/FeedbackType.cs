namespace Core.Enums
{
    /// <summary>
    /// Kullanıcı geri bildirimlerinin tipini belirtir
    /// </summary>
    public enum FeedbackType
    {
        /// <summary>
        /// Genel öneri
        /// </summary>
        Suggestion = 0,

        /// <summary>
        /// Yeni özellik talebi
        /// </summary>
        FeatureRequest = 1,

        /// <summary>
        /// Hata bildirimi
        /// </summary>
        BugReport = 2,

        /// <summary>
        /// Sorun bildirimi
        /// </summary>
        Issue = 3,

        /// <summary>
        /// İyileştirme önerisi
        /// </summary>
        Improvement = 4,

        /// <summary>
        /// Diğer
        /// </summary>
        Other = 5
    }
}