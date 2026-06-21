namespace SMS.Web.Services;

public record ConfirmDialogRequest(
    string Title,
    string Message,
    string ConfirmLabel,
    string CancelLabel,
    TaskCompletionSource<bool> Completion);

public class ConfirmDialogService
{
    public event Action<ConfirmDialogRequest>? OnShow;

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmLabel = "Delete",
        string cancelLabel = "Cancel")
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        OnShow?.Invoke(new ConfirmDialogRequest(title, message, confirmLabel, cancelLabel, completion));
        return completion.Task;
    }
}
