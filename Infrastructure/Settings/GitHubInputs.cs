namespace DeletePackageVersionsAction.Infrastructure.Settings;

internal static class GitHubInputs
{
	public static string NugetApiKey => Environment.GetEnvironmentVariable(
		"INPUT_NUGET_API_KEY",
		EnvironmentVariableTarget.User | EnvironmentVariableTarget.Process)
			?? throw new InvalidOperationException("No INPUT_NUGET_API_KEY env var was set");

	public static string PackageType => Environment.GetEnvironmentVariable("INPUT_PACKAGE_TYPE") ?? "nuget";

	public static string? UserName => Environment.GetEnvironmentVariable("INPUT_USERNAME");

	public static string? OrgName => Environment.GetEnvironmentVariable("INPUT_ORGNAME");

	public static string PackageName => Environment.GetEnvironmentVariable("INPUT_PACKAGE_NAME")
		?? throw new InvalidOperationException("No INPUT_PACKAGE_TYPE env var was set");

	public static string? VersionFilter => Environment.GetEnvironmentVariable("INPUT_VERSION_FILTER");

	public static bool UserNameOrOrgNameSet => !string.IsNullOrWhiteSpace(UserName)
		|| !string.IsNullOrWhiteSpace(OrgName);
}
