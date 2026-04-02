using System.Reflection;
using QwenCode.App.Ipc.Attributes;
using QwenCode.App.Ipc;

namespace QwenCode.IpcGen;

internal sealed class IpcMethodCollector
{
    public List<IpcMethod> Collect(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           typeof(IpcServiceBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(TryCreate)
            .OfType<IpcMethod>()
            .ToList();
    }

    private static IpcMethod? TryCreate(MethodInfo method)
    {
        if (method.GetCustomAttribute<IpcInvokeAttribute>() is { } invoke)
        {
            return new IpcMethod(
                IpcKind.Invoke,
                invoke.Channel,
                ToCamelCase(method.Name),
                GetInputType(method),
                UnwrapReturnType(method.ReturnType));
        }

        if (method.GetCustomAttribute<IpcSendAttribute>() is { } send)
        {
            return new IpcMethod(
                IpcKind.Send,
                send.Channel,
                ToCamelCase(method.Name),
                GetInputType(method),
                typeof(void));
        }

        if (method.GetCustomAttribute<IpcEventAttribute>() is { } evt)
        {
            var callbackType = method.GetParameters().SingleOrDefault()?.ParameterType;
            var payloadType = callbackType?.IsGenericType == true
                ? callbackType.GetGenericArguments()[0]
                : typeof(object);

            return new IpcMethod(
                IpcKind.Event,
                evt.Channel,
                ToCamelCase(method.Name),
                null,
                payloadType);
        }

        return null;
    }

    private static Type? GetInputType(MethodInfo method)
        => method.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .Select(parameter => parameter.ParameterType)
            .FirstOrDefault();

    private static Type UnwrapReturnType(Type type)
    {
        if (type == typeof(Task))
        {
            return typeof(void);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }

    private static string ToCamelCase(string value)
        => string.IsNullOrEmpty(value)
            ? value
            : string.Create(value.Length, value, static (span, source) =>
            {
                span[0] = char.ToLowerInvariant(source[0]);
                source.AsSpan(1).CopyTo(span[1..]);
            });
}
