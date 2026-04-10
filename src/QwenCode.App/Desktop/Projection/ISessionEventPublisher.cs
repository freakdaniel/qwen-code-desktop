using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Allows services outside the session host pipeline to emit session events
/// to all IPC subscribers
/// </summary>
public interface ISessionEventPublisher
{
    /// <summary>
    /// Publishes a session event to all IPC subscribers
    /// </summary>
    /// <param name="sessionEvent">The session event to publish</param>
    void Publish(DesktopSessionEvent sessionEvent);
}
