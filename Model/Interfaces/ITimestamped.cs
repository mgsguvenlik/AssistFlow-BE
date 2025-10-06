namespace Model.Interfaces
{
    /// <summary>Zaman damgaları için ortak arayüz.</summary>
    public interface ITimestamped
    {
        DateTimeOffset CreatedDate { get; set; }
        DateTimeOffset? UpdatedDate { get; set; }
    }
}
