using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	public partial class Os
	{
		private static readonly CloudStorageType[] DefaultCloudOrder =
		{
			CloudStorageType.GoogleDrive,
			CloudStorageType.ICloud,
			CloudStorageType.Dropbox,
			CloudStorageType.OneDrive
		};

		private static readonly Dictionary<CloudStorageType, string> EnvVarByProvider = new()
		{
			{ CloudStorageType.Dropbox, "OSLIB_CLOUD_ROOT_DROPBOX" },
			{ CloudStorageType.OneDrive, "OSLIB_CLOUD_ROOT_ONEDRIVE" },
			{ CloudStorageType.GoogleDrive, "OSLIB_CLOUD_ROOT_GOOGLEDRIVE" },
			{ CloudStorageType.ICloud, "OSLIB_CLOUD_ROOT_ICLOUD" }
		};

		private static Dictionary<CloudStorageType, string> cloudRootsCache;
		private static bool isDiscoveringCloudRoots;

		/// <summary>
		/// Preferred cloud storage root based on configured precedence.
		/// Throws when no provider root could be found.
		/// </summary>
		public static string CloudStorageRoot => GetPreferredCloudStorageRoot();

		/// <summary>
		/// Returns all discovered cloud storage roots keyed by provider.
		/// Discovery uses environment overrides, config files, and OS-specific probes.
		/// </summary>
		public static IReadOnlyDictionary<CloudStorageType, string> GetCloudStorageRoots(bool refresh = false)
		{
			if (refresh || cloudRootsCache == null)
			{
				if (isDiscoveringCloudRoots)
					return new Dictionary<CloudStorageType, string>(cloudRootsCache ?? new Dictionary<CloudStorageType, string>());

				try
				{
					isDiscoveringCloudRoots = true;
					cloudRootsCache = DiscoverCloudStorageRoots();
				}
				finally
				{
					isDiscoveringCloudRoots = false;
				}
			}
			return new Dictionary<CloudStorageType, string>(cloudRootsCache);
		}

		internal static bool IsCloudPath(string candidatePath)
		{
			var normalizedCandidate = NormalizePathForComparison(candidatePath);
			if (string.IsNullOrWhiteSpace(normalizedCandidate) || IsDropboxMetadataPath(normalizedCandidate))
				return false;

			if (isDiscoveringCloudRoots)
				return false;

			foreach (var root in GetCloudStorageRoots().Values)
			{
				var normalizedRoot = NormalizePathForComparison(root);
				if (string.IsNullOrWhiteSpace(normalizedRoot))
					continue;

				if (normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
					normalizedCandidate.StartsWith(normalizedRoot + DIRSEPERATOR, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns the first available root according to the preferred order.
		/// When no order is given, uses: GoogleDrive, ICloud, Dropbox, OneDrive.
		/// </summary>
		public static string GetPreferredCloudStorageRoot(params CloudStorageType[] preferredOrder)
		{
			var roots = GetCloudStorageRoots();
			var order = (preferredOrder != null && preferredOrder.Length > 0) ? preferredOrder : DefaultCloudOrder;
			foreach (var provider in order)
			{
				if (roots.TryGetValue(provider, out var root))
					return root;
			}

			throw new DirectoryNotFoundException(
				"No cloud storage root could be discovered. Configure one of: " +
				"OSLIB_CLOUD_ROOT_DROPBOX, OSLIB_CLOUD_ROOT_ONEDRIVE, OSLIB_CLOUD_ROOT_GOOGLEDRIVE, OSLIB_CLOUD_ROOT_ICLOUD " +
				"or provide an INI file via OSLIB_CLOUD_CONFIG.");
		}

		/// <summary>
		/// Returns the discovered root for a specific provider, or null if not found.
		/// </summary>
		public static string GetCloudStorageRoot(CloudStorageType provider, bool refresh = false)
		{
			var roots = GetCloudStorageRoots(refresh);
			return roots.TryGetValue(provider, out var root) ? root : null;
		}

		/// <summary>
		/// Clears cloud discovery cache so subsequent calls recompute values.
		/// </summary>
		public static void ResetCloudStorageCache()
		{
			cloudRootsCache = null;
			isDiscoveringCloudRoots = false;
		}

		/// <summary>
		/// Returns a readable report of all discovered cloud providers and roots.
		/// </summary>
		public static string GetCloudDiscoveryReport(bool refresh = false)
		{
			var roots = GetCloudStorageRoots(refresh);
			var sb = new StringBuilder();
			sb.AppendLine("Discovered cloud storage roots:");
			foreach (var provider in Enum.GetValues<CloudStorageType>())
			{
				if (roots.TryGetValue(provider, out var root))
					sb.AppendLine($"- {provider}: {root}");
				else
					sb.AppendLine($"- {provider}: <not found>");
			}
			return sb.ToString().TrimEnd();
		}

		private static Dictionary<CloudStorageType, string> DiscoverCloudStorageRoots()
		{
			var roots = new Dictionary<CloudStorageType, string>();

			ApplyEnvironmentOverrides(roots);
			ApplyIniConfiguration(roots);
			ApplyProviderProbes(roots);

			return roots;
		}

		private static void ApplyEnvironmentOverrides(Dictionary<CloudStorageType, string> roots)
		{
			foreach (var kvp in EnvVarByProvider)
			{
				var value = Environment.GetEnvironmentVariable(kvp.Value);
				TryAddRoot(roots, kvp.Key, value);
			}
		}

		private static void ApplyIniConfiguration(Dictionary<CloudStorageType, string> roots)
		{
			foreach (var configPath in GetCloudConfigCandidates())
			{
				if (!File.Exists(configPath))
					continue;

				foreach (var line in File.ReadAllLines(configPath))
				{
					var raw = line.Trim();
					if (raw.Length == 0 || raw.StartsWith("#") || raw.StartsWith(";") || !raw.Contains('='))
						continue;

					var idx = raw.IndexOf('=');
					if (idx <= 0 || idx >= raw.Length - 1)
						continue;

					var key = raw.Substring(0, idx).Trim().ToLowerInvariant();
					var value = raw.Substring(idx + 1).Trim();

					switch (key)
					{
						case "dropbox":
							TryAddRoot(roots, CloudStorageType.Dropbox, value);
							break;
						case "onedrive":
							TryAddRoot(roots, CloudStorageType.OneDrive, value);
							break;
						case "googledrive":
						case "google_drive":
							TryAddRoot(roots, CloudStorageType.GoogleDrive, value);
							break;
						case "icloud":
						case "icloud_drive":
							TryAddRoot(roots, CloudStorageType.ICloud, value);
							break;
					}
				}
			}
		}

		private static IEnumerable<string> GetCloudConfigCandidates()
		{
			var configured = Environment.GetEnvironmentVariable("OSLIB_CLOUD_CONFIG");
			if (!string.IsNullOrWhiteSpace(configured))
			{
				yield return ExpandPath(configured);
				yield break;
			}

			if (Type == OsType.Windows)
			{
				var appData = Environment.GetEnvironmentVariable("APPDATA");
				if (!string.IsNullOrWhiteSpace(appData))
					yield return Path.Combine(appData, "OsLib", "cloudstorage.ini");
			}
			else
			{
				yield return new RaiFile("~/.config/oslib/cloudstorage.ini").FullName;
				yield return new RaiFile("~/.oslib/cloudstorage.ini").FullName;
			}
		}

		private static void ApplyProviderProbes(Dictionary<CloudStorageType, string> roots)
		{
			ProbeDropbox(roots);
			ProbeOneDrive(roots);
			ProbeGoogleDrive(roots);
			ProbeICloud(roots);
		}

		private static void ProbeDropbox(Dictionary<CloudStorageType, string> roots)
		{
			foreach (var infoPath in GetDropboxInfoFileCandidates())
			{
				if (!File.Exists(infoPath))
					continue;

				try
				{
					var json = JObject.Parse(File.ReadAllText(infoPath));
					var personalPath = (string)json["personal"]?["path"];
					var businessRoot = (string)json["business"]?["root_path"];
					TryAddRoot(roots, CloudStorageType.Dropbox, personalPath);
					TryAddRoot(roots, CloudStorageType.Dropbox, businessRoot);
				}
				catch
				{
				}
			}

			TryAddRoot(roots, CloudStorageType.Dropbox, "~/Dropbox");
			TryAddRoot(roots, CloudStorageType.Dropbox, "~/Library/CloudStorage/Dropbox");
		}

		private static IEnumerable<string> GetDropboxInfoFileCandidates()
		{
			if (Type == OsType.Windows)
			{
				var appData = Environment.GetEnvironmentVariable("APPDATA");
				var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
				if (!string.IsNullOrWhiteSpace(appData))
					yield return Path.Combine(appData, "Dropbox", "info.json");
				if (!string.IsNullOrWhiteSpace(localAppData))
					yield return Path.Combine(localAppData, "Dropbox", "info.json");
			}
			else
			{
				yield return new RaiFile("~/.dropbox/info.json").FullName;
			}
		}

		private static void ProbeOneDrive(Dictionary<CloudStorageType, string> roots)
		{
			TryAddRoot(roots, CloudStorageType.OneDrive, Environment.GetEnvironmentVariable("OneDrive"));
			TryAddRoot(roots, CloudStorageType.OneDrive, Environment.GetEnvironmentVariable("OneDriveCommercial"));
			TryAddRoot(roots, CloudStorageType.OneDrive, Environment.GetEnvironmentVariable("OneDriveConsumer"));

			TryAddRoot(roots, CloudStorageType.OneDrive, "~/OneDrive");
			TryAddRoot(roots, CloudStorageType.OneDrive, "~/OneDrive - Personal");
			TryAddRoot(roots, CloudStorageType.OneDrive, "~/Library/CloudStorage/OneDrive");

			foreach (var path in SafeEnumerateDirectories(HomeDir, "OneDrive*"))
				TryAddRoot(roots, CloudStorageType.OneDrive, path);

			foreach (var path in SafeEnumerateDirectories(new RaiFile("~/Library/CloudStorage/").Path, "OneDrive*"))
				TryAddRoot(roots, CloudStorageType.OneDrive, path);
		}

		private static void ProbeGoogleDrive(Dictionary<CloudStorageType, string> roots)
		{
			TryAddRoot(roots, CloudStorageType.GoogleDrive, "~/Google Drive");
			TryAddRoot(roots, CloudStorageType.GoogleDrive, "~/GoogleDrive");

			foreach (var path in SafeEnumerateDirectories(new RaiFile("~/Library/CloudStorage/").Path, "GoogleDrive*"))
				TryAddRoot(roots, CloudStorageType.GoogleDrive, path);

			if (Type == OsType.Windows)
			{
				var user = Environment.GetEnvironmentVariable("USERPROFILE");
				if (!string.IsNullOrWhiteSpace(user))
				{
					TryAddRoot(roots, CloudStorageType.GoogleDrive, Path.Combine(user, "Google Drive"));
					TryAddRoot(roots, CloudStorageType.GoogleDrive, Path.Combine(user, "My Drive"));
				}
			}
		}

		private static void ProbeICloud(Dictionary<CloudStorageType, string> roots)
		{
			TryAddRoot(roots, CloudStorageType.ICloud, "~/Library/Mobile Documents/com~apple~CloudDocs");
			TryAddRoot(roots, CloudStorageType.ICloud, "~/iCloudDrive");
			TryAddRoot(roots, CloudStorageType.ICloud, "~/Cloud/iCloud");
			TryAddRoot(roots, CloudStorageType.ICloud, "~/rclone/iCloud");

			if (Type == OsType.Windows)
			{
				var user = Environment.GetEnvironmentVariable("USERPROFILE");
				if (!string.IsNullOrWhiteSpace(user))
					TryAddRoot(roots, CloudStorageType.ICloud, Path.Combine(user, "iCloudDrive"));
			}
		}

		private static bool TryAddRoot(Dictionary<CloudStorageType, string> roots, CloudStorageType provider, string candidate)
		{
			if (roots.ContainsKey(provider))
				return false;

			var expanded = ExpandPath(candidate);
			if (string.IsNullOrWhiteSpace(expanded) || !Directory.Exists(expanded))
				return false;

			roots[provider] = new RaiPath(expanded).Path;
			return true;
		}

		private static string ExpandPath(string candidate)
		{
			if (string.IsNullOrWhiteSpace(candidate))
				return null;

			var expanded = Environment.ExpandEnvironmentVariables(candidate.Trim());
			if (expanded.StartsWith("~/", StringComparison.Ordinal))
				expanded = HomeDir + expanded.Substring(1);
			return expanded;
		}

		private static string NormalizePathForComparison(string value)
		{
			var expanded = ExpandPath(value);
			if (string.IsNullOrWhiteSpace(expanded))
				return null;

			var normalized = NormSeperator(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return normalized;
		}

		private static bool IsDropboxMetadataPath(string normalizedPath)
		{
			var marker = DIRSEPERATOR + ".dropbox";
			return normalizedPath.EndsWith(marker, StringComparison.OrdinalIgnoreCase) ||
				normalizedPath.Contains(marker + DIRSEPERATOR, StringComparison.OrdinalIgnoreCase);
		}

		private static IEnumerable<string> SafeEnumerateDirectories(string path, string pattern)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
					return Directory.EnumerateDirectories(path, pattern);
			}
			catch
			{
			}
			return Array.Empty<string>();
		}
	}
}
