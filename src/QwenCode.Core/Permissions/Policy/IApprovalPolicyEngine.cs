using QwenCode.Core.Models;

namespace QwenCode.Core.Permissions;

/// <summary>
/// Defines the contract for Approval Policy Engine
/// </summary>
public interface IApprovalPolicyEngine
{
    /// <summary>
    /// Executes evaluate
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="approvalProfile">The approval profile</param>
    /// <returns>The resulting approval decision</returns>
    ApprovalDecision Evaluate(
        ApprovalCheckContext context,
        ApprovalProfile approvalProfile);
}
