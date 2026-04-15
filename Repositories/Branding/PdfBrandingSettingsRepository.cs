namespace DynamicFormBuilder.Repositories.Branding
{
    using DynamicFormBuilder.Models.Pdf;
    using MongoDB.Driver;

    public class PdfBrandingSettingsRepository : IPdfBrandingSettingsRepository
    {
        private readonly IMongoCollection<PdfBrandingSettings> _collection;

        public PdfBrandingSettingsRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<PdfBrandingSettings>("PdfBrandingSettings");
        }

        public async Task<PdfBrandingSettings?> GetByUserIdAsync(string userId)
        {
            return await _collection.Find(x => x.UserId == userId).FirstOrDefaultAsync();
        }

        public async Task UpsertAsync(PdfBrandingSettings settings)
        {
            settings.UpdatedAtUtc = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(settings.Id))
                settings.Id = Guid.NewGuid().ToString("N");

            var existing = await GetByUserIdAsync(settings.UserId);

            if (existing is null)
            {
                settings.CreatedAtUtc = DateTime.UtcNow;
                await _collection.InsertOneAsync(settings);
                return;
            }

            settings.Id = existing.Id;
            settings.CreatedAtUtc = existing.CreatedAtUtc;

            await _collection.ReplaceOneAsync(
                x => x.UserId == settings.UserId,
                settings,
                new ReplaceOptions { IsUpsert = true });
        }
    }
}
