using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OsLib
{
	public static partial class Os
	{
		private static readonly OsRuntimeSnapshot runtime;
		static Os()
		{
			runtime = OsRuntimeSnapshot.Create();
		}
		public static dynamic Config => runtime.Config;
		public static bool IsConfigLoaded => runtime.IsConfigLoaded;
		public static string ConfigFileFullName => runtime.ConfigFileFullName;
		public static RaiPath TempDir => runtime.TempDir;
		internal static bool IsCloudPath(string path) => runtime.IsCloudPath(path);
		private sealed class OsRuntimeSnapshot
		{
			private readonly Lazy<RaiPath> userHomeDir;
			private readonly Lazy<RaiPath> appRootDir;
			private readonly Lazy<RaiPath> tempDir;
			private readonly Lazy<RaiPath> localBackupDir;
			private readonly IReadOnlyList<string> cloudRoots;
			private OsRuntimeSnapshot(
				JObject config,
				bool isConfigLoaded,
				string configFileFullName,
				string userHomeDirText,
				string appRootDirText,
				string tempDirText,
				string localBackupDirText,
				IReadOnlyList<string> cloudRoots)
			{
				Config = config;
				IsConfigLoaded = isConfigLoaded;
				ConfigFileFullName = configFileFullName;
				UserHomeDirText = userHomeDirText;
				AppRootDirText = appRootDirText;
				TempDirText = tempDirText;
				LocalBackupDirText = localBackupDirText;
				this.cloudRoots = cloudRoots;
				userHomeDir = new Lazy<RaiPath>(() => new RaiPath(UserHomeDirText));
				appRootDir = new Lazy<RaiPath>(() => new RaiPath(AppRootDirText));
				tempDir = new Lazy<RaiPath>(() => new RaiPath(TempDirText));
				localBackupDir = new Lazy<RaiPath>(() =>
					string.IsNullOrWhiteSpace(LocalBackupDirText) ? null : new RaiPath(LocalBackupDirText));
			}
			internal JObject Config { get; }
			internal bool IsConfigLoaded { get; }
			internal string ConfigFileFullName { get; }
			internal string UserHomeDirText { get; }
			internal string AppRootDirText { get; }
			internal string TempDirText { get; }
			internal string LocalBackupDirText { get; }
			internal RaiPath UserHomeDir => userHomeDir.Value;
			internal RaiPath AppRootDir => appRootDir.Value;
			internal RaiPath TempDir => tempDir.Value;
			internal RaiPath LocalBackupDir => localBackupDir.Value;
			internal bool IsCloudPath(string path)
			{
				if (!IsConfigLoaded || string.IsNullOrWhiteSpace(path) || cloudRoots.Count == 0)
					return false;
				try
				{
					var normalized = NormalizeConfiguredDirectory(path, UserHomeDirText, AppRootDirText);
					var comparison = Type == OsType.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
					return cloudRoots.Any(root => normalized.StartsWith(root, comparison));
				}
				catch
				{
					return false;
				}
			}
			internal static OsRuntimeSnapshot Create()
			{
				var userHomeDirText = ResolveUserHomeDirText();
				var appRootDirText = ensureTrailingDirSeparator(Directory.GetCurrentDirectory());
				var configFileFullName = ResolveBootstrapConfigPath(userHomeDirText, appRootDirText);
				var isConfigLoaded = false;
				JObject config;
				string tempDirText;
				try
				{
					config = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(configFileFullName))
						?? throw new InvalidOperationException("Config file did not contain a JSON object.");
					tempDirText = NormalizeConfiguredDirectory(
						config["TempDir"]?.ToString() ?? throw new ArgumentNullException("TempDir missing"),
						userHomeDirText,
						appRootDirText);
					isConfigLoaded = true;
				}
				catch
				{
					tempDirText = ensureTrailingDirSeparator(Path.GetTempPath());
					config = JObject.FromObject(new { TempDir = tempDirText });
				}
				var localBackupDirText = NormalizeOptionalConfiguredDirectory(
					config["LocalBackupDir"]?.ToString(),
					userHomeDirText,
					appRootDirText);
				var cloudRoots = GetCloudRoots(config, userHomeDirText, appRootDirText);
				return new OsRuntimeSnapshot(
					config,
					isConfigLoaded,
					configFileFullName,
					userHomeDirText,
					appRootDirText,
					tempDirText,
					localBackupDirText,
					cloudRoots);
			}
			private static IReadOnlyList<string> GetCloudRoots(JObject config, string userHomeDirText, string appRootDirText)
			{
				var cloud = config["Cloud"] as JObject;
				if (cloud == null) return Array.Empty<string>();
				return new[] { "Dropbox", "OneDrive", "GoogleDrive" }
					.Select(provider => NormalizeOptionalConfiguredDirectory(cloud[provider]?.ToString(), userHomeDirText, appRootDirText))
					.Where(root => !string.IsNullOrWhiteSpace(root))
					.Distinct(Type == OsType.Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
					.ToList();
			}
			private static string ResolveBootstrapConfigPath(string userHomeDirText, string appRootDirText)
			{
				return NormalizeConfiguredFileName(DefaultConfigFileLocation, userHomeDirText, appRootDirText);
			}
			private static string ResolveUserHomeDirText()
			{
				string resolved = Type == OsType.Windows
					? ensureTrailingDirSeparator(Environment.GetEnvironmentVariable("USERPROFILE"))
					: ensureTrailingDirSeparator(Environment.GetEnvironmentVariable("HOME"));
				if (string.IsNullOrWhiteSpace(resolved) && Type == OsType.Windows)
				{
					var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
					var homePath = ensureTrailingDirSeparator(Environment.GetEnvironmentVariable("HOMEPATH"));
					if (!string.IsNullOrEmpty(homeDrive) && !string.IsNullOrEmpty(homePath))
						resolved = NormSeperator(homeDrive + homePath);
				}
				if (string.IsNullOrEmpty(resolved))
					resolved = ensureTrailingDirSeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty);
				if (string.IsNullOrWhiteSpace(resolved))
					LogWarningOnce<OsDiagnosticsLogScope>("fallback:userhome:empty", "User home directory could not be resolved.");
				return resolved;
			}
			private static string NormalizeOptionalConfiguredDirectory(string value, string userHomeDirText, string appRootDirText)
			{
				return string.IsNullOrWhiteSpace(value)
					? null
					: NormalizeConfiguredDirectory(value, userHomeDirText, appRootDirText);
			}
			private static string NormalizeConfiguredDirectory(string value, string userHomeDirText, string appRootDirText)
			{
				return ensureTrailingDirSeparator(NormalizeConfiguredPath(value, userHomeDirText, appRootDirText));
			}
			private static string NormalizeConfiguredFileName(string value, string userHomeDirText, string appRootDirText)
			{
				return NormalizeConfiguredPath(value, userHomeDirText, appRootDirText);
			}
			private static string NormalizeConfiguredPath(string value, string userHomeDirText, string appRootDirText)
			{
				if (string.IsNullOrWhiteSpace(value)) return value;
				var normalized = NormSeperator(value.Trim());
				if (normalized == "~") return userHomeDirText;
				var homePrefix = "~" + DIR;
				if (normalized.StartsWith(homePrefix, StringComparison.Ordinal))
					return userHomeDirText + normalized.Substring(homePrefix.Length);
				if (normalized == ".") return appRootDirText;
				var appRootPrefix = "." + DIR;
				if (normalized.StartsWith(appRootPrefix, StringComparison.Ordinal))
					return appRootDirText + normalized.Substring(appRootPrefix.Length);
				return NormSeperator(Path.GetFullPath(normalized));
			}
		}
	}
}
