using System;
using System.Collections.Generic;
using System.Linq;

namespace OsLib;

/// <summary>Describes a reserved CLI verb that appeared after another argument.</summary>
public sealed record CliVerbDiagnostic(
	string Verb,
	string Message,
	string CorrectedCommandLine);

/// <summary>Shared fail-fast validation for command-first RAIkeep CLI grammars.</summary>
public static class CliVerbDispatch
{
	/// <summary>Returns an actionable diagnostic when a reserved verb is not the first argument.</summary>
	public static CliVerbDiagnostic DetectMisplacedVerb(
		string toolName,
		IReadOnlyList<string> arguments,
		IEnumerable<string> reservedVerbs)
	{
		if (string.IsNullOrWhiteSpace(toolName))
			throw new ArgumentException("A CLI tool name is required.", nameof(toolName));
		ArgumentNullException.ThrowIfNull(arguments);
		ArgumentNullException.ThrowIfNull(reservedVerbs);

		var verbs = reservedVerbs
			.Where(verb => !string.IsNullOrWhiteSpace(verb))
			.ToHashSet(StringComparer.Ordinal);
		if (arguments.Count == 0 || verbs.Contains(arguments[0]))
			return null;

		for (var index = 1; index < arguments.Count; index++)
		{
			var candidate = arguments[index];
			if (!verbs.Contains(candidate))
				continue;

			var corrected = new List<string> { toolName, candidate };
			for (var sourceIndex = 0; sourceIndex < arguments.Count; sourceIndex++)
			{
				if (sourceIndex != index)
					corrected.Add(Quote(arguments[sourceIndex]));
			}

			var correctedCommandLine = string.Join(' ', corrected);
			return new CliVerbDiagnostic(
				candidate,
				$"Error: Subcommand '{candidate}' must be the first parameter.\n" +
				$"Try:   {correctedCommandLine}",
				correctedCommandLine);
		}

		return null;
	}

	private static string Quote(string value)
	{
		if (value.Length > 0 && value.All(character =>
			char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '/' or ':' or '\\'))
			return value;

		return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
	}
}
