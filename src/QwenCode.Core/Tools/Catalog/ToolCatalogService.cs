using QwenCode.App.Compatibility;
using QwenCode.App.Models;
using QwenCode.App.Permissions;

namespace QwenCode.App.Tools;

/// <summary>
/// Represents the Tool Catalog Service
/// </summary>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="approvalPolicyService">The approval policy service</param>
public sealed class ToolCatalogService(
    QwenRuntimeProfileService runtimeProfileService,
    IApprovalPolicyEngine approvalPolicyService) : IToolRegistry
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting tool catalog snapshot</returns>
    public ToolCatalogSnapshot Inspect(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var tools = LoadDescriptors(runtimeProfile);

        return new ToolCatalogSnapshot
        {
            SourceMode = tools.Count > 0 ? "native-contracts" : "empty",
            TotalCount = tools.Count,
            AllowedCount = tools.Count(tool => tool.ApprovalState == "allow"),
            AskCount = tools.Count(tool => tool.ApprovalState == "ask"),
            DenyCount = tools.Count(tool => tool.ApprovalState == "deny"),
            Tools = tools
        };
    }

    private IReadOnlyList<ToolDescriptor> LoadDescriptors(QwenRuntimeProfile runtimeProfile) =>
        ToolContractCatalog.All
            .OrderBy(static tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(tool =>
            {
                var approval = approvalPolicyService.Evaluate(
                    new ApprovalCheckContext
                    {
                        ToolName = tool.Name,
                        Kind = tool.Kind,
                        ProjectRoot = runtimeProfile.ProjectRoot,
                        WorkingDirectory = runtimeProfile.ProjectRoot
                    },
                    runtimeProfile.ApprovalProfile);

                return new ToolDescriptor
                {
                    Name = tool.Name,
                    DisplayName = tool.DisplayName,
                    Kind = tool.Kind,
                    SourcePath = tool.ContractPath,
                    ApprovalState = approval.State,
                    ApprovalReason = approval.Reason
                };
            })
            .ToArray();
}
