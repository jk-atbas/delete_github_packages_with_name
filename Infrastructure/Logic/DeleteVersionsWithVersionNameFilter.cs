using DeletePackageVersionsAction.Infrastructure.Logging;
using DeletePackageVersionsAction.Infrastructure.Settings;
using DeletePackageVersionsAction.Infrastructure.Types;
using DeletePackageVersionsAction.Infrastructure.Uris;
using DeletePackageVersionsAction.Infrastructure.Versions;
using Microsoft.Extensions.Logging;

namespace DeletePackageVersionsAction.Infrastructure.Logic;

public sealed class DeleteVersionsWithVersionNameFilter(
	IHttpClientFactory clientFactory,
	FetchAllPackageVersions packageVersions,
	GitHubInputs inputs,
	ILogger<DeleteVersionsWithVersionNameFilter> logger)
{
	public async Task<bool> Execute(CancellationToken cancellationToken)
	{
		try
		{
			if (!inputs.UserNameOrOrgNameSet)
			{
				logger.LogWarningGitHub("No user or orgname was found!");

				throw new InvalidOperationException("Either INPUT_USERNAME or INPUT_ORGNAME must be set");
			}

			await DeleteVersions(cancellationToken);

			return true;
		}
		catch (Exception e)
		{
			logger.LogErrorGitHub(e);

			return false;
		}
	}

	private async Task DeleteVersions(CancellationToken cancellationToken)
	{
		Func<string, bool> isAffected = VersionGlobber.CreateMatcher(inputs.VersionFilter, inputs.ExcludeFilter);
		HashSet<long> attemptedVersions = [];

		using HttpClient client = clientFactory.CreateClient(GeneralSettings.GeneralTopic);

		int total = 0, deleted = 0, skipped = 0, failed = 0;

		// Todo: When need arises paralellize this; Eg. when a lots versions are to be deleted
		await foreach (GitHubPackage package in packageVersions.StreamVersions(cancellationToken))
		{
			total++;

			if (!isAffected.Invoke(package.Version) || attemptedVersions.Contains(package.Id))
			{
				skipped++;

				continue;
			}

			if (await TryDeleteVersion(client, package, cancellationToken))
			{
				deleted++;
			}
			else
			{
				failed++;
			}

			attemptedVersions.Add(package.Id);
		}

		logger.LogNoticeGitHub($"Delete package versions summary: " +
			$"total={total}, deleted={deleted}, skipped={skipped}, failed={failed}");
	}

	private async Task<bool> TryDeleteVersion(HttpClient client, GitHubPackage package, CancellationToken cancellationToken)
	{
		Uri packageUriToDelete = UriHelper.BuildRelativeVersionsUri(inputs, versionId: package.Id);

		using HttpResponseMessage response = await client.DeleteAsync(packageUriToDelete, cancellationToken);

		if (response.IsSuccessStatusCode)
		{
			logger.LogNoticeGitHub($"Deleted {inputs.PackageName} Version={package.Version} successfully");

			return true;
		}

		logger.LogWarningGitHub(
			$"Deletion attempt for {inputs.PackageName} Version={package.Version} was not successful: " +
			$"{(int) response.StatusCode} {response.ReasonPhrase}");

		return false;
	}
}
