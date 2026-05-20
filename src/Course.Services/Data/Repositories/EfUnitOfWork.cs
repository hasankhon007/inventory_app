using Course.Domain.Abstractions;

namespace Course.Services.Data.Repositories;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Dictionary<Type, object> _repositories = new();

    public EfUnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        var entityType = typeof(TEntity);
        if (_repositories.TryGetValue(entityType, out var repository))
        {
            return (IRepository<TEntity>)repository;
        }

        var createdRepository = new EfRepository<TEntity>(_dbContext);
        _repositories[entityType] = createdRepository;
        return createdRepository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
