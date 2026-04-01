using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Permissions;

public static class PermissionServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionServices(this IServiceCollection services)
    {
        services.AddSingleton<IApprovalPolicyEngine, QwenApprovalPolicyService>();

        return services;
    }
}
