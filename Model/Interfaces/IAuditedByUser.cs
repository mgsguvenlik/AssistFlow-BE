namespace Model.Interfaces
{
    /// <summary>Oluşturan / güncelleyen kullanıcı alanları için arayüz.</summary>
    public interface IAuditedByUser
    {
        long CreatedUser { get; set; }
        long? UpdatedUser { get; set; }
    }
}
