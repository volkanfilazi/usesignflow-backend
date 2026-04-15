namespace DynamicFormBuilder.Models.Submission
{
    public class SubmissionSummaryResponse
    {
        public long TotalCount { get; set; }
        public long PendingCount { get; set; }
        public long CompletedCount { get; set; }
        public double CompletionRate { get; set; }
    }

    public class SubmissionTrendPointResponse
    {
        public string Label { get; set; } = string.Empty;
        public int Created { get; set; }
        public int Completed { get; set; }
    }

    public class SubmissionTrendResponse
    {
        public string Granularity { get; set; } = "day";
        public List<SubmissionTrendPointResponse> Points { get; set; } = new();
    }
}
