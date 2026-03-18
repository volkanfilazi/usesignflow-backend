using DynamicFormBuilder.Models;
using MongoDB.Driver;

namespace DynamicFormBuilder.Services
{
    public class AgreementTemplateRepository
    {
        private readonly IMongoCollection<AgreementTemplate> _agreements;

        public AgreementTemplateRepository(IMongoDatabase database)
        {
            _agreements = database.GetCollection<AgreementTemplate>("agreements");
        }

        public async Task<AgreementTemplate?> GetByIdAsync(string id)
        {
            return await _agreements.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<AgreementTemplate>> GetByOwnerUserIdAsync(string ownerUserId)
        {
            return await _agreements.Find(x => x.OwnerUserId == ownerUserId).ToListAsync();
        }

        public async Task CreateAsync(AgreementTemplate agreement)
        {
            await _agreements.InsertOneAsync(agreement);
        }

        public async Task UpdateAsync(AgreementTemplate agreement)
        {
            await _agreements.ReplaceOneAsync(x => x.Id == agreement.Id, agreement);
        }

        public Task DeleteAsync(string id) =>
       _agreements.DeleteOneAsync(x => x.Id == id);
    }
}