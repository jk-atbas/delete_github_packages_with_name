using DeletePackageVersionsAction.Infrastructure.Types.Responses;
using NuGet.Versioning;

namespace DeletePackageVersionsAction.Infrastructure.Types;

public readonly record struct GitHubPackage(long Id, NuGetVersion Version)
{
	public static bool TryParse(GitHubPackageVersionResponse response, out GitHubPackage? package)
	{
		if (!NuGetVersion.TryParseStrict(response.Name, out NuGetVersion? version))
		{
			package = null;

			return false;
		}

		package = new GitHubPackage(response.Id, version);

		return true;
	}
}
