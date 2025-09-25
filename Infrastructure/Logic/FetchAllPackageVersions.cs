using DeletePackageVersionsAction.Infrastructure.Extensions;
using DeletePackageVersionsAction.Infrastructure.Logging;
using DeletePackageVersionsAction.Infrastructure.Settings;
using DeletePackageVersionsAction.Infrastructure.Types;
using DeletePackageVersionsAction.Infrastructure.Types.Responses;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DeletePackageVersionsAction.Infrastructure.Logic;
public sealed class FetchAllPackageVersions(
	IHttpClientFactory clientFactory,
	GitHubInputs inputs,
	ILogger<FetchAllPackageVersions> logger)
{
	public async Task<GitHubPackage[]> GetAllVersions(CancellationToken cancellationToken)
	{
		string orgOrUserDomain = inputs.UserName is not null
			? $"users/{inputs.UserName}"
			: $"orgs/{inputs.OrgName}";

#if DEBUG
		const int pageSize = 1;
#else
		const int pageSize = 30;
#endif

		Uri relativePackageVersionsUri = new(
				$"{orgOrUserDomain}/packages/{inputs.PackageType}/{inputs.PackageName}/versions?per_page={pageSize}",
				UriKind.Relative);

		logger.LogNoticeGitHub($"Fetching all version infos for {inputs.PackageType} package {inputs.PackageName}");

		var result = await FetchVersions(relativePackageVersionsUri, cancellationToken);

		return [.. result
			.Select(ghResponse => new GitHubPackage(ghResponse.Id, ghResponse.Name))];
	}

	private async Task<GitHubPackageVersionResponse[]> FetchVersions(
		Uri relativeUri,
		CancellationToken cancellationToken)
	{
		var (responses, links) = await GetResults(relativeUri.OriginalString, cancellationToken);

		if (links.Count == 0 || !links.TryGetValue("next", out string? nextUrl) || string.IsNullOrWhiteSpace(nextUrl))
		{
			logger.LogNoticeGitHub("Only single page result was received");

			return responses;
		}

		List<GitHubPackageVersionResponse> all = [.. responses];

		while (!string.IsNullOrWhiteSpace(nextUrl))
		{
			var (pageResults, pageLinks) = await GetResults(nextUrl, cancellationToken);
			all.AddRange(pageResults);

			if (!pageLinks.TryGetValue("next", out nextUrl))
			{
				break;
			}
		}

		return [.. all];
	}

	private async Task<(GitHubPackageVersionResponse[] Results, Dictionary<string, string> LinkHeaders)> GetResults(
		string url,
		CancellationToken cancellationToken)
	{
		using HttpClient client = clientFactory.CreateClient(GeneralSettings.GeneralTopic);
		using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using Stream stream = await response.Content
			.ReadAsStreamAsync(cancellationToken);

		var packageVersions = await JsonSerializer.DeserializeAsync<GitHubPackageVersionResponse[]>(
			stream,
			JsonSerializerSettings.DefaultGitHub,
			cancellationToken) ?? [];

		return (packageVersions, response.Headers.ParseLinkHeader());
	}
}
