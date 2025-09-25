using DeletePackageVersionsAction.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using static DeletePackageVersionsAction.Infrastructure.Settings.GitHubInputs;

namespace DeletePackageVersionsAction.Infrastructure.Logic;

public sealed class DeleteVersionsWithVersionNameFilter(
	HttpClient client,
	FetchAllPackageVersions packageVersions,
	ILogger<DeleteVersionsWithVersionNameFilter> logger)
{
	public async Task<bool> Execute(CancellationToken cancellationToken)
	{
		try
		{
			if (!UserNameOrOrgNameSet)
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
