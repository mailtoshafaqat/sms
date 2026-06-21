namespace SMS.Web.Services;

public enum ToastLevel
{
    Success,
    Error,
    Info
}

public record ToastItem(Guid Id, string Message, ToastLevel Level, string? Title = null);

public class ToastService
{
    public event Action<ToastItem>? OnShow;

    public void Success(string message, string? title = null) => Show(message, ToastLevel.Success, title);

    public void Error(string message, string? title = null) => Show(message, ToastLevel.Error, title);

    public void Info(string message, string? title = null) => Show(message, ToastLevel.Info, title);

    private void Show(string message, ToastLevel level, string? title)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        OnShow?.Invoke(new ToastItem(Guid.NewGuid(), message, level, title));
    }
}
