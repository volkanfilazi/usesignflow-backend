namespace DynamicFormBuilder.Services.Common;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}