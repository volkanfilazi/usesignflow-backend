using DynamicFormBuilder.Models.Billing;
using MongoDB.Driver;

namespace DynamicFormBuilder.Repositories.Billing;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly IMongoCollection<UserSubscription> _collection;

    public SubscriptionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<UserSubscription>("user_subscriptions");
    }

    public async Task<UserSubscription?> GetByUserIdAsync(string userId)
    {
        return await _collection.Find(x => x.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task<UserSubscription?> GetByLemonSubscriptionIdAsync(string lemonSubscriptionId)
    {
        return await _collection.Find(x => x.LemonSubscriptionId == lemonSubscriptionId).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(UserSubscription subscription)
    {
        await _collection.InsertOneAsync(subscription);
    }

    public async Task UpsertByUserIdAsync(UserSubscription subscription)
    {
        subscription.UpdatedAtUtc = DateTime.UtcNow;

        var filter = Builders<UserSubscription>.Filter.Eq(x => x.UserId, subscription.UserId);

        await _collection.ReplaceOneAsync(
            filter,
            subscription,
            new ReplaceOptions { IsUpsert = true });
    }
}