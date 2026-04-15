using MongoDB.Driver;
namespace DynamicFormBuilder.Repositories.Submission;

public class SubmissionAccessTokenRepository : ISubmissionAccessTokenRepository
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

    public async Task RevokeActiveTokensBySubmissionIdAsync(string submissionId)
    {
        var filter = Builders<SubmissionAccessToken>.Filter.And(
            Builders<SubmissionAccessToken>.Filter.Eq(x => x.SubmissionId, submissionId),
            Builders<SubmissionAccessToken>.Filter.Eq(x => x.IsRevoked, false)
        );

        var now = DateTime.UtcNow;

        var update = Builders<SubmissionAccessToken>.Update
            .Set(x => x.IsRevoked, true)
            .Set(x => x.UsedAtUtc, now)
            .Set(x => x.RevokedAtUtc, now);

        await _collection.UpdateManyAsync(filter, update);
    }

    public async Task DeleteBySubmissionIdAsync(string submissionId)
    {
        await _collection.DeleteManyAsync(x => x.SubmissionId == submissionId);
    }
}