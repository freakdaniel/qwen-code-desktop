namespace QwenCode.Core.Permissions;

/// <summary>
/// Provides extension members for Permission Service Collection
/// </summary>
public static class PermissionServiceCollectionExtensions
{
    /// <summary>
    /// Executes add permission services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddPermissionServices(this IServiceCollection services)
    {
        services.AddSingleton<IApprovalPolicyEngine, ApprovalPolicyService>();
        services.AddSingleton<IApprovalSessionRuleStore, ApprovalSessionRuleStore>();

        return services;
    }
}
