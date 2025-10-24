namespace Core.Enums
{
    public enum ServicesCostStatus : int
    {
        Unknown = 0,        // Belirtilmemiş
        NotRequired = 1,    // Ücret gerekmiyor
        Chargeable = 2,     // Ücretli (müşteri öder)
        Maintenance = 3,    // Bakım kapsamında
    }
}
