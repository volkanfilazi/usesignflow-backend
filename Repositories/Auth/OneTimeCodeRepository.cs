using MongoDB.Driver;

namespace DynamicFormBuilder.Repositories.Auth
{
    public class OneTimeCodeRepository : IOneTimeCodeRepository
    {
        private readonly IMongoCollection<OneTimeCode> _collection;

        public OneTimeCodeRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<OneTimeCode>("one_time_codes");
        }

        public async Task CreateAsync(OneTimeCode code)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));

            await _collection.InsertOneAsync(code);
        }

        public async Task InvalidateActiveCodesByTargetAsync(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Target is required", nameof(target));

            var filter = Builders<OneTimeCode>.Filter.And(
                Builders<OneTimeCode>.Filter.Eq(x => x.Target, target),
                Builders<OneTimeCode>.Filter.Eq(x => x.IsUsed, false),
                Builders<OneTimeCode>.Filter.Gt(x => x.ExpiresAtUtc, DateTime.UtcNow)
            );

            var update = Builders<OneTimeCode>.Update
                .Set(x => x.IsUsed, true)
                .Set(x => x.VerifiedAtUtc, DateTime.UtcNow);

            await _collection.UpdateManyAsync(filter, update);
        }

        public async Task<OneTimeCode?> GetLatestActiveBySubmissionIdAndTargetAsync(string submissionId, string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Target is required", nameof(target));

            var filter = Builders<OneTimeCode>.Filter.And(
                Builders<OneTimeCode>.Filter.Eq(x => x.Target, target),
                Builders<OneTimeCode>.Filter.Eq(x => x.IsUsed, false),
                Builders<OneTimeCode>.Filter.Gt(x => x.ExpiresAtUtc, DateTime.UtcNow)
            );

            return await _collection
                .Find(filter)
                .SortByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(OneTimeCode code)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));

            var filter = Builders<OneTimeCode>.Filter.Eq(x => x.Id, code.Id);

            await _collection.ReplaceOneAsync(filter, code);
        }
    }
}
