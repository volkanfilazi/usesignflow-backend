namespace DynamicFormBuilder.Repositories.Auth
{
    public interface IAuthRepository
    {
        Task<AuthDefinition?> GetByEmailAsync(string email);
        Task<AuthDefinition?> GetByIdAsync(string id);
        Task<AuthDefinition?> GetByExternalLoginAsync(string provider, string providerUserId);
        Task CreateAsync(AuthDefinition user);
        Task UpdateAsync(AuthDefinition user);
    }
}
