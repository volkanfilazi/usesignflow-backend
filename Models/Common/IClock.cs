namespace DynamicFormBuilder.Services.Common;

public interface IClock
{
    DateTime UtcNow { get; }
}