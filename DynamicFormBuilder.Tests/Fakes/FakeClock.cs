using DynamicFormBuilder.Services.Common;

namespace DynamicFormBuilder.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; }
}