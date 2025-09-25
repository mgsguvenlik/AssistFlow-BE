namespace Model.Interfaces
{
    /// <summary>
    /// This interface implemented Deletion Date and Is Deleted property for entity
    /// </summary>
    public interface ISoftDeletable
    {
        /// <summary>
        /// Is Deleted
        /// </summary>
        bool IsDeleted { get; set; }
    }
}
