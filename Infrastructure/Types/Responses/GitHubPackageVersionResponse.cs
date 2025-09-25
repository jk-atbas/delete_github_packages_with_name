namespace DeletePackageVersionsAction.Infrastructure.Types.Responses;

public sealed class GitHubPackageVersionResponse
{
	private const string ExampleUrl = "http://some.url";

	public long Id { get; set; }

	/// <summary>
	/// Represents the actual version number as visible in github ui; eg 1.1.3
	/// </summary>
	public string Name { get; set; } = string.Empty;

	public Uri Url { get; set; } = new Uri(ExampleUrl);

	public Uri PackageHtmlUrl { get; set; } = new Uri(ExampleUrl);

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public Uri HtmlUrl { get; set; } = new Uri(ExampleUrl);

	public Dictionary<string, string> Metadata { get; set; } = [];
}
