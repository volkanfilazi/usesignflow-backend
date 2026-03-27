namespace DynamicFormBuilder.Repositories.Form
{
    public interface IFormRepository
    {
        Task<List<FormDefinition>> GetByUserIdAsync(string userId);
        Task<List<FormDefinition>> GetAllAsync();
        Task<FormDefinition> GetByIdAsync(string id);
        Task CreateAsync(FormDefinition form);
        Task UpdateAsync(string id, FormDefinition updated);
        Task DeleteAsync(string id);
        Task<long> CountByUserIdAsync(string userId);
    }
}
