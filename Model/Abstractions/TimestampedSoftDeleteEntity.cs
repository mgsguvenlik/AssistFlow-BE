using Model.Interfaces;

namespace Model.Abstractions
{
    /// <summary>
    /// 3) CreatedDate, UpdatedDate ve IsDeleted içeren base
    /// </summary>
    public abstract class TimestampedSoftDeleteEntity : BaseEntity, ITimestamped, ISoftDeletable
    {
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; }
    }
}
