namespace Model.Interfaces
{
    /// <summary>
    /// This interface implemented Deletion Date and Is Deleted property for entity
    /// </summary>
    public interface ISoftDeleteModel
    {
        /// <summary>
        /// Is Deleted
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
