using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using QwenCode.App.Attributes;
using ElectronApi = ElectronNET.API.Electron;

namespace QwenCode.App.Ipc;

public abstract class IpcServiceBase(IServiceProvider services)
{
    protected IServiceProvider Services { get; } = services;

    private static int _registered;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public void RegisterAll()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var methods = GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            if (method.GetCustomAttribute<IpcInvokeAttribute>() is { } invoke)
            {
                BindInvoke(method, invoke.Channel);
            }
            else if (method.GetCustomAttribute<IpcSendAttribute>() is { } send)
            {
                BindSend(method, send.Channel);
            }
            else if (method.GetCustomAttribute<IpcEventAttribute>() is { } evt)
            {
                BindEvent(method, evt.Channel);
            }
        }
    }

    private static Type? GetSingleParameterType(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ToArray();

        return parameters.Length == 1 ? parameters[0].ParameterType : null;
    }

    private void BindInvoke(MethodInfo method, string channel)
    {
        var replyChannel = $"{channel}:reply";
        var parameterType = GetSingleParameterType(method);

        ElectronApi.IpcMain.On(channel, async payload =>
        {
            try
            {
                var argument = parameterType is null ? null : Deserialize(payload, parameterType);
                var result = await InvokeAsync(method, argument);
                Reply(replyChannel, result);
            }
            catch (Exception exception)
            {
                Reply(replyChannel, new { error = exception.Message });
            }
        });
    }

    private void BindSend(MethodInfo method, string channel)
    {
        var parameterType = GetSingleParameterType(method);

        ElectronApi.IpcMain.On(channel, payload =>
        {
            var argument = parameterType is null ? null : Deserialize(payload, parameterType);
            _ = InvokeAsync(method, argument);
        });
    }

    private void BindEvent(MethodInfo method, string channel)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 1 ||
            !parameters[0].ParameterType.IsGenericType ||
            parameters[0].ParameterType.GetGenericTypeDefinition() != typeof(Action<>))
        {
            return;
        }

        var payloadType = parameters[0].ParameterType.GetGenericArguments()[0];
        var actionType = typeof(Action<>).MakeGenericType(payloadType);
        var handler = Delegate.CreateDelegate(
            actionType,
            new EventEmitter(channel),
            typeof(EventEmitter).GetMethod(nameof(EventEmitter.Emit))!.MakeGenericMethod(payloadType));

        method.Invoke(this, [handler]);
    }

    private static object? Deserialize(object payload, Type targetType)
    {
        var json = payload switch
        {
            string text => text,
            JsonElement element => element.GetRawText(),
            _ => JsonSerializer.Serialize(payload, JsonOptions)
        };

        if (targetType == typeof(string))
        {
            return JsonSerializer.Deserialize<string>(json, JsonOptions) ?? string.Empty;
        }

        return JsonSerializer.Deserialize(json, targetType, JsonOptions);
    }

    private async Task<object?> InvokeAsync(MethodInfo method, object? argument)
    {
        var args = argument is null ? Array.Empty<object?>() : [argument];
        var result = method.Invoke(this, args);

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return result;
    }

    private static void Reply(string channel, object? payload)
    {
        var window = ElectronApi.WindowManager.BrowserWindows.FirstOrDefault();
        if (window is null)
        {
            return;
        }

        ElectronApi.IpcMain.Send(window, channel, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private sealed class EventEmitter(string channel)
    {
        public void Emit<T>(T payload) => Reply(channel, payload);
    }
}
