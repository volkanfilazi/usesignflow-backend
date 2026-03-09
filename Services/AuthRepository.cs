using DynamicFormBuilder.Models;
using MongoDB.Driver;

namespace DynamicFormBuilder.Services
{
    public class AuthRepository
    {
        private readonly IMongoCollection<AuthDefinition> _users;

        public AuthRepository(IMongoDatabase database)
        {
            _users = database.GetCollection<AuthDefinition>("users");
        }

        public async Task<AuthDefinition?> GetByEmailAsync(string email)
        {
            return await _users.Find(x => x.Email == email).FirstOrDefaultAsync();
        }

        public async Task<AuthDefinition?> GetByIdAsync(string id)
        {
            return await _users.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(AuthDefinition user)
        {
            await _users.InsertOneAsync(user);
        }

        public async Task UpdateAsync(AuthDefinition user)
        {
            await _users.ReplaceOneAsync(x => x.Id == user.Id, user);
        }
    }
}
