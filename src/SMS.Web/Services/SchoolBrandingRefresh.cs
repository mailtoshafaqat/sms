namespace SMS.Web.Services;

public sealed class SchoolBrandingRefresh
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
