
using DynamicFormBuilder.Models.Billing;

namespace DynamicFormBuilder.Services.Billing;

public interface IPlanEntitlementService
{
    PlanEntitlements Get(PlanCode planCode);
}