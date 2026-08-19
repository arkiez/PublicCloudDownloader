namespace PublicCloudDownloader.App.Updates;

public enum UpdateCheckStatus
{
    NoUpdate,
    Available,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    UpdateRelease? Release,
    string? ErrorMessage,
    bool UserInitiated);

public sealed class UpdateUiCoordinator
{
    private readonly IUpdateClient _updateClient;
    private readonly SemaphoreSlim _checkGate = new(1, 1);

    public UpdateUiCoordinator(IUpdateClient updateClient) => _updateClient = updateClient;

    public async Task<UpdateCheckResult> CheckAsync(bool userInitiated, CancellationToken cancellationToken)
    {
        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            var currentVersion = typeof(UpdateUiCoordinator).Assembly.GetName().Version ?? new Version(0, 0, 0);
            var release = await _updateClient.CheckAsync(currentVersion, cancellationToken);
            return release is null
                ? new(UpdateCheckStatus.NoUpdate, null, null, userInitiated)
                : new(UpdateCheckStatus.Available, release, null, userInitiated);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new(UpdateCheckStatus.Failed, null, ex.Message, userInitiated);
        }
        finally
        {
            _checkGate.Release();
        }
    }
}
