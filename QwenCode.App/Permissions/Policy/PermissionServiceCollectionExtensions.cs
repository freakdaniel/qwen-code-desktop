using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Permissions;

public static class PermissionServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionServices(this IServiceCollection services)
    {
        services.AddSingleton<IApprovalPolicyEngine, ApprovalPolicyService>();

        return services;
    }
}
