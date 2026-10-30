#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OsLib;

/// <summary>
/// Formats CLI help according to https://github.com/Burkhardt/RAIkeep/blob/main/doc/cli-style.md.
/// This dependency-free source is also compiled into standalone Amafu; synchronize it
/// with scripts/sync-cli-style.py after edits. It does not initialize Os.Config.
/// </summary>
public static class CliHelpStyle
{
	/// <summary>A plain-text help row; cloud providers optionally receive individual colored badges.</summary>
	public sealed record Row(string Label, string Icon, string Description, IReadOnlyList<string>? Providers = null, string? DefaultProvider = null);

	/// <summary>Enables ANSI styling only for interactive stdout, respecting NO_COLOR and TERM=dumb.</summary>
	public static bool UseColor(TextWriter? output = null) =>
		(output is null || ReferenceEquals(output, Console.Out)) && !Console.IsOutputRedirected
		&& string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
		&& Environment.GetEnvironmentVariable("TERM") != "dumb";

	/// <summary>Counts the single-cell ASCII and Nerd Font Unicode scalars used in help labels.</summary>
	public static string Pad(string value, int width) =>
		value + new string(' ', Math.Max(0, width - value.EnumerateRunes().Count()));

	/// <summary>Maps the second-column glyph to the base text color shared across all tools.</summary>
	public static string GlyphColor(string icon) => icon switch
	{
		"\uea83" => "33", // folder
		"\uf059" => "32", // help
		"\ueb1e" or "\ueb24" => "95", // banner
		"\uea74" => "36", // information
		"\uea7b" => "35", // file
		"\ueac2" or "\ueac3" => "34", // transfer
		"\ueab2" or "\uf00c" => "92", // success
		"\ueabd" => "91", // unavailable
		"\uf071" => "93", // warning
		"\uea87" or "\uf188" => "31", // error/debug
		"\uf0e7" => "93", // force
		"\uf04b" => "38;5;114", // execution
		"\uf3ed" => "38;5;75", // rule/shield
		"\uf0ad" => "38;5;208", // repair
		"\uea92" => "38;5;117", // output
		_ => icon.EnumerateRunes().Any(rune => rune.Value is >= 0xF03A4 and <= 0xF03BE)
			? "38;5;180" : "36" // numeric convention selector / information
	};

	/// <summary>Wraps a complete text span in SGR styling and resets it afterwards.</summary>
	public static string Paint(string text, string color, bool useColor) =>
		useColor ? $"\u001b[{color}m{text}\u001b[0m" : text;

	/// <summary>Formats a space-padded row; syntax colors take precedence over its glyph's base color.</summary>
	public static string FormatRow(Row row, int labelWidth, bool useColor)
	{
		var color = GlyphColor(row.Icon);
		return Syntax(Pad(row.Label, labelWidth), color, useColor) + "  "
			+ Paint(Pad(row.Icon, 2), color, useColor) + "  "
			+ (row.Providers is not null ? CloudOptions(row.Providers, useColor, row.DefaultProvider) : Syntax(row.Description, color, useColor));
	}

	/// <summary>Formats rows with one shared label width; narrow terminals may put descriptions on the next line.</summary>
	public static IEnumerable<string> Rows(IEnumerable<Row> rows, bool useColor, int terminalWidth = 120)
	{
		var items = rows.ToArray();
		var labelWidth = items.Where(row => row.Icon.Length > 0)
			.Select(row => row.Label.EnumerateRunes().Count()).DefaultIfEmpty(0).Max();
		foreach (var row in items)
		{
			if (row.Icon.Length == 0) { yield return Syntax(row.Label, "36", useColor); continue; }
			var plain = FormatRow(row, labelWidth, false);
			if (terminalWidth >= 72 || plain.EnumerateRunes().Count() <= terminalWidth)
				yield return FormatRow(row, labelWidth, useColor);
			else
			{
				yield return Syntax(row.Label, GlyphColor(row.Icon), useColor);
				yield return "  " + FormatRow(row with { Label = "" }, 0, useColor).TrimStart();
			}
		}
	}

	/// <summary>Formats a cyan banner with its complete heading in bold.</summary>
	public static string[] Banner(string title, bool useColor)
	{
		var rule = "\ueb1e " + new string('─', Math.Max(40, title.EnumerateRunes().Count() - 2));
		return [Paint(rule, "36", useColor), Paint(title, "1;36", useColor), Paint(rule, "36", useColor)];
	}

	private static readonly string[] BadgeColors = ["96", "92", "94", "91", "35", "95", "36", "34"];

	/// <summary>Formats configured cloud choices in order, matching jpit's positional badge palette.</summary>
	public static string CloudOptions(IReadOnlyList<string> providers, bool useColor, string? defaultProvider = null)
	{
		if (providers.Count == 0) return Paint("no DefaultCloudOrder providers are configured", "33", useColor);
		return string.Join(Paint(", ", "33", useColor), providers.Select((name, i) =>
			Paint(CloudGlyph(name, i + 1), BadgeColors[i % BadgeColors.Length], useColor) + Paint(" " + name + (string.Equals(name, defaultProvider, StringComparison.OrdinalIgnoreCase) ? " (default)" : ""), "33", useColor)));
	}

	/// <summary>Returns a provider badge, or a numbered badge for an account-qualified name.</summary>
	public static string CloudGlyph(string provider, int number) => provider.ToLowerInvariant() switch
	{
		"onedrive" => "\U000F0C15",
		"dropbox" => "\U000F0BF4",
		"googledrive" => "\U000F0BFD",
		"iclouddrive" => "\U000F0C03",
		_ => number is >= 1 and <= 9 ? char.ConvertFromUtf32(new[]
			{ 0xF03A6, 0xF03A9, 0xF03AC, 0xF03AE, 0xF03B0, 0xF03B5, 0xF03B8, 0xF03BB, 0xF03BE }[number - 1]) : $"({number})"
	};

	private const string OptionColor = "32";
	private const string ValueColor = "96";
	private const string ChoiceColor = "35";
	private const string OptionalColor = "94";
	private const string CodeColor = "33";
	private static readonly Regex Options = new(@"(?<![\w/\-])--?[A-Za-z][A-Za-z0-9-]*", RegexOptions.CultureInvariant);
	private sealed record Group(int Start, int End, char Kind, bool Choice);

	public static string Syntax(string text, string baseColor, bool useColor)
	{
		if (!useColor || text.Length == 0) return text;
		var colors = Enumerable.Repeat(baseColor, text.Length).ToArray();
		var italic = new bool[text.Length];
		var groups = new List<Group>();
		var open = new Stack<(int Position, char Kind)>();
		int quote = -1;
		for (int i = 0; i < text.Length; i++)
		{
			var ch = text[i];
			if (ch == '\'')
			{
				if (quote < 0) quote = i;
				else
				{
					Array.Fill(colors, CodeColor, quote + 1, i - quote - 1);
					quote = -1;
				}
			}
			if (ch is '<' or '(' or '[') open.Push((i, ch));
			else if (ch is '>' or ')' or ']')
			{
				var expected = ch switch { '>' => '<', ')' => '(', _ => '[' };
				if (open.TryPeek(out var start) && start.Kind == expected)
				{
					open.Pop();
					groups.Add(new(start.Position, i, expected,
						expected == '(' && HasChoice(text, start.Position + 1, i)));
				}
			}
		}

		// Outer groups supply context; nested values keep their own style, except
		// values in a round one-of group share that group's choice color.
		foreach (var group in groups.OrderByDescending(group => group.End - group.Start))
		{
			if (group.Kind == '<')
			{
				var inChoice = groups.Any(parent => parent.Choice && parent.Start < group.Start && parent.End > group.End);
				Array.Fill(colors, inChoice ? ChoiceColor : ValueColor, group.Start + 1, group.End - group.Start - 1);
				Array.Fill(italic, true, group.Start + 1, group.End - group.Start - 1);
			}
			else if (group.Kind == '[' || group.Choice)
				Array.Fill(colors, group.Choice ? ChoiceColor : OptionalColor, group.Start, group.End - group.Start + 1);
		}
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] is '<' or '>' or '|')
			{
				colors[i] = baseColor;
				italic[i] = false;
			}
		}
		// A switch always has the same color, including inside groups and quoted code.
		foreach (Match option in Options.Matches(text))
			Array.Fill(colors, OptionColor, option.Index, option.Length);

		var result = new StringBuilder();
		for (int start = 0; start < text.Length;)
		{
			int end = start + 1;
			while (end < text.Length && colors[end] == colors[start] && italic[end] == italic[start]) end++;
			result.Append("\u001b[").Append(colors[start]);
			if (italic[start]) result.Append(";3");
			result.Append('m').Append(text, start, end - start).Append("\u001b[0m");
			start = end;
		}
		return result.ToString();
	}

	private static bool HasChoice(string text, int start, int end)
	{
		int depth = 0;
		for (int i = start; i < end; i++)
		{
			if (text[i] is '<' or '(' or '[') depth++;
			else if (text[i] is '>' or ')' or ']') depth--;
			else if (text[i] == '|' && depth == 0) return true;
		}
		return false;
	}
}
