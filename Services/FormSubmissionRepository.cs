using MongoDB.Driver;
namespace DynamicFormBuilder.Services;

public class FormSubmissionRepository
{
    private readonly IMongoCollection<FormSubmission> _collection;

    public FormSubmissionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<FormSubmission>("formSubmissions");
    }

    public async Task<List<FormSubmission>> GetByUserIdAsync(string userId)
    {
        return await _collection
            .Find(x => x.CreatedByUserId == userId)
            .ToListAsync();
    }

    public async Task CreateAsync(FormSubmission submission) =>
        await _collection.InsertOneAsync(submission);

    public async Task<FormSubmission?> GetByIdAsync(string id) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task UpdateAsync(FormSubmission submission) =>
    await _collection.ReplaceOneAsync(x => x.Id == submission.Id, submission);

    public async Task<long> CountCreatedInPeriodAsync(string userId, DateTime periodStartUtc, DateTime periodEndUtc)
    {
        var filter = Builders<FormSubmission>.Filter.And(
            Builders<FormSubmission>.Filter.Eq(x => x.CreatedByUserId, userId),
            Builders<FormSubmission>.Filter.Gte(x => x.CreatedAtUtc, periodStartUtc),
            Builders<FormSubmission>.Filter.Lt(x => x.CreatedAtUtc, periodEndUtc)
        );

        return await _collection.CountDocumentsAsync(filter);
    }
}