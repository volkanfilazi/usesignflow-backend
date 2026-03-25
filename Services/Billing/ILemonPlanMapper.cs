using DynamicFormBuilder.Models.Billing;

namespace DynamicFormBuilder.Services.Billing;

public interface ILemonPlanMapper
{
    PlanCode MapVariantToPlan(string variantId);
}