using DeletePackageVersionsAction.Infrastructure.Settings;
using System.Collections.Specialized;
using System.Web;

namespace DeletePackageVersionsAction.Infrastructure.Uris;

public static class UriHelper
{
	public static Uri BuildRelativeVersionsUri(GitHubInputs inputs, int? pageSize = null, long? versionId = null)
	{
		string orgOrUserDomain = inputs.UserName is not null
			? $"users/{inputs.UserName}"
			: $"orgs/{inputs.OrgName}";

		string versionAppendage = versionId is not null ? "/" + versionId : string.Empty;

		string rawUriString =
			$"{orgOrUserDomain}/packages/{inputs.PackageType}/{inputs.PackageName}/versions{versionAppendage}";

		if (pageSize is not null)
		{
			NameValueCollection query = HttpUtility.ParseQueryString(string.Empty);
			query["per_page"] = pageSize.Value.ToString();
			rawUriString += "?" + query.ToString();
		}

		return new Uri(rawUriString, UriKind.Relative);
	}
}
