using MongoDB.Driver;

namespace DynamicFormBuilder.Services
{
    public class FormRepository
    {
        private readonly IMongoCollection<FormDefinition> _forms;

        public FormRepository(IConfiguration cfg)
        {
            var client = new MongoClient(cfg["MongoDb:ConnectionString"]);
            var db = client.GetDatabase(cfg["MongoDb:DatabaseName"]);
            _forms = db.GetCollection<FormDefinition>("forms");
        }

        public async Task<List<FormDefinition>> GetByUserIdAsync(string userId)
        {
            return await _forms
                .Find(x => x.OwnerUserId == userId)
                .ToListAsync();
        }

        public Task<List<FormDefinition>> GetAllAsync() =>
            _forms.Find(_ => true).ToListAsync();

        public Task<FormDefinition> GetByIdAsync(string id) =>
         _forms.Find(x => x.Id == id).FirstOrDefaultAsync();

        public Task CreateAsync(FormDefinition form) =>
        _forms.InsertOneAsync(form);

        public Task UpdateAsync(string id, FormDefinition updated) =>
        _forms.ReplaceOneAsync(x => x.Id == id, updated);

        public Task DeleteAsync(string id) =>
        _forms.DeleteOneAsync(x => x.Id == id);

        public async Task<long> CountByUserIdAsync(string userId)
        {
            return await _forms.CountDocumentsAsync(x => x.OwnerUserId == userId);
        }
    }
}
