using MongoDB.Driver;
namespace DynamicFormBuilder.Services;

public class SubmissionAccessTokenRepository
{
    private readonly IMongoCollection<SubmissionAccessToken> _collection;

    public SubmissionAccessTokenRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SubmissionAccessToken>("submissionAccessTokens");
    }

    public async Task CreateAsync(SubmissionAccessToken token)
    {
        await _collection.InsertOneAsync(token);
    }

    public async Task<SubmissionAccessToken?> GetByTokenHashAsync(string hash)
    {
        return await _collection
            .Find(x => x.TokenHash == hash && !x.IsRevoked)
            .FirstOrDefaultAsync();
    }
}