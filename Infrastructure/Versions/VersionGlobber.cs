using System.Text;
using System.Text.RegularExpressions;

namespace DeletePackageVersionsAction.Infrastructure.Versions;

/// <summary>
/// Intended to allow glob-matching for versions
/// </summary>
public static class VersionGlobber
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Checks whether <paramref name="input"/> matches the glob pattern.
	/// <para>
	/// Supports *, ?, [], [!], {a,b,c}. Default: case-insensitive
	/// </para>
	/// </summary>
	public static bool IsMatch(string input, string pattern, bool caseInsensitive = true)
	{
		return Regex.IsMatch(
			input ?? string.Empty,
			"^" + GlobToRegex(pattern ?? string.Empty) + "$",
			GetRegexOptions(caseInsensitive),
			DefaultTimeout);
	}

	/// <summary>
	/// Creates a predicate function that determines whether a string matches the specified include 
	/// and exclude glob patterns.
	/// </summary>
	/// <param name="include">A collection of glob patterns that define which strings should be included. 
	/// If null or empty, all strings are considered included (subject to exclude patterns).</param>
	/// <param name="include">A collection of glob patterns that define which strings should be excluded.
	/// If null or empty, no strings are excluded based on exclude patterns.</param>
	/// <param name="caseInsensitive">
	/// When true, pattern matching is case-insensitive; otherwise, it is case-sensitive. Default is true.
	/// </param>
	/// <returns>
	/// A predicate function that returns true when the input string matches any include pattern 
	/// (or all if no includes are specified) 
	/// and does not match any exclude pattern.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The returned function evaluates strings against the pattern rules in the following order:
	/// </para>
	/// <list type="number">
	/// <item>
	/// <description>
	/// If no include patterns are provided, all strings are considered included
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// If include patterns exist, the string must match at least one include pattern
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// The string must not match any exclude patterns (if exclude patterns are provided)
	/// </description>
	/// </item>
	/// </list>
	/// <para>
	/// Supported glob features include: *, ?, character classes [...], negation [!...], and alternation {a,b,c}.
	/// </para>
	/// <example>
	/// The following example shows how to create a matcher that includes "test-*" patterns
	/// but excludes "test-backup" and "test-temp":
	/// <code>
	/// var matcher = VersionGlobber.CreateMatcher(
	///     include: new[] { "test-*" },
	///     exclude: new[] { "test-backup", "test-temp" });
	/// 
	/// bool result1 = matcher("test-v1"); // returns true
	/// bool result2 = matcher("test-backup"); // returns false
	/// </code>
	/// </example>
	/// </remarks>
	public static Func<string, bool> CreateMatcher(
	IEnumerable<string>? include = null,
	IEnumerable<string>? exclude = null,
	bool caseInsensitive = true)
	{
		var inc = CompilePatterns(include, caseInsensitive);
		var exc = CompilePatterns(exclude, caseInsensitive);

		return s =>
		{
			bool included = inc.Count == 0 || inc.Any(rx => rx.IsMatch(s));

			return included && (exc.Count == 0 || exc.All(rx => !rx.IsMatch(s)));
		};
	}

	/// <summary>
	/// Filters a sequence based on include/exclude globs while preserving its original order
	/// </summary>
	public static IReadOnlyList<string> Filter(
		IEnumerable<string> inputs,
		IEnumerable<string>? include = null,
		IEnumerable<string>? exclude = null,
		bool caseInsensitive = true)
	{
		var list = inputs.ToList();

		List<Regex> includeRegexes = CompilePatterns(include, caseInsensitive);
		List<Regex> excludeRegexes = CompilePatterns(exclude, caseInsensitive);

		// If no include is specified, that means that everything is permitted (before excludes)
		IEnumerable<string> candidate = includeRegexes.Count == 0
			? list
			: list.Where(s => includeRegexes.Any(rx => rx.IsMatch(s)));

		// Remove excludes
		if (excludeRegexes.Count > 0)
		{
			candidate = candidate.Where(s => excludeRegexes.All(rx => !rx.IsMatch(s)));
		}

		return [.. candidate];
	}

	/// <summary>
	/// Converts a glob into a regex expression (without ^$ anchors)
	/// </summary>
	public static string GlobToRegex(string pattern)
	{
		var sb = new StringBuilder();
		int i = 0;

		while (i < pattern.Length)
		{
			char c = pattern[i];

			switch (c)
			{
				case '\\':
					if (i + 1 < pattern.Length)
					{
						// Escape next char literally for regex
						char lit = pattern[i + 1];

						if ("[]{}().+*?^$|\\".Contains(lit))
						{
							sb.Append('\\');
						}

						sb.Append(lit);
						i += 2;
					}
					else
					{
						// Trailing backslash -> literal '\'
						sb.Append("\\\\");
						i++;
					}
					break;

				case '*':

					// Group multiple consecutive *
					while (i < pattern.Length && pattern[i] == '*')
					{
						i++;
					}

					sb.Append(".*");
					break;

				case '?':
					sb.Append('.');
					i++;
					break;

				case '[':
					int end = FindClosingBracket(pattern, i + 1);

					if (end > i)
					{
						string raw = pattern.Substring(i + 1, end - i - 1);

						if (string.IsNullOrEmpty(raw) || raw == "!")
						{
							sb.Append("\\[");
							i++;
							break;
						}

						string content = BuildRegexCharClass(raw, out bool neg);

						if (content.Length == 0)
						{
							sb.Append("\\[");
							i++;
							break;
						}

						sb.Append('[');

						if (neg)
						{
							sb.Append('^');
						}

						sb.Append(content).Append(']');
						i = end + 1;
					}
					else
					{
						sb.Append("\\[");
						i++;
					}
					break;

				case '{':
					int endBrace = FindClosingBrace(pattern, i + 1);

					if (endBrace > i)
					{
						string inner = pattern.Substring(i + 1, endBrace - i - 1);
						var alts = SplitByCommaOutsideBraces(inner)
							.Select(GlobToRegex)
							.ToArray();

						sb.Append("(?:").Append(string.Join("|", alts)).Append(')');
						i = endBrace + 1;
					}
					else
					{
						// Not a closing }, handle '{' as literal
						sb.Append("\\{");
						i++;
					}
					break;

				case '.':
				case '+':
				case '(':
				case ')':
				case '^':
				case '$':
				case '|':
					sb.Append('\\').Append(c);
					i++;
					break;

				case '}':
				case ']':
					sb.Append('\\').Append(c);
					i++;
					break;

				default:
					sb.Append(c);
					i++;
					break;
			}
		}

		return sb.ToString();
	}

	private static string BuildRegexCharClass(string cls, out bool negated)
	{
		negated = false;

		if (string.IsNullOrEmpty(cls))
		{
			return string.Empty;
		}

		int i = 0;

		// Allows for negation (! or ^) at the beginning
		if (cls[i] is '!' or '^')
		{
			negated = true;
			i++;
		}

		var sb = new StringBuilder(cls.Length);

		// Whether we have a range operatior beginning
		bool havePrevChar = false;

		// Last unescaped literal char
		char prevChar = '\0';

		while (i < cls.Length)
		{
			char ch = cls[i++];

			if (ch == '\\')
			{
				// Next literal char
				if (i < cls.Length)
				{
					char lit = cls[i++];
					if (lit is '\\' or ']' or '^' or '-')
					{
						sb.Append('\\');
					}

					sb.Append(lit);

					// Escapes counts towards previous chars to allow ranges
					havePrevChar = true;
					prevChar = lit;
				}
				else
				{
					// Trailing backslash
					sb.Append("\\\\");
					havePrevChar = true;
					prevChar = '\\';
				}

				continue;
			}

			if (ch == '-')
			{
				// Only a range when there is a left character, then we have not reached the end yet
				// and the next char is not ']'
				bool canBeRange = havePrevChar && i < cls.Length && cls[i] != ']';

				if (canBeRange)
				{
					// Peek next character and take escapes into account
					int j = i;
					char next = j < cls.Length && cls[j] == '\\' && j + 1 < cls.Length ? cls[j + 1] : cls[j];

					// Prevents a reversed range: prevChar > next => '-' literal
					if (prevChar <= next)
					{
						// Encountered a range separator
						sb.Append('-');      // echter Range-Separator

						// Range is consumed; Next char is a new end
						havePrevChar = false;

						continue;
					}

					// Assume a literal
					sb.Append("\\-");
					havePrevChar = true;
					prevChar = '-';
				}
				else
				{
					sb.Append("\\-");
					havePrevChar = true;
					prevChar = '-';
				}

				continue;
			}

			// Escape for special cases in classes
			if (ch is ']' or '^')
			{
				sb.Append('\\').Append(ch);
			}
			else
			{
				sb.Append(ch);
			}

			havePrevChar = true;
			prevChar = ch;
		}

		return sb.ToString();
	}

	private static List<Regex> CompilePatterns(IEnumerable<string>? patterns, bool caseInsensitive)
	{
		var result = new List<Regex>();

		if (patterns is null)
		{
			return result;
		}

		foreach (var p in patterns.SelectMany(SplitByCommaOutsideBraces))
		{
			if (string.IsNullOrWhiteSpace(p))
			{
				continue;
			}

			var rx = new Regex(
				"^" + GlobToRegex(p.Trim()) + "$",
				GetRegexOptions(caseInsensitive),
				DefaultTimeout);

			result.Add(rx);
		}

		return result;
	}

	private static int FindClosingBracket(string s, int from)
	{
		for (int i = from; i < s.Length; i++)
		{
			if (s[i] == '\\')
			{
				i++;

				continue;
			}

			if (s[i] == ']')
			{
				return i;
			}
		}
		return -1;
	}

	private static int FindClosingBrace(string s, int from)
	{
		int depth = 1;

		for (int i = from; i < s.Length; i++)
		{
			if (s[i] == '\\')
			{
				i++;

				continue;
			}

			if (s[i] == '{')
			{
				depth++;
			}
			else if (s[i] == '}' && --depth == 0)
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Splits a string at commas that are NOT within {...} blocks
	/// </summary>
	private static IEnumerable<string> SplitByCommaOutsideBraces(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			yield break;
		}

		var sb = new StringBuilder();
		int depth = 0;

		for (int i = 0; i < input.Length; i++)
		{
			char ch = input[i];

			if (ch == '\\')
			{
				// Copy the next escaped char as literal if it exists
				if (i + 1 < input.Length)
				{
					sb.Append('\\').Append(input[i + 1]);
					i++;
				}
				else
				{
					// Trailing backslash
					sb.Append('\\');
				}

				continue;
			}

			if (ch == '{')
			{
				depth++;
				sb.Append(ch);
			}
			else if (ch == '}')
			{
				depth = Math.Max(0, depth - 1);
				sb.Append(ch);
			}
			else if (ch == ',' && depth == 0)
			{
				yield return sb.ToString();

				sb.Clear();
			}
			else
			{
				sb.Append(ch);
			}
		}

		if (sb.Length > 0)
		{
			yield return sb.ToString();
		}
	}

	private static RegexOptions GetRegexOptions(bool caseInsensitive)
	{
		RegexOptions opts = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

		return opts | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
	}
}
