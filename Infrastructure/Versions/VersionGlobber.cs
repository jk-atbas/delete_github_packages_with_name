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
						// escape next char literally for regex
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
						sb.Append("\\\\"); // trailing backslash -> literal '\'
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
					int end = FindClosingBracket(pattern, i + 1); // s. Fix unten
					if (end > i)
					{
						string raw = pattern.Substring(i + 1, end - i - 1);

						if (string.IsNullOrEmpty(raw) || raw == "!")
						{
							// siehe (5): fehlerhafte/ leere Klasse -> behandle '[' literal
							sb.Append("\\[");
							i++; // nur das '[' konsumieren, ']' bleibt normaler Text
							break;
						}

						string content = BuildRegexCharClass(raw, out bool neg);

						// noch einmal absichern: leere Klasse nach Verarbeitung ist ungültig
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
					// Alternativen {a,b,c} – jede Alternative darf wieder ein Glob sein
					int endBrace = FindClosingBrace(pattern, i + 1);

					if (endBrace > i)
					{
						string inner = pattern.Substring(i + 1, endBrace - i - 1);
						var alts = SplitByCommaOutsideBraces(inner)
							.Select(GlobToRegex) // recursive
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

		// Negation am Anfang: ! oder ^ zulassen
		if (cls[i] is '!' or '^')
		{
			negated = true;
			i++;
		}

		var sb = new StringBuilder(cls.Length);
		bool havePrevChar = false;     // haben wir ein linkes Ende für einen Range?
		char prevChar = '\0';          // letztes literal Zeichen (unescaped, bereits emittiert)

		while (i < cls.Length)
		{
			char ch = cls[i++];

			if (ch == '\\')
			{
				// literal nächstes Zeichen
				if (i < cls.Length)
				{
					char lit = cls[i++];
					if (lit is '\\' or ']' or '^' or '-')
					{
						sb.Append('\\');
					}

					sb.Append(lit);
					// escaped zählt als "vorheriges Zeichen" für mögliche Ranges
					havePrevChar = true;
					prevChar = lit;
				}
				else
				{
					// trailing backslash
					sb.Append("\\\\");
					havePrevChar = true;
					prevChar = '\\';
				}

				continue;
			}

			if (ch == '-')
			{
				// Nur Range, wenn: es gibt ein linkes Zeichen, wir sind nicht am Ende,
				// und der nächste Char ist nicht ']'
				bool canBeRange = havePrevChar && i < cls.Length && cls[i] != ']';

				if (canBeRange)
				{
					// Peek nächstes Zeichen (unter Berücksichtigung von Escape)
					int j = i;
					char next = j < cls.Length && cls[j] == '\\' && j + 1 < cls.Length ? cls[j + 1] : cls[j];

					// Reversed Range verhindern: prevChar > next => '-' literal
					if (prevChar <= next)
					{
						sb.Append('-');      // echter Range-Separator
						havePrevChar = false; // Range „verbraucht“ prevChar; nächstes Zeichen wird neues Ende

						continue;
					}

					// sonst literal:
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

			// Escape für Sonderfälle in Klassen
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

		if (patterns == null)
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
				// escaped: nächstes Zeichen (falls vorhanden) wörtlich übernehmen, inkl. Komma
				if (i + 1 < input.Length)
				{
					sb.Append('\\').Append(input[i + 1]);
					i++;
				}
				else
				{
					sb.Append('\\'); // trailing backslash
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
		return RegexOptions.CultureInvariant | (caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
	}
}
