using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	/// <summary>Amafu's Cloud object declares roots; keys are labels, not a provider enum.</summary>
	internal static class ConfiguredCloudPaths
	{
		internal static IReadOnlyList<string> Read(JObject config, Func<string, string> normalize, StringComparer comparer)
		{
			var cloud = config["Cloud"] as JObject;
			if (cloud == null) return Array.Empty<string>();
			return cloud.Properties()
				.Where(property => property.Value.Type == JTokenType.String)
				.Select(property => (string)property.Value)
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.Select(normalize)
				.SelectMany(root => new[] { WithSeparator(root), WithSeparator(ResolveLinks(root)) })
				.Distinct(comparer).ToArray();
		}

		internal static bool Contains(IReadOnlyList<string> roots, string normalizedPath, StringComparison comparison)
		{
			var candidate = WithSeparator(normalizedPath);
			if (roots.Any(root => candidate.StartsWith(root, comparison))) return true;
			// Shortcut paths can sit outside configured roots. Resolve existing ancestors
			// while retaining a not-yet-created suffix, so new files are classified too.
			candidate = WithSeparator(ResolveLinks(normalizedPath));
			return roots.Any(root => candidate.StartsWith(root, comparison));
		}

		private static string WithSeparator(string path) => Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

		private static string ResolveLinks(string path, int depth = 0)
		{
			if (depth >= 40) return path;
			try
			{
				var full = Path.GetFullPath(path);
				var current = Path.GetPathRoot(full);
				foreach (var part in full.Substring(current.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
				{
					current = Path.Combine(current, part);
					var info = new DirectoryInfo(current);
					if (info.LinkTarget != null)
						current = ResolveLinks(info.ResolveLinkTarget(true)?.FullName ?? current, depth + 1);
				}
				return current;
			}
			catch (IOException) { return path; }
			catch (UnauthorizedAccessException) { return path; }
		}
	}
}
