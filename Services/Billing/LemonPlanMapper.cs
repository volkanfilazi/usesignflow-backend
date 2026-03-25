using DynamicFormBuilder.Models.Billing;
using Microsoft.Extensions.Options;

namespace DynamicFormBuilder.Services.Billing;

public class LemonPlanMapper : ILemonPlanMapper
{
    private readonly LemonOptions _options;

    public LemonPlanMapper(IOptions<LemonOptions> options)
    {
        _options = options.Value;
    }

    public PlanCode MapVariantToPlan(string variantId)
    {
        if (variantId == _options.ProMonthlyVariantId)
            return PlanCode.Pro;

        if (variantId == _options.BusinessMonthlyVariantId)
            return PlanCode.Business;

        throw new InvalidOperationException($"Unknown Lemon variant id: {variantId}");
    }
}