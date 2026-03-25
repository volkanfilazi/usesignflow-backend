public class LemonOptions
{
    public string ApiKey { get; set; } = default!;
    public string WebhookSecret { get; set; } = default!;
    public string StoreUrl { get; set; } = default!;
    public string ProMonthlyVariantId { get; set; } = default!;
    public string BusinessMonthlyVariantId { get; set; } = default!;
    public string ProCheckoutUrl { get; set; } = default!;
    public string BusinessCheckoutUrl { get; set; } = default!;
}