using DeployManager.GitHelper;
using Microsoft.Extensions.Logging;

namespace DeployManager.UI
{
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register the service for DI
        builder.Services.AddSingleton<GitService>();
        builder.Services.AddSingleton<ConfigService>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
