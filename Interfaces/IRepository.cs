public interface IRepository<T> where T : class
{
    T? Find(object id);
    void Update(T entity);
    ICollection<T> ListAll();
    void Add(T entity);
    void Remove(T entity);
}
