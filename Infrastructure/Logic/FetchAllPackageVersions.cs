using DeletePackageVersionsAction.Infrastructure.Extensions;
using DeletePackageVersionsAction.Infrastructure.Logging;
using DeletePackageVersionsAction.Infrastructure.Settings;
using DeletePackageVersionsAction.Infrastructure.Types;
using DeletePackageVersionsAction.Infrastructure.Types.Responses;
using DeletePackageVersionsAction.Infrastructure.Uris;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DeletePackageVersionsAction.Infrastructure.Logic;

public sealed class FetchAllPackageVersions(
	IHttpClientFactory clientFactory,
	GitHubInputs inputs,
	ILogger<FetchAllPackageVersions> logger)
{
	public async IAsyncEnumerable<GitHubPackage> StreamVersions(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
#if DEBUG
		const int pageSize = 1;
#else
		const int pageSize = 30;
#endif

		using HttpClient client = clientFactory.CreateClient(GeneralSettings.GeneralTopic);

		Uri firstUri = UriHelper.BuildRelativeVersionsUri(inputs, pageSize);
		logger.LogNoticeGitHub($"Fetching all version infos for {inputs.PackageType} package {inputs.PackageName}");

		var firstLinks = await GetLinksOnly(client, firstUri, cancellationToken);

		if (!firstLinks.TryGetValue("last", out string? lastUrl) || string.IsNullOrWhiteSpace(lastUrl))
		{
			logger.LogNoticeGitHub("Only single page result was received");

			var (firstResults, _) = await GetResults(client, firstUri.OriginalString, cancellationToken);

			foreach (GitHubPackageVersionResponse response in firstResults)
			{
				yield return new GitHubPackage(response.Id, response.Name);
			}

			yield break;
		}

		string? url = lastUrl;
		int pageCounter = 1;

		while (!string.IsNullOrWhiteSpace(url))
		{
			logger.LogNoticeGitHub($"Request page backwards {pageCounter} for {inputs.PackageName}");

			var (pageResults, pageLinks) = await GetResults(client, url, cancellationToken);

			foreach (GitHubPackageVersionResponse pageResult in pageResults)
			{
				yield return new GitHubPackage(pageResult.Id, pageResult.Name);
			}

			if (!pageLinks.TryGetValue("prev", out url))
			{
				break;
			}

			pageCounter++;
		}
	}

	private async Task<(GitHubPackageVersionResponse[] Results, Dictionary<string, string> LinkHeaders)> GetResults(
		HttpClient client,
		string url,
		CancellationToken cancellationToken)
	{
		// Mark the response as completed after all headers where received; leads to better latency
		// The response body gets populated never the less
		// IMPORTANT: The Response Content must be accessed as a stream because of this!!!
		using HttpResponseMessage response = await client.GetAsync(
			url,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken);

		response.EnsureSuccessStatusCode();

		await using Stream stream = await response.Content
			.ReadAsStreamAsync(cancellationToken);

		var packageVersions = await JsonSerializer.DeserializeAsync<GitHubPackageVersionResponse[]>(
			stream,
			JsonSerializerSettings.DefaultGitHub,
			cancellationToken) ?? [];

		return (packageVersions, response.Headers.ParseLinkHeader());
	}

	private async Task<Dictionary<string, string>> GetLinksOnly(
		HttpClient client,
		Uri uri,
		CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await client.GetAsync(
			uri,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken);

		response.EnsureSuccessStatusCode();

		return response.Headers.ParseLinkHeader();
	}
}
