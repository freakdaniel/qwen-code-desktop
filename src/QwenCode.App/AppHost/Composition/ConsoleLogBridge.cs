using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace QwenCode.App.AppHost;

internal static class ConsoleLogBridge
{
    private static readonly Regex AnsiEscapePattern = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static int _installed;

    /// <summary>
    /// Executes install
    /// </summary>
    /// <param name="loggerFactory">The logger factory</param>
    public static void Install(ILoggerFactory loggerFactory)
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }

        var logger = loggerFactory.CreateLogger("QwenCode.App.ElectronConsole");
        Console.SetOut(new BridgedTextWriter(Console.Out, logger, isErrorStream: false));
        Console.SetError(new BridgedTextWriter(Console.Error, logger, isErrorStream: true));
    }

    private sealed class BridgedTextWriter(
        TextWriter inner,
        ILogger logger,
        bool isErrorStream) : TextWriter
    {
        private static readonly AsyncLocal<int> SuppressionDepth = new();
        private static readonly Regex SerilogConsolePattern = new(@"^\[\d{2}:\d{2}:\d{2}\s+[A-Z]{3}\]", RegexOptions.Compiled);
        private readonly StringBuilder _buffer = new();

        /// <summary>
        /// Gets the encoding
        /// </summary>
        public override Encoding Encoding => inner.Encoding;

        /// <summary>
        /// Writes value
        /// </summary>
        /// <param name="value">The value</param>
        public override void Write(char value)
        {
            Append(value);
        }

        /// <summary>
        /// Writes value
        /// </summary>
        /// <param name="value">The value</param>
        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var character in value)
            {
                Append(character);
            }
        }

        /// <summary>
        /// Writes line
        /// </summary>
        /// <param name="value">The value</param>
        public override void WriteLine(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var character in value)
                {
                    Append(character);
                }
            }

            FlushBuffer();
        }

        /// <summary>
        /// Executes flush
        /// </summary>
        public override void Flush()
        {
            FlushBuffer();
            inner.Flush();
        }

        private void Append(char value)
        {
            if (value == '\r')
            {
                return;
            }

            if (value == '\n')
            {
                FlushBuffer();
                return;
            }

            _buffer.Append(value);
        }

        private void FlushBuffer()
        {
            if (_buffer.Length == 0)
            {
                return;
            }

            var message = _buffer.ToString().Trim();
            _buffer.Clear();
            var unescapedMessage = StripAnsi(message);

            if (string.IsNullOrWhiteSpace(unescapedMessage))
            {
                return;
            }

            if (SuppressionDepth.Value > 0 || SerilogConsolePattern.IsMatch(unescapedMessage))
            {
                inner.WriteLine(message);
                return;
            }

            var normalized = NormalizeMessage(message);
            if (ShouldSuppress(normalized))
            {
                return;
            }

            try
            {
                SuppressionDepth.Value++;
                var level = ClassifyLevel(normalized, isErrorStream);
                switch (level)
                {
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        logger.LogError("Electron runtime: {Message}", normalized);
                        break;
                    case LogLevel.Warning:
                        logger.LogWarning("Electron runtime: {Message}", normalized);
                        break;
                    default:
                        logger.LogInformation("Electron runtime: {Message}", normalized);
                        break;
                }
            }
            finally
            {
                SuppressionDepth.Value--;
            }
        }

        private static string NormalizeMessage(string message) =>
            message.StartsWith("||", StringComparison.Ordinal)
                ? message.TrimStart('|', ' ')
                : message;

        private static string StripAnsi(string message) =>
            AnsiEscapePattern.Replace(message, string.Empty);

        private static bool ShouldSuppress(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            if (message.StartsWith("[StartCore]:", StringComparison.Ordinal) ||
                message.StartsWith("[StartInternal]: startCmd:", StringComparison.Ordinal) ||
                message.StartsWith("[StartInternal]: args:", StringComparison.Ordinal) ||
                message.StartsWith("[StartInternal]: after run:", StringComparison.Ordinal) ||
                message.StartsWith("GatherBuildInfo:", StringComparison.Ordinal) ||
                message.StartsWith("Probe scored for launch origin:", StringComparison.Ordinal) ||
                message.StartsWith("Probe scored for package mode:", StringComparison.Ordinal) ||
                message.StartsWith("Evaluated StartupMethod:", StringComparison.Ordinal) ||
                message.StartsWith("Entry!!!:", StringComparison.Ordinal) ||
                message.StartsWith("unpackedelectron! dir:", StringComparison.Ordinal) ||
                message.StartsWith("Electron Socket IO", StringComparison.Ordinal) ||
                message.StartsWith("Electron Socket: starting", StringComparison.Ordinal) ||
                message.StartsWith("Electron Socket: listening", StringComparison.Ordinal) ||
                message.StartsWith("Electron Socket: loading components", StringComparison.Ordinal) ||
                message.StartsWith("Electron Socket: startup complete", StringComparison.Ordinal) ||
                message.StartsWith("BridgeConnector connected!", StringComparison.Ordinal))
            {
                return true;
            }

            if (message.Contains("custom_main → Renderer console", StringComparison.Ordinal) &&
                !message.Contains("[2]", StringComparison.Ordinal) &&
                !message.Contains("[3]", StringComparison.Ordinal) &&
                !message.Contains("[4]", StringComparison.Ordinal) &&
                !message.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("warning", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (message.StartsWith("Policy set or a policy with", StringComparison.Ordinal) ||
                message.StartsWith("this app to unnecessary security risks.", StringComparison.Ordinal) ||
                message.StartsWith("For more information and help, consult", StringComparison.Ordinal) ||
                message.StartsWith("https://electronjs.org/docs/tutorial/security.", StringComparison.Ordinal) ||
                message.StartsWith("This warning will not show up", StringComparison.Ordinal) ||
                message.StartsWith("once the app is packaged.", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static LogLevel ClassifyLevel(string message, bool fromErrorStream)
        {
            if (LooksLikeError(message))
            {
                return LogLevel.Error;
            }

            if (LooksLikeWarning(message))
            {
                return LogLevel.Warning;
            }

            return fromErrorStream ? LogLevel.Information : LogLevel.Information;
        }

        private static bool LooksLikeError(string message) =>
            message.Contains(" error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("transport error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("uncaught", StringComparison.OrdinalIgnoreCase);

        private static bool LooksLikeWarning(string message) =>
            message.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("warn", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("disconnect", StringComparison.OrdinalIgnoreCase);
    }
}
