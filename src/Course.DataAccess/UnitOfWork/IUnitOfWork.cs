using Course.DataAccess.Repositories;

namespace Course.DataAccess.UnitOfWork;

public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
