using QwenCode.IpcGen;

namespace QwenCode.Tests.Parity;

public sealed class IpcGenerationParityTests
{
    [Fact]
    public void Generator_EmitsTypedCoreModelContractsForFrontendBridge()
    {
        var assembly = typeof(QwenCode.App.AppHost.Bootstrapper).Assembly;
        var collector = new IpcMethodCollector();
        var methods = collector.Collect(assembly);
        var emitter = new TypeScriptEmitter();

        var output = emitter.Emit(methods);

        Assert.Contains("export interface AppBootstrapPayload", output);
        Assert.Contains("export interface AuthStatusSnapshot", output);
        Assert.Contains("export interface DesktopSessionDetail", output);
        Assert.Contains("export interface DirectConnectServerState", output);
        Assert.Contains("export interface DirectConnectSessionState", output);
        Assert.Contains("export interface RuntimeModelSnapshot", output);
        Assert.Contains("export interface AssistantExecutionStats", output);
        Assert.Contains("bootstrap(): Promise<AppBootstrapPayload>;", output);
        Assert.Contains("createDirectConnectSession(request: CreateDirectConnectSessionRequest): Promise<DirectConnectSessionState>;", output);
        Assert.Contains("getDirectConnectServer(): Promise<DirectConnectServerState>;", output);
        Assert.Contains("readDirectConnectSessionEvents(request: ReadDirectConnectSessionEventsRequest): Promise<DirectConnectSessionEventBatch>;", output);
        Assert.Contains("getSession(request: GetDesktopSessionRequest): Promise<DesktopSessionDetail>;", output);
        Assert.DoesNotContain("bootstrap(): Promise<unknown>;", output);
        Assert.DoesNotContain("getSession(request: unknown): Promise<unknown>;", output);
        Assert.DoesNotContain("qwenModels: unknown;", output);
        Assert.DoesNotContain("stats: unknown;", output);
        Assert.DoesNotContain("environmentVariables: unknown[];", output);
        Assert.DoesNotContain("headers: unknown[];", output);
    }
}
