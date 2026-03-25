using DynamicFormBuilder.Models.Billing;
using MongoDB.Driver;

public class BillingWebhookEventRepository
{
    private readonly IMongoCollection<BillingWebhookEvent> _collection;

    public BillingWebhookEventRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<BillingWebhookEvent>("billing_webhook_events");
    }

    public async Task<bool> ExistsAsync(string eventIdempotencyKey)
    {
        return await _collection.Find(x => x.EventIdempotencyKey == eventIdempotencyKey).AnyAsync();
    }

    public async Task CreateAsync(BillingWebhookEvent webhookEvent)
    {
        await _collection.InsertOneAsync(webhookEvent);
    }
}