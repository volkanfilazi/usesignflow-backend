using MongoDB.Driver;

namespace DynamicFormBuilder.Services;

public class SignatureRequestRepository
{
    private readonly IMongoCollection<SignatureRequest> _collection;

    public SignatureRequestRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SignatureRequest>("signatureRequests");
    }

    public async Task CreateAsync(SignatureRequest request)
        => await _collection.InsertOneAsync(request);

    public async Task<SignatureRequest?> GetByTokenHashAsync(string tokenHash)
        => await _collection.Find(x => x.TokenHash == tokenHash).FirstOrDefaultAsync();

    public async Task UpdateAsync(string id, SignatureRequest request)
        => await _collection.ReplaceOneAsync(x => x.Id == id, request);
}