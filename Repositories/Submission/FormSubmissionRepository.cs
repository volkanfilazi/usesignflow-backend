using DynamicFormBuilder.Models.Common.Query;
using DynamicFormBuilder.Models.Submission;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;
using QuestPDF.Helpers;
using SendGrid.Helpers.Mail;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
namespace DynamicFormBuilder.Repositories.Submission;

public class FormSubmissionRepository : IFormSubmissionRepository
{
    private readonly IMongoCollection<FormSubmission> _collection;

    public FormSubmissionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<FormSubmission>("formSubmissions");
        CreateIndexesAsync().GetAwaiter().GetResult();
    }

    private async Task CreateIndexesAsync()
    {
        var reminderIndex = new CreateIndexModel<FormSubmission>(
            Builders<FormSubmission>.IndexKeys
                .Ascending(x => x.Status)
                .Ascending(x => x.ReminderEnabled)
                .Ascending(x => x.ExternalConfirmed)
                .Ascending(x => x.NextReminderAtUtc)
        );

        var createdByIndex = new CreateIndexModel<FormSubmission>(
            Builders<FormSubmission>.IndexKeys
                .Ascending(x => x.CreatedByUserId)
        );

        await _collection.Indexes.CreateManyAsync(new[]
        {
            reminderIndex,
            createdByIndex
        });
    }

    public async Task<List<FormSubmission>> GetByUserIdAsync(string userId)
    {
        return await _collection
            .Find(x => x.CreatedByUserId == userId)
            .ToListAsync();
    }

    public async Task CreateAsync(FormSubmission submission) =>
        await _collection.InsertOneAsync(submission);

    public async Task<FormSubmission?> GetByIdAsync(string id) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<PagedResult<FormSubmission>> GetMineAsync(
    string userId,
    string? search,
    int page,
    int pageSize,
    string? sortField,
    string? sortDir)
    {
        if (page < 1)
            page = 1;

        if (pageSize < 1)
            pageSize = 20;

        var filter = Builders<FormSubmission>.Filter.Eq(x => x.CreatedByUserId, userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var escapedSearch = Regex.Escape(search);
            var regex = new BsonRegularExpression(escapedSearch, "i");

            var filters = new List<FilterDefinition<FormSubmission>>
        {
            Builders<FormSubmission>.Filter.Regex(x => x.FormName, regex),
                Builders<FormSubmission>.Filter.Regex(x => x.ExternalRecipientEmail, regex)
        };

            if (Enum.TryParse<SubmissionStatus>(search, true, out var parsedStatus))
            {
                filters.Add(
                    Builders<FormSubmission>.Filter.Eq(x => x.Status, parsedStatus)
                );
            }

            var searchFilter = Builders<FormSubmission>.Filter.Or(filters);

            filter = Builders<FormSubmission>.Filter.And(filter, searchFilter);
        }

        var totalCount = await _collection.CountDocumentsAsync(filter);

        var sortBuilder = Builders<FormSubmission>.Sort;
        var isAsc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        SortDefinition<FormSubmission> sort = sortBuilder.Descending(x => x.CreatedAtUtc);

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            sort = sortField switch
            {
                "formName" => isAsc
                    ? sortBuilder.Ascending(x => x.FormName)
                    : sortBuilder.Descending(x => x.FormName),

                "externalRecipientEmail" => isAsc
                    ? sortBuilder.Ascending(x => x.ExternalRecipientEmail)
                    : sortBuilder.Descending(x => x.ExternalRecipientEmail),

                "status" => isAsc
                    ? sortBuilder.Ascending(x => x.Status)
                    : sortBuilder.Descending(x => x.Status),

                "formId" => isAsc
                    ? sortBuilder.Ascending(x => x.FormId)
                    : sortBuilder.Descending(x => x.FormId),

                "createdAt" => isAsc
                    ? sortBuilder.Ascending(x => x.CreatedAtUtc)
                    : sortBuilder.Descending(x => x.CreatedAtUtc),

                _ => sortBuilder.Descending(x => x.CreatedAtUtc)
            };
        }

        var items = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PagedResult<FormSubmission>
        {
            Items = items,
            PageIndex = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<SubmissionSummaryResponse> GetMineSummaryAsync(
        string userId,
        DateTime? start,
        DateTime? end)
    {
        var filterBuilder = Builders<FormSubmission>.Filter;

        var filter = filterBuilder.Eq(x => x.CreatedByUserId, userId);

        if (start.HasValue)
        {
            filter &= filterBuilder.Gte(x => x.CreatedAtUtc, start.Value);
        }

        if (end.HasValue)
        {
            filter &= filterBuilder.Lt(x => x.CreatedAtUtc, end.Value);
        }

        var totalCount = await _collection.CountDocumentsAsync(filter);

        var pendingCount = await _collection.CountDocumentsAsync(
            filter & filterBuilder.Eq(x => x.Status, SubmissionStatus.Pending));

        var completedCount = await _collection.CountDocumentsAsync(
            filter & filterBuilder.Eq(x => x.Status, SubmissionStatus.Completed));

        var completionRate = totalCount == 0
            ? 0
            : Math.Round((double)completedCount / totalCount * 100, 1);

        return new SubmissionSummaryResponse
        {
            TotalCount = totalCount,
            PendingCount = pendingCount,
            CompletedCount = completedCount,
            CompletionRate = completionRate
        };
    }

    public async Task<SubmissionTrendResponse> GetMineTrendAsync(
    string userId,
    DateTime? start,
    DateTime? end)
    {
        var endUtc = end ?? DateTime.UtcNow;
        var startUtc = start ?? endUtc.AddDays(-29);

        if (startUtc > endUtc)
            throw new ArgumentException("Start date cannot be greater than end date.");

        var totalDays = (endUtc - startUtc).TotalDays;

        var granularity =
            totalDays <= 60 ? "day" :
            totalDays <= 180 ? "week" :
            "month";

        var createdMap = await GetTrendCountsAsync(
            userId,
            startUtc,
            endUtc,
            granularity,
            useCompletedDate: false);

        var completedMap = await GetTrendCountsAsync(
            userId,
            startUtc,
            endUtc,
            granularity,
            useCompletedDate: true);

        var labels = BuildLabels(startUtc, endUtc, granularity);

        var points = labels.Select(label => new SubmissionTrendPointResponse
        {
            Label = label,
            Created = createdMap.TryGetValue(label, out var created) ? created : 0,
            Completed = completedMap.TryGetValue(label, out var completed) ? completed : 0
        }).ToList();

        return new SubmissionTrendResponse
        {
            Granularity = granularity,
            Points = points
        };
    }

    private async Task<Dictionary<string, int>> GetTrendCountsAsync(
    string userId,
    DateTime startUtc,
    DateTime endUtc,
    string granularity,
    bool useCompletedDate)
    {
        var match = new BsonDocument
    {
        { "CreatedByUserId", userId }
    };

        string dateField;

        if (useCompletedDate)
        {
            match.Add("Status", (int)SubmissionStatus.Completed);

            // Şu an CompletedAtUtc yoksa geçici olarak UpdatedAtUtc kullanıyoruz.
            // İleride CompletedAtUtc eklersen bunu değiştir.
            dateField = "$UpdatedAtUtc";
            match.Add("UpdatedAtUtc", new BsonDocument
        {
            { "$gte", startUtc },
            { "$lt", endUtc }
        });
        }
        else
        {
            dateField = "$CreatedAtUtc";
            match.Add("CreatedAtUtc", new BsonDocument
        {
            { "$gte", startUtc },
            { "$lt", endUtc }
        });
        }

        var groupId = granularity switch
        {
            "day" => new BsonDocument
        {
            { "$dateToString", new BsonDocument
                {
                    { "format", "%Y-%m-%d" },
                    { "date", dateField }
                }
            }
        },

            "week" => new BsonDocument
        {
            { "$dateToString", new BsonDocument
                {
                    { "format", "%Y-%m-%d" },
                    {
                        "date",
                        new BsonDocument
                        {
                            {
                                "$dateTrunc",
                                new BsonDocument
                                {
                                    { "date", dateField },
                                    { "unit", "week" },
                                    { "startOfWeek", "monday" }
                                }
                            }
                        }
                    }
                }
            }
        },

            _ => new BsonDocument
        {
            { "$dateToString", new BsonDocument
                {
                    { "format", "%Y-%m" },
                    { "date", dateField }
                }
            }
        }
        };

        var pipeline = new[]
        {
        new BsonDocument("$match", match),
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", groupId },
            { "count", new BsonDocument("$sum", 1) }
        }),
        new BsonDocument("$sort", new BsonDocument("_id", 1))
    };

        var result = await _collection
            .Aggregate<BsonDocument>(pipeline)
            .ToListAsync();

        return result.ToDictionary(
            x => x["_id"].AsString,
            x => x["count"].ToInt32()
        );
    }

    private List<string> BuildLabels(DateTime startUtc, DateTime endUtc, string granularity)
    {
        var labels = new List<string>();

        if (granularity == "day")
        {
            var cursor = startUtc.Date;
            while (cursor < endUtc.Date)
            {
                labels.Add(cursor.ToString("yyyy-MM-dd"));
                cursor = cursor.AddDays(1);
            }

            return labels;
        }

        if (granularity == "week")
        {
            var cursor = StartOfWeek(startUtc.Date);
            while (cursor < endUtc.Date)
            {
                labels.Add(cursor.ToString("yyyy-MM-dd"));
                cursor = cursor.AddDays(7);
            }

            return labels;
        }

        var monthCursor = new DateTime(startUtc.Year, startUtc.Month, 1);
        var monthEnd = new DateTime(endUtc.Year, endUtc.Month, 1).AddMonths(1);

        while (monthCursor < monthEnd)
        {
            labels.Add(monthCursor.ToString("yyyy-MM"));
            monthCursor = monthCursor.AddMonths(1);
        }

        return labels;
    }

    private DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }

    public async Task UpdateAsync(FormSubmission submission) =>
        await _collection.ReplaceOneAsync(x => x.Id == submission.Id, submission);

    public async Task<long> CountCreatedInPeriodAsync(string userId, DateTime periodStartUtc, DateTime periodEndUtc)
    {
        var filter = Builders<FormSubmission>.Filter.And(
            Builders<FormSubmission>.Filter.Eq(x => x.CreatedByUserId, userId),
            Builders<FormSubmission>.Filter.Gte(x => x.CreatedAtUtc, periodStartUtc),
            Builders<FormSubmission>.Filter.Lt(x => x.CreatedAtUtc, periodEndUtc)
        );

        return await _collection.CountDocumentsAsync(filter);
    }

    public async Task<List<FormSubmission>> GetReminderDueSubmissionsAsync(DateTime nowUtc) =>
    await _collection.Find(x =>
        x.Status == SubmissionStatus.Pending &&
        x.ReminderEnabled &&
        x.NextReminderAtUtc != null &&
        x.NextReminderAtUtc <= nowUtc &&
        x.ExternalConfirmed == false &&
        x.ReminderIntervalDays != null &&
        x.MaxReminderCount != null &&
        x.ReminderCount < x.MaxReminderCount
    ).ToListAsync();
}