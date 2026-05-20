namespace Course.Domain.Abstractions;

public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void RemoveRange(IEnumerable<TEntity> entities);
}
