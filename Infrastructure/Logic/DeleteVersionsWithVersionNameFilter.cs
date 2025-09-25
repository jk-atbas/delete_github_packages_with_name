using DeletePackageVersionsAction.Infrastructure.Logging;
using DeletePackageVersionsAction.Infrastructure.Settings;
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

			var result = await packageVersions.GetAllVersions(cancellationToken);

			return true;
		}
		catch (Exception e)
		{
			logger.LogErrorGitHub(e);

			return false;
		}
	}
}
