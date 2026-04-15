using DynamicFormBuilder.Models.Submission;
using MongoDB.Driver;

namespace DynamicFormBuilder.Repositories.Submission
{
    public class SubmissionSettingsRepository: ISubmissionSettingsRepository
    {
        private readonly IMongoCollection<SubmissionSettings> _collection;

        public SubmissionSettingsRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<SubmissionSettings>("submissionSettings");
        }

        public async Task<SubmissionSettings?> GetByUserIdAsync(string userId) =>
            await _collection.Find(x => x.UserId == userId).FirstOrDefaultAsync();

        public async Task CreateAsync(SubmissionSettings settings) =>
            await _collection.InsertOneAsync(settings);

        public async Task UpdateAsync(SubmissionSettings settings) =>
            await _collection.ReplaceOneAsync(x => x.UserId == settings.UserId, settings);
    }
}
