using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	public static partial class Os
	{
		private static dynamic config;
		public static dynamic Config
		{
			get
			{
				string homeDir = string.Empty;
				string tempDir = string.Empty;
				string localBackupDir = string.Empty;
				List<string> defaultCloudOrder = new List<string>();
				string oneDrive = string.Empty;
				string dropbox = string.Empty;
				string googleDrive = string.Empty;
				if (config == null)
				{	// read it from disk
					try
					{
						var json = new TextFile(defaultConfigFileLocation).ReadAllText();
						homeDir = (string)config.homeDir;
						tempDir = (string)config.tempDir;
						localBackupDir = (string)config.localBackupDir;
						oneDrive = (string)config.cloud.onedrive;
						dropbox = (string)config.cloud.dropbox;
						googleDrive = (string)config.cloud.googledrive;
						defaultCloudOrder = ((JArray)config.defaultCloudOrder).ToObject<List<string>>() ?? new List<string>();
						var provider = defaultCloudOrder.FirstOrDefault();
					}
					catch (RuntimeBinderException ex)
					{
						// Missing property or unexpected shape in dynamic object
						Console.WriteLine("Config binding error: " + ex.Message);
					}
					catch (Exception ex)
					{
						// Parse/type conversion/etc.
						Console.WriteLine("Config read error: " + ex.Message);
					}
				}
				return config;
			}
		}
		private static string configPathOverride;
		[Obsolete("if we remove DI for testing")]
		private static Dictionary<CloudStorageType, string> cloudRootsCache;
		private static bool isDiscoveringCloudRoots;
		private static bool isInitializingConfig;

		private const string cloudDiscoveryGuidePath = "OsLib/CLOUD_STORAGE_DISCOVERY.md";

	
		public static string GetCloudDiscoveryReport(bool refresh = false)
		{
			if (refresh)
				;	// read config again
			var sb = new StringBuilder();
			sb.AppendLine("Discovered cloud storage roots:");
			try 
			{
				foreach (var provider in Config.DefaultCloudOrder)
				{
					switch (provider)
					{
						case "GoogleDrive":
							sb.AppendLine($"- {provider}: {Config.Cloud.GoogleDrive}");
							break;
						case "Dropbox":
							sb.AppendLine($"- {provider}: {Config.Cloud.Dropbox}");
							break;
						case "OneDrive":
							sb.AppendLine($"- {provider}: {Config.Cloud.OneDrive}");
							break;
						default:
							sb.AppendLine($"- {provider}: <not found>");
							break;
					}
				}
			}
			catch (Exception ex)
			{
				LogError<OsDiagnosticsLogScope>(ex, "Error generating cloud discovery report");
			}
			return sb.ToString().TrimEnd();
		}
		public static string GetCloudConfigurationDiagnosticReport(bool refresh = false)
		{
			if (refresh)
				;	// read config again
			var sb = new StringBuilder();
			sb.AppendLine("Cloud configuration diagnostics:");
			sb.AppendLine($"- active config path: {defaultConfigFileLocation}");
			sb.AppendLine($"- userHomeDir: {UserHomeDir?.Path}");
			sb.AppendLine($"- appRootDir: {AppRootDir?.Path}");
			sb.AppendLine($"- tempDir: {TempDir?.Path}");
			sb.AppendLine($"- localBackupDir: {LocalBackupDir?.Path}");
			sb.AppendLine($"- configured default cloud order: {Config.Cloud}");

			foreach (var provider in Config.DefaultCloudOrder)
			{
				sb.Append($"- {provider}: ");
				switch (provider) {
					case "GoogleDrive": sb.AppendLine($" path: {Config.Cloud.GoogleDrive}"); break;
					case "Dropbox": sb.AppendLine($" path: {Config.Cloud.Dropbox}"); break;
					case "OneDrive": sb.AppendLine($" path: {Config.Cloud.OneDrive}"); break;
				}	
			}
			return sb.ToString().TrimEnd() + GetCloudDiscoveryReport(refresh);
		}

		public static string GetCloudStorageSetupGuidance()
		{
			return "Configure Os.Config in " + GetDefaultConfigPath() + ". See " + cloudDiscoveryGuidePath;
		}
		public static string GetDefaultConfigPath()
		{
			return defaultConfigFileLocation;   // no DI here!!!!!
		}
		private static void InvalidateConfiguredPathCaches()
		{
			userHomeDir = null;
			appRootDir = null;
			tempDir = null;
			localBackupDir = null;
		}
		private static string NormalizeConfigPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return string.Empty;

			var expanded = ExpandUserHomePath(path);

			return Path.GetFullPath(expanded);
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

			return ExpandUserHomePath(candidate);
		}

		private static string ExpandUserHomePath(string path)
		{
			var expanded = path.Trim();
			if (!expanded.StartsWith("~/", StringComparison.Ordinal))
				return expanded;

			var home = ResolveSystemHomeDir();
			if (string.IsNullOrWhiteSpace(home))
				return expanded;

			return home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + DIRSEPERATOR + expanded.Substring(2);
		}

		private static string NormalizePathForComparison(string value)
		{
			var expanded = ExpandPath(value);
			if (string.IsNullOrWhiteSpace(expanded))
				return null;

			return NormSeperator(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}

		private static bool PathIsUnderCloudRoot(string normalizedCandidate, string root)
		{
			var normalizedRoot = NormalizePathForComparison(root);
			if (string.IsNullOrWhiteSpace(normalizedRoot))
				return false;

			return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
				normalizedCandidate.StartsWith(normalizedRoot + DIRSEPERATOR, StringComparison.OrdinalIgnoreCase);
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
			catch (Exception ex)
			{
				LogError<OsDiagnosticsLogScope>(ex, "Failed to enumerate directories under {DirectoryPath} with pattern {DirectoryPattern}", path ?? string.Empty, pattern ?? string.Empty);
			}

			return Array.Empty<string>();
		}

		private sealed class DisposableScope : IDisposable
		{
			private readonly Action onDispose;
			private bool disposed;

			internal DisposableScope(Action onDispose)
			{
				this.onDispose = onDispose;
			}

			public void Dispose()
			{
				if (disposed)
					return;

				disposed = true;
				onDispose();
			}
		}
	}
}
