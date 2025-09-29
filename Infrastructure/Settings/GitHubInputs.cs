namespace DeletePackageVersionsAction.Infrastructure.Settings;

public sealed class GitHubInputs(IEnvironmentVariableProvider environment)
{
	public string GithubApiKey => environment.GetEnvironmentVariable("INPUT_GITHUB_API_KEY")
		?? environment.GetEnvironmentVariable(
			"INPUT_GITHUB_API_KEY",
			EnvironmentVariableTarget.User | EnvironmentVariableTarget.Process)
		?? throw new InvalidOperationException("No INPUT_GITHUB_API_KEY env var was set");

	public string PackageType => environment.GetEnvironmentVariable("INPUT_PACKAGE_TYPE") ?? "nuget";

	public string? UserName => environment.GetEnvironmentVariable("INPUT_USERNAME");

	public string? OrgName => environment.GetEnvironmentVariable("INPUT_ORGNAME");

	public string PackageName => environment.GetEnvironmentVariable("INPUT_PACKAGE_NAME")
		?? throw new InvalidOperationException("No INPUT_PACKAGE_TYPE env var was set");

	public string[]? VersionFilter => environment.GetEnvironmentVariable("INPUT_VERSION_FILTER")?.Split(
		";",
		StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	public string[]? ExcludeFilter => environment.GetEnvironmentVariable("INPUT_VERSION_EXCLUDE_FILTER")?.Split(
		";",
		StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	public bool UserNameOrOrgNameSet => !string.IsNullOrWhiteSpace(UserName)
		|| !string.IsNullOrWhiteSpace(OrgName);
}
