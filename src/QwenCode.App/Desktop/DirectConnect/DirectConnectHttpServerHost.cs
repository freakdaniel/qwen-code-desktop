using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QwenCode.Core.Models;

namespace QwenCode.App.Desktop.DirectConnect;

/// <summary>
/// Hosts the local HTTP surface for direct-connect session control.
/// </summary>
public sealed class DirectConnectHttpServerHost(
    IOptions<DirectConnectServerOptions> options,
    IDirectConnectSessionService directConnectSessionService,
    ILogger<DirectConnectHttpServerHost> logger) : IDirectConnectServerHost, IAsyncDisposable, IDisposable
{
    private static readonly TimeSpan StreamHeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly object gate = new();
    private readonly string accessToken = GenerateAccessToken();
    private WebApplication? app;
    private DirectConnectServerState state = new()
    {
        Enabled = options.Value.Enabled
    };

    /// <inheritdoc />
    public DirectConnectServerState State
    {
        get
        {
            lock (gate)
            {
                return state;
            }
        }
    }

    /// <inheritdoc />
    public async Task<DirectConnectServerState> StartAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (app is not null || state.Listening)
            {
                return state;
            }
        }

        var configured = options.Value;
        if (!configured.Enabled)
        {
            SetState(new DirectConnectServerState
            {
                Enabled = false
            });
            return State;
        }

        try
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(DirectConnectHttpServerHost).Assembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory
            });

            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(directConnectSessionService);
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls(BuildListenUrl(configured));

            var application = builder.Build();
            application.Use(async (context, next) =>
            {
                if (!IsAuthorized(context.Request, accessToken))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Unauthorized direct-connect request."
                    }, cancellationToken: context.RequestAborted);
                    return;
                }

                await next(context);
            });
            MapEndpoints(application);

            await application.StartAsync(cancellationToken);

            var addressesFeature = application.Services.GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>();
            var baseUrl = addressesFeature?.Addresses.FirstOrDefault()
                ?? application.Urls.FirstOrDefault()
                ?? BuildListenUrl(configured);

            lock (gate)
            {
                app = application;
                state = new DirectConnectServerState
                {
                    Enabled = true,
                    Listening = true,
                    BaseUrl = baseUrl.TrimEnd('/'),
                    AccessToken = accessToken
                };
                return state;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Direct-connect HTTP server failed to start");
            SetState(new DirectConnectServerState
            {
                Enabled = true,
                Listening = false,
                AccessToken = accessToken,
                Error = exception.Message
            });
            return State;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        WebApplication? application;
        lock (gate)
        {
            application = app;
            app = null;
            state = state.WithListening(false);
        }

        if (application is not null)
        {
            await application.StopAsync(cancellationToken);
            await application.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();

    private void SetState(DirectConnectServerState nextState)
    {
        lock (gate)
        {
            state = nextState;
        }
    }

    private void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/direct-connect/server", () => Results.Ok(State));
        endpoints.MapPost("/direct-connect/sessions", directConnectSessionService.CreateSessionAsync);
        endpoints.MapGet("/direct-connect/sessions", directConnectSessionService.ListSessionsAsync);
        endpoints.MapGet("/direct-connect/sessions/{directConnectSessionId}", directConnectSessionService.GetSessionAsync);
        endpoints.MapGet("/direct-connect/sessions/{directConnectSessionId}/events", directConnectSessionService.ReadEventsAsync);
        endpoints.MapGet("/direct-connect/sessions/{directConnectSessionId}/events/stream", StreamSessionEventsAsync);
        endpoints.MapPost("/direct-connect/sessions/{directConnectSessionId}/turns", directConnectSessionService.StartTurnAsync);
        endpoints.MapPost("/direct-connect/sessions/{directConnectSessionId}/approvals", directConnectSessionService.ApprovePendingToolAsync);
        endpoints.MapPost("/direct-connect/sessions/{directConnectSessionId}/answers", directConnectSessionService.AnswerPendingQuestionAsync);
        endpoints.MapPost("/direct-connect/sessions/{directConnectSessionId}/cancel", directConnectSessionService.CancelTurnAsync);
        endpoints.MapPost("/direct-connect/sessions/{directConnectSessionId}/resume", directConnectSessionService.ResumeInterruptedTurnAsync);
        endpoints.MapPost("/direct-connect/sessions/{directConnectSessionId}/dismiss", directConnectSessionService.DismissInterruptedTurnAsync);
        endpoints.MapDelete("/direct-connect/sessions/{directConnectSessionId}", directConnectSessionService.CloseSessionAsync);
    }

    private async Task StreamSessionEventsAsync(
        HttpContext context,
        string directConnectSessionId,
        long? afterSequence = null)
    {
        var cancellationToken = context.RequestAborted;
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers.XContentTypeOptions = "nosniff";

        await WriteSseCommentAsync(context.Response, "connected", cancellationToken);

        await using var enumerator = directConnectSessionService
            .StreamEventsAsync(
                directConnectSessionId,
                afterSequence.GetValueOrDefault(),
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var moveNextTask = enumerator.MoveNextAsync().AsTask();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!moveNextTask.IsCompleted &&
                await Task.WhenAny(moveNextTask, Task.Delay(StreamHeartbeatInterval, cancellationToken)) != moveNextTask)
            {
                await WriteSseCommentAsync(context.Response, "keep-alive", cancellationToken);
                continue;
            }

            if (!await moveNextTask)
            {
                break;
            }

            await WriteSseEventAsync(context.Response, enumerator.Current, cancellationToken);
            moveNextTask = enumerator.MoveNextAsync().AsTask();
        }
    }

    private static async Task WriteSseEventAsync(
        HttpResponse response,
        DirectConnectSessionEventRecord record,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"id: {record.Sequence}\n", cancellationToken);
        await response.WriteAsync("event: session-event\n", cancellationToken);
        var data = JsonSerializer.Serialize(record, StreamJsonOptions);
        foreach (var line in data.Split('\n'))
        {
            await response.WriteAsync($"data: {line}\n", cancellationToken);
        }

        await response.WriteAsync("\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static async Task WriteSseCommentAsync(
        HttpResponse response,
        string comment,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($": {comment}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static bool IsAuthorized(HttpRequest request, string expectedToken)
    {
        if (request.Headers.Authorization.ToString() is { Length: > 0 } authorization &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(authorization["Bearer ".Length..].Trim(), expectedToken, StringComparison.Ordinal))
        {
            return true;
        }

        return request.Headers.TryGetValue("X-Qwen-Direct-Connect-Token", out var token) &&
               string.Equals(token.ToString(), expectedToken, StringComparison.Ordinal);
    }

    private static string BuildListenUrl(DirectConnectServerOptions options)
    {
        var host = string.IsNullOrWhiteSpace(options.Host) ? "127.0.0.1" : options.Host.Trim();
        var port = Math.Max(0, options.Port);
        return $"http://{host}:{port}";
    }

    private static string GenerateAccessToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

file static class DirectConnectServerStateExtensions
{
    public static DirectConnectServerState WithListening(this DirectConnectServerState state, bool listening) =>
        new()
        {
            Enabled = state.Enabled,
            Listening = listening,
            BaseUrl = listening ? state.BaseUrl : string.Empty,
            AccessToken = state.AccessToken,
            Error = state.Error
        };
}
