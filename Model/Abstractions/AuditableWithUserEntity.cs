using Model.Interfaces;

namespace Model.Abstractions
{
    /// <summary>
    /// 1) CreatedDate, UpdatedDate, CreatedUser, UpdatedUser ve IsDeleted içeren base
    /// </summary>

    public abstract class AuditableWithUserEntity : BaseEntity, ITimestamped, IAuditedByUser, ISoftDeletable
    {
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }

        // ASP.NET Identity defaultu string olduğu için string bıraktım (username veya userId tutabilirsiniz)
        public long CreatedUser { get; set; }
        public long UpdatedUser { get; set; }

        public bool IsDeleted { get; set; }
    }
}
