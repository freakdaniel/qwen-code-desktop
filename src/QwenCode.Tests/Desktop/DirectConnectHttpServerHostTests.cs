using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QwenCode.App.Desktop.DirectConnect;
using QwenCode.Core.Models;

namespace QwenCode.Tests.Desktop;

public sealed class DirectConnectHttpServerHostTests
{
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task StartAsync_ExposesTokenProtectedSessionEndpoints()
    {
        var directConnect = new RecordingDirectConnectSessionService();
        await using var host = new DirectConnectHttpServerHost(
            Options.Create(new DirectConnectServerOptions
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = 0
            }),
            directConnect,
            NullLogger<DirectConnectHttpServerHost>.Instance);

        var state = await host.StartAsync();

        Assert.True(state.Listening);
        Assert.False(string.IsNullOrWhiteSpace(state.BaseUrl));
        Assert.False(string.IsNullOrWhiteSpace(state.AccessToken));

        using var client = new HttpClient();
        var unauthorized = await client.GetAsync($"{state.BaseUrl}/direct-connect/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", state.AccessToken);
        var response = await client.PostAsJsonAsync(
            $"{state.BaseUrl}/direct-connect/sessions",
            new CreateDirectConnectSessionRequest
            {
                WorkingDirectory = "D:\\Projects\\workspace"
            });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<DirectConnectSessionState>();

        Assert.NotNull(created);
        Assert.Equal("dc-http", created!.DirectConnectSessionId);
        Assert.Equal("D:\\Projects\\workspace", directConnect.LastCreateRequest?.WorkingDirectory);
    }

    [Fact]
    public async Task StreamEvents_EmitsBufferedRecordsAsServerSentEvents()
    {
        var directConnect = new RecordingDirectConnectSessionService();
        await using var host = new DirectConnectHttpServerHost(
            Options.Create(new DirectConnectServerOptions
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = 0
            }),
            directConnect,
            NullLogger<DirectConnectHttpServerHost>.Instance);

        var state = await host.StartAsync();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", state.AccessToken);
        using var response = await client.GetAsync(
            $"{state.BaseUrl}/direct-connect/sessions/dc-http/events/stream?afterSequence=0",
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(": connected", await reader.ReadLineAsync(wait.Token));
        Assert.Equal(string.Empty, await reader.ReadLineAsync(wait.Token));

        directConnect.Publish(new DirectConnectSessionEventRecord
        {
            Sequence = 1,
            Event = new DesktopSessionEvent
            {
                SessionId = "desktop-session",
                Kind = DesktopSessionEventKind.TurnStarted,
                TimestampUtc = DateTime.UtcNow,
                Message = "Turn started",
                WorkingDirectory = "D:\\Projects\\workspace"
            }
        });

        Assert.Equal("id: 1", await reader.ReadLineAsync(wait.Token));
        Assert.Equal("event: session-event", await reader.ReadLineAsync(wait.Token));
        var dataLine = await reader.ReadLineAsync(wait.Token);
        Assert.StartsWith("data: ", dataLine);

        var record = JsonSerializer.Deserialize<DirectConnectSessionEventRecord>(
            dataLine!["data: ".Length..],
            SseJsonOptions);

        Assert.NotNull(record);
        Assert.Equal(1, record!.Sequence);
        Assert.Equal("Turn started", record.Event.Message);
    }

    private sealed class RecordingDirectConnectSessionService : IDirectConnectSessionService
    {
        private readonly Channel<DirectConnectSessionEventRecord> streamEvents = Channel.CreateUnbounded<DirectConnectSessionEventRecord>();

        public CreateDirectConnectSessionRequest? LastCreateRequest { get; private set; }

        public void Publish(DirectConnectSessionEventRecord record) =>
            streamEvents.Writer.TryWrite(record);

        public Task<DirectConnectSessionState> CreateSessionAsync(
            CreateDirectConnectSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            return Task.FromResult(new DirectConnectSessionState
            {
                DirectConnectSessionId = "dc-http",
                BoundSessionId = request.PreferredSessionId,
                WorkingDirectory = request.WorkingDirectory,
                Status = "active",
                CreatedAtUtc = DateTime.UtcNow,
                LastActivityAtUtc = DateTime.UtcNow
            });
        }

        public Task<IReadOnlyList<DirectConnectSessionState>> ListSessionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DirectConnectSessionState>>([]);

        public Task<DirectConnectSessionState?> GetSessionAsync(string directConnectSessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DirectConnectSessionState?>(null);

        public Task<DirectConnectSessionEventBatch> ReadEventsAsync(
            string directConnectSessionId,
            long afterSequence = 0,
            int maxCount = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DirectConnectSessionEventBatch
            {
                DirectConnectSessionId = directConnectSessionId,
                LatestSequence = afterSequence,
                Events = []
            });

        public async IAsyncEnumerable<DirectConnectSessionEventRecord> StreamEventsAsync(
            string directConnectSessionId,
            long afterSequence = 0,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await streamEvents.Reader.WaitToReadAsync(cancellationToken))
            {
                while (streamEvents.Reader.TryRead(out var record))
                {
                    if (record.Sequence > afterSequence)
                    {
                        yield return record;
                    }
                }
            }
        }

        public Task<DesktopSessionTurnResult> StartTurnAsync(
            string directConnectSessionId,
            StartDesktopSessionTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
            string directConnectSessionId,
            ApproveDesktopSessionToolRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
            string directConnectSessionId,
            AnswerDesktopSessionQuestionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
            string directConnectSessionId,
            CancelDesktopSessionTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
            string directConnectSessionId,
            ResumeInterruptedTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
            string directConnectSessionId,
            DismissInterruptedTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DirectConnectSessionState> CloseSessionAsync(
            string directConnectSessionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
