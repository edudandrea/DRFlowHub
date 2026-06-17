namespace UniFlowHub.Api.Data.Interfaces
{
    public interface IBaseRepo<T>
    {
        IQueryable<T> Query();

        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);
        void Save();


    }
}