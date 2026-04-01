using System.Reflection;

namespace QwenCode.IpcGen;

internal static class Program
{
    private static int Main(string[] args)
    {
        var outputPath = ResolveOutputPath(args);
        var assembly = typeof(QwenCode.App.AppHost.Bootstrapper).Assembly;
        var collector = new IpcMethodCollector();
        var methods = collector.Collect(assembly);

        if (methods.Count == 0)
        {
            Console.Error.WriteLine("[IpcGen] No IPC handlers found in QwenCode.App.");
            return 1;
        }

        var emitter = new TypeScriptEmitter();
        var contents = emitter.Emit(methods);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, contents);

        Console.WriteLine($"[IpcGen] Wrote {methods.Count} IPC methods to {outputPath}");
        return 0;
    }

    private static string ResolveOutputPath(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "--output")
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "QwenCode.App",
            "Frontend",
            "src",
            "types",
            "ipc.generated.ts"));
    }
}
