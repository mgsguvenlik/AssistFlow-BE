using Data.Abstract;

namespace Business.UnitOfWork
{
    /// <summary>
    /// Implementation of Unit of work pattern
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        public UnitOfWork(IRepository repository)
        {
            Repository = repository;
        }
        public IRepository Repository { get; }

    }
}
