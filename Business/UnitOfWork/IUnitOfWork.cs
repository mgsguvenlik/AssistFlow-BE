using Data.Abstract;

namespace Business.UnitOfWork
{
    /// <summary>
    /// Abstraction of Unit Of Work pattern
    /// </summary>
    public interface IUnitOfWork
    {
        IRepository Repository { get; }
    }
}
