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
        var nullabilityContext = new NullabilityInfoContext();

        if (method.GetCustomAttribute<IpcInvokeAttribute>() is { } invoke)
        {
            var input = GetInputParameter(method);
            return new IpcMethod(
                IpcKind.Invoke,
                invoke.Channel,
                ToCamelCase(method.Name),
                input?.ParameterType,
                input is null ? null : nullabilityContext.Create(input),
                UnwrapReturnType(method.ReturnType),
                nullabilityContext.Create(method.ReturnParameter));
        }

        if (method.GetCustomAttribute<IpcSendAttribute>() is { } send)
        {
            var input = GetInputParameter(method);
            return new IpcMethod(
                IpcKind.Send,
                send.Channel,
                ToCamelCase(method.Name),
                input?.ParameterType,
                input is null ? null : nullabilityContext.Create(input),
                typeof(void),
                null);
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
                null,
                payloadType,
                null);
        }

        return null;
    }

    private static ParameterInfo? GetInputParameter(MethodInfo method)
        => method.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
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
