using QwenCode.App.Config;

namespace QwenCode.Tests.Runtime;

public sealed class ModelRegistryTests
{
    [Fact]
    public void Inspect_ReturnsConfiguredProvidersAndEmbeddingModel()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-model-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "openai"
                    }
                  },
                  "model": {
                    "name": "qwen-max"
                  },
                  "embeddingModel": "text-embedding-v4",
                  "modelProviders": {
                    "openai": [
                      {
                        "id": "qwen-max",
                        "baseUrl": "https://provider.example/v1",
                        "envKey": "CUSTOM_OPENAI_KEY"
                      }
                    ]
                  }
                }
                """);

            var registry = new ModelRegistryService(new RuntimeConfigService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)));
            var snapshot = registry.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Equal("qwen-max", snapshot.DefaultModelId);
            Assert.Equal("text-embedding-v4", snapshot.EmbeddingModelId);
            Assert.Equal("openai", snapshot.SelectedAuthType);

            var defaultModel = Assert.Single(snapshot.AvailableModels, static model => model.IsDefaultModel);
            Assert.Equal("qwen-max", defaultModel.Id);
            Assert.Equal("model-provider", defaultModel.Source);
            Assert.True(defaultModel.Capabilities.SupportsToolCalls);

            var embeddingModel = Assert.Single(snapshot.AvailableModels, static model => model.IsEmbeddingModel);
            Assert.Equal("text-embedding-v4", embeddingModel.Id);
            Assert.True(embeddingModel.Capabilities.SupportsEmbeddings);
            Assert.False(embeddingModel.Capabilities.SupportsToolCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ReturnsEmbeddingAndDefaultModelsFromSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-model-resolver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "openai"
                    }
                  },
                  "model": {
                    "name": "qwen3-coder-plus"
                  },
                  "embeddingModel": "text-embedding-v4"
                }
                """);

            var configService = new RuntimeConfigService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var resolver = new ModelConfigResolver(new ModelRegistryService(configService));
            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };

            var defaultModel = resolver.Resolve(workspace);
            var embeddingModel = resolver.Resolve(workspace, embedding: true);

            Assert.Equal("qwen3-coder-plus", defaultModel.Id);
            Assert.False(defaultModel.IsEmbeddingModel);
            Assert.Equal("text-embedding-v4", embeddingModel.Id);
            Assert.True(embeddingModel.IsEmbeddingModel);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
