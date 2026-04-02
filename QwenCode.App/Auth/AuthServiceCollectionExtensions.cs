using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuthUrlLauncher, ShellAuthUrlLauncher>();
        services.AddSingleton<IQwenOAuthCredentialStore, FileQwenOAuthCredentialStore>();
        services.AddSingleton<IQwenOAuthTokenManager, QwenOAuthTokenManager>();
        services.AddSingleton<IAuthFlowService, AuthFlowService>();
        return services;
    }
}
