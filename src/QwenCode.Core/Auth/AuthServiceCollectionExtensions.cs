namespace QwenCode.Core.Auth;

/// <summary>
/// Provides extension members for Auth Service Collection
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Executes add auth services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuthUrlLauncher, ShellAuthUrlLauncher>();
        services.AddSingleton<IQwenOAuthCredentialStore, FileQwenOAuthCredentialStore>();
        services.AddSingleton<IQwenOAuthTokenManager, QwenOAuthTokenManager>();
        services.AddSingleton<IAuthFlowService, AuthFlowService>();
        return services;
    }
}
