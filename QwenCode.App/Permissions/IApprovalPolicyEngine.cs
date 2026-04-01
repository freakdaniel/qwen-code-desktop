using QwenCode.App.Models;

namespace QwenCode.App.Permissions;

public interface IApprovalPolicyEngine
{
    QwenApprovalDecision Evaluate(
        ApprovalCheckContext context,
        QwenApprovalProfile approvalProfile);
}
