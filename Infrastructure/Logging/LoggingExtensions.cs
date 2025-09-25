using Microsoft.Extensions.Logging;

namespace DeletePackageVersionsAction.Infrastructure.Logging;

internal static class LoggingExtensions
{
	private const string LoggingTemplate = "{level}{message}";

	public static void LogNoticeGitHub(this ILogger logger, string message)
	{
		logger.LogInformation(LoggingTemplate, GitHubLogLevels.Notice, Escape(message));
	}

	public static void LogWarningGitHub(this ILogger logger, string message)
	{
		logger.LogWarning(LoggingTemplate, GitHubLogLevels.Warning, Escape(message));
	}

	public static void LogErrorGitHub(this ILogger logger, Exception exception)
	{
		logger.LogError(exception, LoggingTemplate, GitHubLogLevels.Error, Escape(exception.Message));
	}

	private static string Escape(string input)
	{
		return input.Replace("\r", "%0D").Replace("\n", "%0A").Replace("]", "%5D").Replace(":", "%3A");
	}

	private static class GitHubLogLevels
	{
		internal const string Notice = "::notice::";

		internal const string Warning = "::warning::";

		internal const string Error = "::error::";
	}
}
