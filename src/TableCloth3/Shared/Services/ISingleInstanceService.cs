namespace TableCloth3.Shared.Services;

/// <summary>
/// Service to ensure only one instance of the application runs per user session
/// </summary>
public interface ISingleInstanceService
{
    /// <summary>
    /// Checks if another instance of the application is already running
    /// </summary>
    /// <returns>True if another instance is running, false otherwise</returns>
    bool IsAnotherInstanceRunning();

    /// <summary>
    /// Attempts to bring the existing instance to the foreground
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    bool BringExistingInstanceToForeground();

    /// <summary>
    /// Releases the singleton lock when the application is closing
    /// </summary>
    void ReleaseLock();
}