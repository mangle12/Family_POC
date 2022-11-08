namespace Family_POC.Interface
{
    public interface IDbService
    {
        Task<T> GetAsync<T>(string command, object parms);

        Task<List<T>> GetAllAsync<T>(string command, object parms);

        Task<int> ExecuteAsync(string command, object parms);
    }
}
