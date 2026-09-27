using GFlow.GitHub;
using GFlow.GitHub.Authentication;
using GFlow.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using MudBlazor.Services;
namespace GFlow;

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
		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddMudServices();
		builder.Services.AddSingleton<ISecureStorage>(SecureStorage.Default);
		builder.Services.AddSingleton<ISecureCredentialStore, MauiSecureCredentialStore>();
		builder.Services.AddGFlowGitHubAuthenticated();
		builder.Services.AddRepositoryWorkflowManagement();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
