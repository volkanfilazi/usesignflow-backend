using DynamicFormBuilder.Models.Billing;
using MongoDB.Driver;

namespace DynamicFormBuilder.Repositories.Billing;

public class EmailLogRepository
{
    private readonly IMongoCollection<EmailLog> _collection;

    public EmailLogRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EmailLog>("email_logs");
    }

    public async Task CreateAsync(EmailLog emailLog)
    {
        await _collection.InsertOneAsync(emailLog);
    }

    public async Task UpdateAsync(string id, EmailLog emailLog)
    {
        await _collection.ReplaceOneAsync(x => x.Id == id, emailLog);
    }

    public async Task<List<EmailLog>> GetByUserIdAsync(string userId)
    {
        return await _collection
            .Find(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<long> CountSentInPeriodAsync(string userId, DateTime periodStartUtc, DateTime periodEndUtc)
    {
        var filter = Builders<EmailLog>.Filter.And(
            Builders<EmailLog>.Filter.Eq(x => x.UserId, userId),
            Builders<EmailLog>.Filter.Eq(x => x.Status, EmailLogStatus.Sent),
            Builders<EmailLog>.Filter.Gte(x => x.CreatedAtUtc, periodStartUtc),
            Builders<EmailLog>.Filter.Lt(x => x.CreatedAtUtc, periodEndUtc)
        );

        return await _collection.CountDocumentsAsync(filter);
    }
}