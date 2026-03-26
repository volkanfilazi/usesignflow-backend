namespace DynamicFormBuilder.Models.Billing.Requests;

public class CreateCheckoutRequest
{
    public string PlanCode { get; set; } = default!;
}