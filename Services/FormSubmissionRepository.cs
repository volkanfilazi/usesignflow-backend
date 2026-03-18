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
}