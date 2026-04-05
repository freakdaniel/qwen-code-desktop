namespace QwenCode.Tests.Tools;

public sealed class LspToolServiceTests
{
    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspDocumentSymbol_ReturnsDeclaredSymbols()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"documentSymbol","filePath":"{{fixture.FooFilePath.Replace("\\", "\\\\")}}"}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Document symbols", result.Output);
            Assert.Contains("Foo", result.Output);
            Assert.Contains("Run", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspWorkspaceSymbol_FindsWorkspaceMatches()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = """{"operation":"workspaceSymbol","query":"Foo"}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Workspace symbols", result.Output);
            Assert.Contains("Foo", result.Output);
            Assert.Contains("IFoo", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspGoToDefinition_ResolvesSymbolLocation()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var (line, character) = FindLineAndCharacter(fixture.BarContent, "new Foo");
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"goToDefinition","filePath":"{{fixture.BarFilePath.Replace("\\", "\\\\")}}","line":{{line}},"character":{{character + 4}}}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Definitions:", result.Output);
            Assert.Contains("Foo.cs", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspFindReferences_FindsUsageLocations()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var (line, character) = FindLineAndCharacter(fixture.FooContent, "class Foo");
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"findReferences","filePath":"{{fixture.FooFilePath.Replace("\\", "\\\\")}}","line":{{line}},"character":{{character + 7}}}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("References:", result.Output);
            Assert.Contains("Bar.cs", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspDiagnostics_ReturnsSourceErrors()
    {
        var fixture = await CreateLspFixtureAsync();
        var brokenFilePath = Path.Combine(fixture.WorkspaceRoot, "Broken.cs");
        await File.WriteAllTextAsync(
            brokenFilePath,
            """
            namespace Demo;
            public sealed class Broken
            {
                public void Test()
                {
                    var value = ;
                }
            }
            """);

        try
        {
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"diagnostics","filePath":"{{brokenFilePath.Replace("\\", "\\\\")}}"}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Diagnostics for", result.Output);
            Assert.Contains("Broken.cs", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspWorkspaceDiagnostics_ReturnsWorkspaceErrors()
    {
        var fixture = await CreateLspFixtureAsync();
        var brokenFilePath = Path.Combine(fixture.WorkspaceRoot, "Broken.cs");
        await File.WriteAllTextAsync(
            brokenFilePath,
            """
            namespace Demo;
            public sealed class Broken
            {
                public void Test()
                {
                    var value = ;
                }
            }
            """);

        try
        {
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = """{"operation":"workspaceDiagnostics"}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Workspace diagnostics", result.Output);
            Assert.Contains("Broken.cs", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspPrepareCallHierarchy_ReturnsResolvedSymbol()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var (line, character) = FindLineAndCharacter(fixture.FooContent, "void Run();");
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"prepareCallHierarchy","filePath":"{{fixture.FooFilePath.Replace("\\", "\\\\")}}","line":{{line}},"character":{{character + 6}}}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Call hierarchy item", result.Output);
            Assert.Contains("Run", result.Output);
            Assert.Contains("Foo.cs", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspIncomingCalls_ReturnsCallers()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var (line, character) = FindLineAndCharacter(fixture.FooContent, "void Run();");
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"incomingCalls","filePath":"{{fixture.FooFilePath.Replace("\\", "\\\\")}}","line":{{line}},"character":{{character + 6}}}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Incoming calls", result.Output);
            Assert.Contains("Test", result.Output);
            Assert.Contains("Bar.cs", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspOutgoingCalls_ReturnsCallees()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var (line, character) = FindLineAndCharacter(fixture.BarContent, "void Test()");
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"outgoingCalls","filePath":"{{fixture.BarFilePath.Replace("\\", "\\\\")}}","line":{{line}},"character":{{character + 6}}}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Outgoing calls", result.Output);
            Assert.Contains("Foo", result.Output);
            Assert.Contains("Run", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_LspCodeActions_ReturnsSuggestionsForDiagnostics()
    {
        var fixture = await CreateLspFixtureAsync();
        var brokenFilePath = Path.Combine(fixture.WorkspaceRoot, "Broken.cs");
        await File.WriteAllTextAsync(
            brokenFilePath,
            """
            namespace Demo;
            public sealed class Broken
            {
                public void Test()
                {
                    var value = ;
                }
            }
            """);

        try
        {
            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"codeActions","filePath":"{{brokenFilePath.Replace("\\", "\\\\")}}","line":5,"character":21}"""
                });

            Assert.Equal("completed", result.Status);
            Assert.Contains("Code actions", result.Output);
            Assert.Contains("CS", result.Output);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Lsp_UntrustedWorkspace_ReturnsTrustError()
    {
        var fixture = await CreateLspFixtureAsync();

        try
        {
            var homeQwenRoot = Path.Combine(fixture.Root, "home", ".qwen");
            Directory.CreateDirectory(homeQwenRoot);
            await File.WriteAllTextAsync(
                Path.Combine(homeQwenRoot, "settings.json"),
                """
                {
                  "security": {
                    "folderTrust": {
                      "enabled": true
                    }
                  }
                }
                """);
            await File.WriteAllTextAsync(
                Path.Combine(homeQwenRoot, "trustedFolders.json"),
                BuildTrustedFoldersJson(fixture.WorkspaceRoot, "DO_NOT_TRUST"));

            var result = await fixture.Host.ExecuteAsync(
                fixture.Workspace,
                new ExecuteNativeToolRequest
                {
                    ToolName = "lsp",
                    ArgumentsJson = $$"""{"operation":"documentSymbol","filePath":"{{fixture.FooFilePath.Replace("\\", "\\\\")}}"}"""
                });

            Assert.Equal("error", result.Status);
            Assert.Contains("not trusted", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static async Task<LspFixture> CreateLspFixtureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-lsp-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        var fooContent =
            """
            namespace Demo;

            public interface IFoo
            {
                void Run();
            }

            public sealed class Foo : IFoo
            {
                public void Run()
                {
                }
            }
            """;
        var barContent =
            """
            namespace Demo;

            public sealed class Bar
            {
                public void Test()
                {
                    IFoo foo = new Foo();
                    foo.Run();
                }
            }
            """;

        var fooFilePath = Path.Combine(workspaceRoot, "Foo.cs");
        var barFilePath = Path.Combine(workspaceRoot, "Bar.cs");
        await File.WriteAllTextAsync(fooFilePath, fooContent);
        await File.WriteAllTextAsync(barFilePath, barContent);

        var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
        var host = new NativeToolHostService(
            runtimeProfileService,
            new ApprovalPolicyService(),
            new InMemoryCronScheduler(),
            webToolService: null,
            userQuestionToolService: null,
            lspToolService: new RoslynLspToolService());

        return new LspFixture(root, workspaceRoot, new WorkspacePaths { WorkspaceRoot = workspaceRoot }, host, fooFilePath, barFilePath, fooContent, barContent);
    }

    private static (int Line, int Character) FindLineAndCharacter(string text, string needle)
    {
        var index = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Could not find '{needle}' in the test document.");

        var line = 1;
        var character = 1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                character = 1;
                continue;
            }

            if (text[i] != '\r')
            {
                character++;
            }
        }

        return (line, character);
    }

    private sealed record LspFixture(
        string Root,
        string WorkspaceRoot,
        WorkspacePaths Workspace,
        NativeToolHostService Host,
        string FooFilePath,
        string BarFilePath,
        string FooContent,
        string BarContent) : IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static string BuildTrustedFoldersJson(string workspaceRoot, string trustValue) =>
        $$"""
        {
          "{{workspaceRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}}": "{{trustValue}}"
        }
        """;
}
