using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicFormBuilder.Models.Submission
{
    public class SubmissionSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int DefaultAccessTokenLifetimeDays { get; set; } = 3;

        public bool ReminderEnabledByDefault { get; set; } = false;

        public int DefaultReminderIntervalDays { get; set; } = 3;

        public int MaxReminderCount { get; set; } = 3;

        public DateTime UpdatedAtUtc { get; set; }
    }

    public class UpdateSubmissionSettingsRequest
    {
        public int DefaultAccessTokenLifetimeDays { get; set; }
        public bool ReminderEnabledByDefault { get; set; }
        public int DefaultReminderIntervalDays { get; set; }
        public int MaxReminderCount { get; set; }
    }

    public class SubmissionSettingsResponse
    {
        public int DefaultAccessTokenLifetimeDays { get; set; }
        public bool ReminderEnabledByDefault { get; set; }
        public int DefaultReminderIntervalDays { get; set; }
        public int MaxReminderCount { get; set; }
        public bool IsDefault { get; set; }
    }
}
