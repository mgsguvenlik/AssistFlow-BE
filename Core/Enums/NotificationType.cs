namespace Core.Enums
{
    public enum NotificationType
    {
        Unknown = 0,
        WorkflowAssigned = 1,        // Bir talep kullanıcıya/role atandı
        WorkflowStepChanged = 2,     // Adım değişti (örn: SR -> WH)
        WorkflowSentBack = 3,        // Geri gönderildi (review)
        GenericInfo = 10,            // Serbest bilgilendirme
        GenericWarning = 11,
        GenericError = 12
    }
}
