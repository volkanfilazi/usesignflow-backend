using MongoDB.Driver;
namespace DynamicFormBuilder.Services;

public class FormSubmissionRepository
{
    private readonly IMongoCollection<FormSubmission> _collection;

    public FormSubmissionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<FormSubmission>("formSubmissions");
    }

    public async Task CreateAsync(FormSubmission submission) =>
        await _collection.InsertOneAsync(submission);

    public async Task<FormSubmission?> GetByIdAsync(string id) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task UpdateAsync(string id, FormSubmission submission) =>
        await _collection.ReplaceOneAsync(x => x.Id == id, submission);

    public async Task<FormSubmission?> GetDraftByFormAndUserAsync(string formId, string userId) =>
        await _collection.Find(x =>
            x.FormId == formId &&
            x.CreatedByUserId == userId &&
            x.Status == SubmissionStatus.Draft)
        .FirstOrDefaultAsync();
}