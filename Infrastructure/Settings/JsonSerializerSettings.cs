using System.Text.Json;

namespace DeletePackageVersionsAction.Infrastructure.Settings;

internal static class JsonSerializerSettings
{
	public static readonly JsonSerializerOptions DefaultGitHub = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
	};
}
