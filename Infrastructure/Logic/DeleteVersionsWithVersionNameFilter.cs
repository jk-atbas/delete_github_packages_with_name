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

			var versions = await packageVersions.GetAllVersions(cancellationToken);
			await DeleteVersions(versions, cancellationToken);

			return true;
		}
		catch (Exception e)
		{
			logger.LogErrorGitHub(e);

			return false;
		}
	}

	private async Task DeleteVersions(GitHubPackage[] versions, CancellationToken cancellationToken)
	{
		HashSet<string> affectedVersions = VersionGlobber.Filter(
			versions.Select(ghp => ghp.Version),
			inputs.VersionFilter,
			inputs.ExcludeFilter)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		if (affectedVersions.Count == 0)
		{
			logger.LogNoticeGitHub("Nothing to delete was found");

			return;
		}

		IEnumerable<GitHubPackage> packagesToDelete = versions.Where(ghp => affectedVersions.Contains(ghp.Version));

		using HttpClient client = clientFactory.CreateClient(GeneralSettings.GeneralTopic);

		foreach (var package in packagesToDelete)
		{
			Uri packageUriToDelete = UriHelper.BuildRelativeVersionsUri(inputs, versionId: package.Id);

			using HttpResponseMessage response = await client.DeleteAsync(packageUriToDelete, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				logger.LogWarningGitHub(
					$"Deletion attempt for {inputs.PackageName} Version={package.Version} was not successful");

				continue;
			}

			logger.LogNoticeGitHub($"Deleted {inputs.PackageName} Version={package.Version} successfully");
		}
	}
}
