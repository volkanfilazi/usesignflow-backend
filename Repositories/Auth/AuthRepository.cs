using DynamicFormBuilder.Models;
using MongoDB.Driver;

namespace DynamicFormBuilder.Repositories.Auth
{
    public class AuthRepository : IAuthRepository
    {
        private readonly IMongoCollection<AuthDefinition> _users;

        public AuthRepository(IMongoDatabase database)
        {
            _users = database.GetCollection<AuthDefinition>("users");
        }

        public async Task<AuthDefinition?> GetByEmailAsync(string email)
        {
            email = email.Trim().ToLowerInvariant();

            return await _users.Find(x =>
                !x.IsDeleted &&
                x.Email == email)
                .FirstOrDefaultAsync();
        }

        public async Task<AuthDefinition?> GetByIdAsync(string id)
        {
            return await _users.Find(x =>
                !x.IsDeleted &&
                x.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<AuthDefinition?> GetByExternalLoginAsync(string provider, string providerUserId)
        {
            return await _users.Find(x =>
                !x.IsDeleted &&
                x.ExternalLogins.Any(e =>
                    e.Provider == provider &&
                    e.ProviderUserId == providerUserId))
                .FirstOrDefaultAsync();
        }

        public async Task CreateAsync(AuthDefinition user)
        {
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.CreatedAtUtc = DateTime.UtcNow;

            await _users.InsertOneAsync(user);
        }

        public async Task UpdateAsync(AuthDefinition user)
        {
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _users.ReplaceOneAsync(x => x.Id == user.Id, user);
        }
    }
}