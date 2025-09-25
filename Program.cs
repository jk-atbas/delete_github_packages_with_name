using DeletePackageVersionsAction.Infrastructure.Logging;
using DeletePackageVersionsAction.Infrastructure.Logic;
using DeletePackageVersionsAction.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Reflection;

namespace DeletePackageVersionsAction;

internal class Program
{
	private const string GhAccept = "application/vnd.github+json";
	private const string GhApiHeader = "X-GitHub-Api-Version";
	private const string GhApiVersion = "2022-11-28";

	private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
	private static readonly AssemblyName AssemblyName = CurrentAssembly.GetName();
	private static readonly Version Version = AssemblyName.Version ?? new Version(1, 0, 0);

	private static async Task Main(string[] args)
	{
		if (string.IsNullOrWhiteSpace(GitHubInputs.NugetApiKey))
		{
			throw new InvalidOperationException("No nuget api key was set!");
		}

		var host = Host.CreateDefaultBuilder(args);

		host.ConfigureServices(services =>
		{
			services.AddLogging();
			services.AddRedaction();

			services.AddSingleton<FetchAllPackageVersions>();
			services.AddSingleton<DeleteVersionsWithVersionNameFilter>();

			services
				.AddHttpClient(GeneralSettings.GeneralTopic, builder =>
				{
					builder.DefaultRequestHeaders.UserAgent.ParseAdd($"{AssemblyName.Name}_v{Version.ToString(3)}");
					builder.BaseAddress = new Uri("https://api.github.com/");
					builder.DefaultRequestHeaders.Accept.ParseAdd(GhAccept);
					builder.DefaultRequestHeaders.Add(GhApiHeader, GhApiVersion);

					builder.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
						"Bearer",
						GitHubInputs.NugetApiKey);
				})
				.AddExtendedHttpClientLogging()
				.AddStandardResilienceHandler();
		});

		IHost app = host.Build();
		CancellationToken cancellationToken = app.Services
			.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

		var logger = app.Services.GetRequiredService<ILogger<Program>>();
		var service = app.Services.GetRequiredService<DeleteVersionsWithVersionNameFilter>();

		bool wasSuccessful = await service.Execute(cancellationToken);

		if (!wasSuccessful)
		{
			logger.LogWarningGitHub("Deletion attempt was not successful!");
		}

		app.Run();
	}
}
