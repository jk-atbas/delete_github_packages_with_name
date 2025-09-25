using DeletePackageVersionsAction.Infrastructure.Extensions;
using DeletePackageVersionsAction.Infrastructure.Logging;
using DeletePackageVersionsAction.Infrastructure.Types.Responses;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Web;
using static DeletePackageVersionsAction.Infrastructure.Settings.GitHubInputs;

namespace DeletePackageVersionsAction.Infrastructure.Logic;

public sealed class DeleteVersionsWithVersionNameFilter(
	HttpClient client,
	ILogger<DeleteVersionsWithVersionNameFilter> logger)
{
	private readonly JsonSerializerOptions options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
	};

	public async Task<bool> Execute(CancellationToken cancellationToken)
	{
		try
		{
			if (!UserNameOrOrgNameSet)
			{
				logger.LogWarningGitHub("No user or orgname was found!");
				throw new InvalidOperationException("Either INPUT_USERNAME or INPUT_ORGNAME must be set");
			}

			string orgOrUserDomain = UserName is not null
				? $"users/{UserName}"
				: $"orgs/{OrgName}";

			int pageSize = 30;
#if DEBUG
			pageSize = 1;
#endif

			Uri relativePackageVersionsUri = new(
				$"{orgOrUserDomain}/packages/{PackageType}/{PackageName}/versions?per_page={pageSize}",
				UriKind.Relative);

			logger.LogNoticeGitHub($"Fetching all version infos for {PackageType} package {PackageName}");

			var result = await FetchVersions(relativePackageVersionsUri, cancellationToken);

			return true;
		}
		catch (Exception e)
		{
			logger.LogErrorGitHub(e);

			return false;
		}
	}

	private async Task<GitHubPackageVersionResponse[]> FetchVersions(
		Uri relativeUri,
		CancellationToken cancellationToken)
	{
		var (initialResponses, links) = await GetResults(relativeUri.OriginalString, cancellationToken);

		if (links.Keys.Count > 0)
		{
			if (!links.TryGetValue("next", out string? nextUrl) || !links.TryGetValue("last", out string? lastUrl))
			{
				return initialResponses;
			}

			List<GitHubPackageVersionResponse> allPaginatedResults = [];
			allPaginatedResults.AddRange(initialResponses);

			Uri lastUri = new Uri(lastUrl);
			string? lastPageString = HttpUtility.ParseQueryString(lastUri.Query).Get("page");

			if (string.IsNullOrWhiteSpace(lastPageString))
			{
				return initialResponses;
			}

			if (!int.TryParse(lastPageString, out int lastPage)
				&& lastPage <= 0)
			{
				return initialResponses;
			}

			// Page 2 is the next to request
			int iteration = 2;

			while (nextUrl is not null && iteration <= lastPage)
			{
				var (results, linkHeaders) = await GetResults(nextUrl, cancellationToken);
				allPaginatedResults.AddRange(results);

				if (!linkHeaders.TryGetValue("next", out string? nextIterationUrl))
				{
					break;
				}

				nextUrl = nextIterationUrl;

				iteration++;
			}

			return [.. allPaginatedResults];
		}
		else
		{
			logger.LogNoticeGitHub("Only single page result was received");

			return initialResponses;
		}
	}

	private async Task<HttpResponseMessage> FetchVersion(string url, CancellationToken cancellationToken)
	{
		HttpResponseMessage httpResponse = await client.GetAsync(url, cancellationToken);
		httpResponse.EnsureSuccessStatusCode();

		return httpResponse;
	}

	private async Task<(GitHubPackageVersionResponse[] Results, Dictionary<string, string> LinkHeaders)> GetResults(
		string url,
		CancellationToken cancellationToken)
	{
		using HttpResponseMessage result = await FetchVersion(url, cancellationToken);
		await using Stream stream = await result.Content
			.ReadAsStreamAsync(cancellationToken);

		var packageVersions = await JsonSerializer.DeserializeAsync<GitHubPackageVersionResponse[]>(
			stream,
			options,
			cancellationToken) ?? [];

		return (packageVersions, result.Headers.ParseLinkHeader());
	}
}
