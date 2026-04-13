using System.Diagnostics;
using System.Runtime.InteropServices;
using InfiniFrame;
using Microsoft.Extensions.Logging;
using QwenCode.Core.Models;

namespace QwenCode.App.AppHost;

/// <summary>
/// Defines access to the active native desktop window.
/// </summary>
public interface IDesktopWindowBridge
{
    /// <summary>
    /// Attaches the active window instance.
    /// </summary>
    /// <param name="window">The active window.</param>
    void AttachWindow(IInfiniFrameWindow window);

    /// <summary>
    /// Publishes a raw message to the renderer.
    /// </summary>
    /// <param name="message">The serialized message.</param>
    Task PublishAsync(string message);

    /// <summary>
    /// Opens a project directory picker.
    /// </summary>
    /// <returns>The directory selection result.</returns>
    Task<SelectProjectDirectoryResult> SelectProjectDirectoryAsync();

    /// <summary>
    /// Minimizes the current window.
    /// </summary>
    void Minimize();

    /// <summary>
    /// Toggles maximize state for the current window.
    /// </summary>
    void ToggleMaximize();

    /// <summary>
    /// Starts dragging the current window.
    /// </summary>
    void BeginDrag();

    /// <summary>
    /// Starts resizing the current window from the specified edge.
    /// </summary>
    /// <param name="edge">The resize edge or corner.</param>
    void BeginResize(string edge);

    /// <summary>
    /// Closes the current window.
    /// </summary>
    void Close();

    /// <summary>
    /// Opens the specified URL using the host operating system.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <returns><c>true</c> when the URL was opened.</returns>
    bool OpenExternalUrl(string url);
}

/// <summary>
/// Provides access to the current InfiniFrame window for shell actions.
/// </summary>
public sealed class DesktopWindowBridge(ILogger<DesktopWindowBridge> logger) : IDesktopWindowBridge
{
    private const int SwRestore = 9;
    private const int SwMinimize = 6;
    private const int SwMaximize = 3;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int GwlStyle = -16;
    private const nint WsCaption = 0x00C00000;
    private const nint WsMinimizeBox = 0x00020000;
    private const nint WsMaximizeBox = 0x00010000;
    private const nint WsSysMenu = 0x00080000;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    private readonly object _sync = new();
    private IInfiniFrameWindow? _window;

    /// <inheritdoc />
    public void AttachWindow(IInfiniFrameWindow window)
    {
        lock (_sync)
        {
            _window = window;
        }
    }

    /// <inheritdoc />
    public Task PublishAsync(string message)
    {
        var window = GetWindow();
        window.Invoke(() => window.SendWebMessage(message));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SelectProjectDirectoryResult> SelectProjectDirectoryAsync()
    {
        var window = GetWindow();
        var selectedPath = string.Empty;

        window.Invoke(() =>
        {
            selectedPath = window.ShowOpenFolder(
                "Select project directory",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                ?.FirstOrDefault() ?? string.Empty;
        });

        return Task.FromResult(new SelectProjectDirectoryResult
        {
            Cancelled = string.IsNullOrWhiteSpace(selectedPath),
            SelectedPath = selectedPath
        });
    }

    /// <inheritdoc />
    public void Minimize()
        => WithWindow(window =>
        {
            if (!TryGetNativeWindowHandle(window, out var handle))
            {
                return;
            }

            ShowWindow(handle, SwMinimize);
        });

    /// <inheritdoc />
    public void ToggleMaximize()
        => WithWindow(window =>
        {
            if (!TryGetNativeWindowHandle(window, out var handle))
            {
                return;
            }

            ShowWindow(handle, IsZoomed(handle) ? SwRestore : SwMaximize);
        });

    /// <inheritdoc />
    public void BeginDrag()
        => WithWindow(window =>
        {
            if (!TryGetNativeWindowHandle(window, out var handle))
            {
                return;
            }

            ReleaseCapture();
            SendMessage(handle, WmNcLButtonDown, (nint)HtCaption, IntPtr.Zero);
        });

    /// <inheritdoc />
    public void BeginResize(string edge)
        => WithWindow(window =>
        {
            if (!TryGetNativeWindowHandle(window, out var handle))
            {
                return;
            }

            if (!TryMapResizeEdge(edge, out var hitTest))
            {
                logger.LogDebug("Ignoring unsupported resize edge {Edge}", edge);
                return;
            }

            ReleaseCapture();
            SendMessage(handle, WmNcLButtonDown, (nint)hitTest, IntPtr.Zero);
        });

    /// <inheritdoc />
    public void Close()
        => WithWindow(window => window.Close());

    /// <inheritdoc />
    public bool OpenExternalUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeMailto))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to open external URL {Url}", url);
            return false;
        }
    }

    private void WithWindow(Action<IInfiniFrameWindow> action)
    {
        var window = GetWindow();
        window.Invoke(() => action(window));
    }

    private bool TryGetNativeWindowHandle(IInfiniFrameWindow window, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var property = window.GetType().GetProperty("WindowHandle");
        if (property?.GetValue(window) is IntPtr windowHandle && windowHandle != IntPtr.Zero)
        {
            handle = windowHandle;
            return true;
        }

        logger.LogWarning("InfiniFrame window handle is unavailable; window command skipped");
        return false;
    }

    private static bool TryMapResizeEdge(string edge, out int hitTest)
    {
        hitTest = edge.ToLowerInvariant() switch
        {
            "left" => HtLeft,
            "right" => HtRight,
            "top" => HtTop,
            "bottom" => HtBottom,
            "top-left" => HtTopLeft,
            "top-right" => HtTopRight,
            "bottom-left" => HtBottomLeft,
            "bottom-right" => HtBottomRight,
            _ => 0
        };

        return hitTest != 0;
    }

    private IInfiniFrameWindow GetWindow()
    {
        lock (_sync)
        {
            return _window ?? throw new InvalidOperationException("Desktop window has not been attached yet.");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern nint SendMessage(IntPtr hWnd, int msg, nint wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
