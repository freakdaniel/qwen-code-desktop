namespace QwenCode.App.Channels;

public sealed class ChannelBlockStreamer(Func<string, Task> sendAsync, int minChars, int maxChars, int idleMs) : IAsyncDisposable
{
    private readonly Lock gate = new();
    private string buffer = string.Empty;
    private Task sending = Task.CompletedTask;
    private CancellationTokenSource? idleCancellation;
    private bool disposed;

    public void Push(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }

        List<string> blocks;
        CancellationTokenSource? pendingCancellation;
        CancellationTokenSource? scheduledCancellation = null;

        lock (gate)
        {
            ThrowIfDisposed();
            buffer += chunk;
            pendingCancellation = idleCancellation;
            idleCancellation = null;
            blocks = ExtractBlocksUnsafe();

            if (buffer.Length > 0 && idleMs > 0)
            {
                scheduledCancellation = new CancellationTokenSource();
                idleCancellation = scheduledCancellation;
            }
        }

        pendingCancellation?.Cancel();
        pendingCancellation?.Dispose();

        foreach (var block in blocks)
        {
            QueueSend(block);
        }

        if (scheduledCancellation is not null)
        {
            _ = RunIdleFlushAsync(scheduledCancellation);
        }
    }

    public async Task<bool> FlushAsync()
    {
        string? remainder;
        CancellationTokenSource? pendingCancellation;

        lock (gate)
        {
            ThrowIfDisposed();
            pendingCancellation = idleCancellation;
            idleCancellation = null;
            remainder = buffer;
            buffer = string.Empty;
        }

        pendingCancellation?.Cancel();
        pendingCancellation?.Dispose();

        var hadContent = !string.IsNullOrWhiteSpace(remainder);
        if (hadContent)
        {
            QueueSend(remainder!);
        }

        await AwaitSendsAsync();
        return hadContent;
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? pendingCancellation;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            pendingCancellation = idleCancellation;
            idleCancellation = null;
            buffer = string.Empty;
        }

        pendingCancellation?.Cancel();
        pendingCancellation?.Dispose();
        await AwaitSendsAsync();
    }

    private async Task RunIdleFlushAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(idleMs, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        string? block = null;
        lock (gate)
        {
            if (disposed || !ReferenceEquals(idleCancellation, cancellation))
            {
                return;
            }

            idleCancellation = null;
            if (buffer.Length >= minChars)
            {
                block = buffer;
                buffer = string.Empty;
            }
        }

        cancellation.Dispose();

        if (!string.IsNullOrWhiteSpace(block))
        {
            QueueSend(block);
        }
    }

    private List<string> ExtractBlocksUnsafe()
    {
        var blocks = new List<string>();

        while (buffer.Length >= maxChars)
        {
            var breakPoint = FindBreakPoint(buffer, maxChars);
            blocks.Add(buffer[..breakPoint]);
            buffer = buffer[breakPoint..];
        }

        if (buffer.Length >= minChars)
        {
            var blockBoundary = FindBlockBoundary(buffer);
            if (blockBoundary > 0)
            {
                blocks.Add(buffer[..blockBoundary]);
                buffer = buffer[blockBoundary..];
            }
        }

        return blocks;
    }

    private void QueueSend(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        lock (gate)
        {
            sending = sending
                .ContinueWith(
                    async _ => await sendAsync(trimmed),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap()
                .ContinueWith(
                    static task => task.IsFaulted ? Task.CompletedTask : task,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task AwaitSendsAsync()
    {
        Task pending;
        lock (gate)
        {
            pending = sending;
        }

        await pending;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ChannelBlockStreamer));
        }
    }

    private int FindBlockBoundary(string text)
    {
        var last = text.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (last < 0 || last < minChars)
        {
            return -1;
        }

        return last + 2;
    }

    private static int FindBreakPoint(string text, int maxPosition)
    {
        var subText = text[..Math.Min(text.Length, maxPosition)];
        var paragraph = subText.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (paragraph > 0)
        {
            return paragraph + 2;
        }

        var newline = subText.LastIndexOf('\n');
        if (newline > 0)
        {
            return newline + 1;
        }

        var space = subText.LastIndexOf(' ');
        if (space > 0)
        {
            return space + 1;
        }

        return Math.Min(text.Length, maxPosition);
    }
}
