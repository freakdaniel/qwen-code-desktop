using QwenCode.App.Models;

namespace QwenCode.App.Permissions;

public interface IApprovalPolicyEngine
{
    ApprovalDecision Evaluate(
        ApprovalCheckContext context,
        ApprovalProfile approvalProfile);
}
