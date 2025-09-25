using DeletePackageVersionsAction.Infrastructure.Types.Responses;
using System.Net.Http.Headers;

namespace DeletePackageVersionsAction.Infrastructure.Extensions;

internal static class HttpExtensions
{
	/// <summary>
	/// Tries to parse the link header in <see cref="HttpResponseHeaders"/> 
	/// for a <see cref="GitHubPackageVersionResponse"/> request
	/// <para>
	/// See docu <see href="https://docs.github.com/en/rest/using-the-rest-api/using-pagination-in-the-rest-api?apiVersion=2022-11-28#using-link-headers"/>
	/// </para>
	/// </summary>
	/// <param name="headers">All the response header</param>
	/// <returns></returns>
	public static Dictionary<string, string> ParseLinkHeader(this HttpResponseHeaders headers)
	{
		if (!headers.TryGetValues("Link", out IEnumerable<string>? rawLinkHeaders))
		{
			return [];
		}

		Dictionary<string, string> result = [];

		foreach (string linkHeader in rawLinkHeaders)
		{
			foreach (string part in linkHeader.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				string[] segments = part.Split(';', 2, StringSplitOptions.RemoveEmptyEntries);

				if (segments.Length != 2)
				{
					continue;
				}

				string rel = segments[1]
					.Trim()
					.Replace("rel=", string.Empty, StringComparison.OrdinalIgnoreCase)
					.Replace("\"", string.Empty);

				string url = segments[0]
					.Trim()
					.Replace("<", string.Empty)
					.Replace(">", string.Empty);

				if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(url))
				{
					continue;
				}

				result[rel] = url;
			}
		}

		return result;
	}
}
