using Model.Interfaces;

namespace Model.Abstractions
{
    /// <summary>
    ///  2) Sadece IsDeleted içeren base
    /// </summary>
    public abstract class SoftDeleteEntity : BaseEntity, ISoftDeletable
    {
        public bool IsDeleted { get; set; }
    }
}
