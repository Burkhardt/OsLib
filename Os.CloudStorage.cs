using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	public sealed class CloudModel
	{
		[JsonProperty("googledrive")]
		private string GoogleDriveRootSerialized
		{
			get => GoogleDriveRoot?.Path ?? string.Empty;
			set => GoogleDriveRoot = ToRaiPath(value);
		}

		[JsonProperty("icloud")]
		private string ICloudRootSerialized
		{
			get => ICloudRoot?.Path ?? string.Empty;
			set => ICloudRoot = ToRaiPath(value);
		}

		[JsonProperty("dropbox")]
		private string DropboxRootSerialized
		{
			get => DropboxRoot?.Path ?? string.Empty;
			set => DropboxRoot = ToRaiPath(value);
		}

		[JsonProperty("onedrive")]
		private string OneDriveRootSerialized
		{
			get => OneDriveRoot?.Path ?? string.Empty;
			set => OneDriveRoot = ToRaiPath(value);
		}

		[JsonIgnore]
		public RaiPath GoogleDriveRoot { get; internal set; }

		[JsonIgnore]
		public RaiPath ICloudRoot { get; internal set; }

		[JsonIgnore]
		public RaiPath DropboxRoot { get; internal set; }

		[JsonIgnore]
		public RaiPath OneDriveRoot { get; internal set; }

		[JsonIgnore]
		public IReadOnlyDictionary<CloudStorageType, RaiPath> Roots
		{
			get
			{
				var roots = new Dictionary<CloudStorageType, RaiPath>();
				TryAdd(roots, CloudStorageType.GoogleDrive, GoogleDriveRoot);
				TryAdd(roots, CloudStorageType.ICloud, ICloudRoot);
				TryAdd(roots, CloudStorageType.Dropbox, DropboxRoot);
				TryAdd(roots, CloudStorageType.OneDrive, OneDriveRoot);
				return roots;
			}
		}

		public RaiPath GetRoot(CloudStorageType provider)
		{
			return provider switch
			{
				CloudStorageType.GoogleDrive => GoogleDriveRoot,
				CloudStorageType.ICloud => ICloudRoot,
				CloudStorageType.Dropbox => DropboxRoot,
				CloudStorageType.OneDrive => OneDriveRoot,
				_ => null
			};
		}

		internal void SetRoot(CloudStorageType provider, string value)
		{
			var normalized = ToRaiPath(value);
			switch (provider)
			{
				case CloudStorageType.GoogleDrive:
					GoogleDriveRoot = normalized;
					break;
				case CloudStorageType.ICloud:
					ICloudRoot = normalized;
					break;
				case CloudStorageType.Dropbox:
					DropboxRoot = normalized;
					break;
				case CloudStorageType.OneDrive:
					OneDriveRoot = normalized;
					break;
			}
		}

		internal bool MergeMissingDiscoveredRoots(IReadOnlyDictionary<CloudStorageType, string> discoveredRoots)
		{
			var changed = false;
			foreach (var provider in Enum.GetValues<CloudStorageType>())
			{
				if (GetRoot(provider) != null)
					continue;

				if (!discoveredRoots.TryGetValue(provider, out var root) || string.IsNullOrWhiteSpace(root))
					continue;

				SetRoot(provider, root);
				changed = true;
			}

			return changed;
		}

		internal CloudModel Clone()
		{
			return new CloudModel
			{
				GoogleDriveRoot = ToRaiPath(GoogleDriveRoot?.Path),
				ICloudRoot = ToRaiPath(ICloudRoot?.Path),
				DropboxRoot = ToRaiPath(DropboxRoot?.Path),
				OneDriveRoot = ToRaiPath(OneDriveRoot?.Path),
			};
		}

		internal void Normalize()
		{
			foreach (var provider in Enum.GetValues<CloudStorageType>())
				SetRoot(provider, GetRoot(provider)?.Path);
		}

		private static RaiPath ToRaiPath(string value)
		{
			return string.IsNullOrWhiteSpace(value) ? null : new RaiPath(value);
		}

		private static void TryAdd(Dictionary<CloudStorageType, RaiPath> roots, CloudStorageType provider, RaiPath value)
		{
			if (value != null)
				roots[provider] = value;
		}
	}

	public sealed class OsConfigModel
	{
		[JsonProperty("homeDir")]
		private string HomeDirSerialized
		{
			get => HomeDir?.Path ?? string.Empty;
			set => HomeDir = ToRaiPath(value);
		}

		[JsonProperty("tempDir")]
		private string TempDirSerialized
		{
			get => TempDir?.Path ?? string.Empty;
			set => TempDir = ToRaiPath(value);
		}

		[JsonProperty("localBackupDir")]
		private string LocalBackupDirSerialized
		{
			get => LocalBackupDir?.Path ?? string.Empty;
			set => LocalBackupDir = ToRaiPath(value);
		}

		[JsonIgnore]
		public RaiPath HomeDir { get; internal set; }

		[JsonIgnore]
		public RaiPath TempDir { get; internal set; }

		[JsonIgnore]
		public RaiPath LocalBackupDir { get; internal set; }

		[JsonProperty("defaultCloudOrder")]
		public List<CloudStorageType> DefaultCloudOrder { get; internal set; } = Os.CreateDefaultCloudOrder().ToList();

		[JsonProperty("cloud")]
		public CloudModel Cloud { get; internal set; } = new CloudModel();
		
		[JsonIgnore]
		public RaiPath GooglePath => Cloud?.GoogleDriveRoot;

		[JsonIgnore]
		public RaiPath ICloudPath => Cloud?.ICloudRoot;

		[JsonIgnore]
		public RaiPath DropboxPath => Cloud?.DropboxRoot;

		[JsonIgnore]
		public RaiPath OneDrivePath => Cloud?.OneDriveRoot;


		[JsonIgnore]
		public IReadOnlyDictionary<CloudStorageType, RaiPath> CloudDirPaths => Cloud?.Roots ?? new Dictionary<CloudStorageType, RaiPath>();

		public RaiPath GetCloudDirPath(CloudStorageType provider)
		{
			return Cloud?.GetRoot(provider);
		}

		internal OsConfigModel Clone()
		{
			return new OsConfigModel
			{
				HomeDir = ToRaiPath(HomeDir?.Path),
				TempDir = ToRaiPath(TempDir?.Path),
				LocalBackupDir = ToRaiPath(LocalBackupDir?.Path),
				DefaultCloudOrder = DefaultCloudOrder?.ToList() ?? Os.CreateDefaultCloudOrder().ToList(),
				Cloud = Cloud?.Clone() ?? new CloudModel()
			};
		}

		internal void Normalize()
		{
			HomeDir = ToRaiPath(HomeDir?.Path);
			TempDir = ToRaiPath(TempDir?.Path);
			LocalBackupDir = ToRaiPath(LocalBackupDir?.Path);
			DefaultCloudOrder = (DefaultCloudOrder == null || DefaultCloudOrder.Count == 0)
				? Os.CreateDefaultCloudOrder().ToList()
				: DefaultCloudOrder.Distinct().ToList();
			Cloud ??= new CloudModel();
			Cloud.Normalize();
		}

		private static RaiPath ToRaiPath(string value)
		{
			return string.IsNullOrWhiteSpace(value) ? null : new RaiPath(value);
		}
	}

	public sealed class OsConfigFile : ConfigFile<OsConfigModel>
	{
		public OsConfigFile(string fullName) : base(fullName, autoLoad: true)
		{
		}

		protected override OsConfigModel CreateDefaultData()
		{
			return new OsConfigModel
			{
				HomeDir = new RaiPath(Os.ResolveSystemHomeDir()),
				TempDir = new RaiPath(Os.ResolveSystemTempDir()),
				LocalBackupDir = new RaiPath(Os.ResolveSystemLocalBackupDir()),
				DefaultCloudOrder = Os.CreateDefaultCloudOrder().ToList(),
				Cloud = new CloudModel()
			};
		}

		protected override OsConfigModel NormalizeData(OsConfigModel data)
		{
			data ??= CreateDefaultData();
			data.Normalize();
			return data;
		}
		[Obsolete("Use Save() instead.")]
		internal void Persist()
		{
			Save();
		}
	}

	public partial class Os
	{
		private static OsConfigFile config;
		private static Dictionary<CloudStorageType, string> cloudRootsCache;
		private static bool isDiscoveringCloudRoots;
		private static bool isInitializingConfig;

		private const string CloudDiscoveryGuidePath = "OsLib/CLOUD_STORAGE_DISCOVERY.md";
		private const string DefaultConfigFileName = "osconfig.json";

		public static OsConfigFile Config
		{
			get
			{
				try
				{
					isInitializingConfig = true;
					var configPath = GetDefaultConfigPath();
					if (config == null)
						config = new OsConfigFile(configPath);
					else if (config.SetFullName(configPath))
					{
						config.Load();
						InvalidateConfiguredPathCaches();
						ResetCloudStorageCache();
					}
				}
				finally
				{
					isInitializingConfig = false;
				}

				return config;
			}
		}

		public static string CloudStorageRoot => GetPreferredCloudStorageRoot();

		public static OsConfigModel LoadConfig(bool refresh = false)
		{
			if (refresh)
			{
				Config.Load();
				InvalidateConfiguredPathCaches();
				ResetCloudStorageCache();
			}

			return Config.Data;
		}

		public static IReadOnlyDictionary<CloudStorageType, string> GetCloudStorageRoots(bool refresh = false)
		{
			if (refresh || cloudRootsCache == null)
			{
				if (isDiscoveringCloudRoots)
					return new Dictionary<CloudStorageType, string>(cloudRootsCache ?? new Dictionary<CloudStorageType, string>());

				try
				{
					isDiscoveringCloudRoots = true;
					if (refresh)
						LoadConfig(refresh: true);
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
			return GetCloudStorageProviderForPath(candidatePath) != null;
		}

		internal static CloudStorageType? GetCloudStorageProviderForPath(string candidatePath)
		{
			if (isInitializingConfig)
				return null;

			var normalizedCandidate = NormalizePathForComparison(candidatePath);
			if (string.IsNullOrWhiteSpace(normalizedCandidate) || IsDropboxMetadataPath(normalizedCandidate))
				return null;

			if (isDiscoveringCloudRoots)
				return null;

			foreach (var kvp in GetCloudStorageRoots())
			{
				if (!PathIsUnderCloudRoot(normalizedCandidate, kvp.Value))
					continue;

				return kvp.Key;
			}

			return null;
		}

		public static string GetPreferredCloudStorageRoot(params CloudStorageType[] preferredOrder)
		{
			var roots = GetCloudStorageRoots();
			var configuredOrder = LoadConfig().DefaultCloudOrder ?? CreateDefaultCloudOrder().ToList();
			var order = (preferredOrder != null && preferredOrder.Length > 0)
				? preferredOrder
				: configuredOrder.ToArray();

			foreach (var provider in order)
			{
				if (roots.TryGetValue(provider, out var root))
					return root;
			}

			throw new DirectoryNotFoundException("No cloud storage root could be discovered. " + GetCloudStorageSetupGuidance());
		}

		public static string GetCloudStorageRoot(CloudStorageType provider, bool refresh = false)
		{
			var roots = GetCloudStorageRoots(refresh);
			return roots.TryGetValue(provider, out var root) ? root : null;
		}

		public static void ResetCloudStorageCache()
		{
			cloudRootsCache = null;
			isDiscoveringCloudRoots = false;
		}

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

		public static string GetCloudConfigurationDiagnosticReport(bool refresh = false)
		{
			var effectiveRoots = GetCloudStorageRoots(refresh);
			var config = LoadConfig();
			var sb = new StringBuilder();
			sb.AppendLine("Cloud configuration diagnostics:");
			sb.AppendLine($"- active config path: {GetDefaultConfigPath()}");
			sb.AppendLine($"- homeDir: {HomeDir}");
			sb.AppendLine($"- tempDir: {TempDir}");
			sb.AppendLine($"- localBackupDir: {LocalBackupDir}");
			sb.AppendLine($"- configured default cloud order: {string.Join(", ", config.DefaultCloudOrder ?? CreateDefaultCloudOrder().ToList())}");

			foreach (var provider in Enum.GetValues<CloudStorageType>())
			{
				var configured = config.GetCloudDirPath(provider)?.Path ?? string.Empty;
				var effective = effectiveRoots.TryGetValue(provider, out var root) ? root : string.Empty;
				sb.AppendLine($"- {provider}: configured={(string.IsNullOrWhiteSpace(configured) ? "<empty>" : configured)}; effective={(string.IsNullOrWhiteSpace(effective) ? "<missing>" : effective)}");
			}

			return sb.ToString().TrimEnd();
		}

		public static string GetCloudStorageSetupGuidance()
		{
			return "Configure Os.Config in " + GetDefaultConfigPath() + ". See " + CloudDiscoveryGuidePath;
		}

		public static string GetDefaultConfigPath()
		{
			if (Type == OsType.Windows)
			{
				var appData = Environment.GetEnvironmentVariable("APPDATA");
				if (string.IsNullOrWhiteSpace(appData))
					appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				if (string.IsNullOrWhiteSpace(appData))
					appData = Path.Combine(ResolveSystemHomeDir(), "AppData", "Roaming");

				return NormalizeConfigPath(Path.Combine(appData, "RAIkeep", DefaultConfigFileName));
			}

			return NormalizeConfigPath(Path.Combine(ResolveSystemHomeDir(), ".config", "RAIkeep", DefaultConfigFileName));
		}

		[Obsolete("Use GetDefaultConfigPath() instead.")]
		public static string GetDefaultCloudConfigPath() => GetDefaultConfigPath();

		internal static IReadOnlyList<CloudStorageType> CreateDefaultCloudOrder()
		{
			return new[]
			{
				CloudStorageType.GoogleDrive,
				CloudStorageType.ICloud,
				CloudStorageType.Dropbox,
				CloudStorageType.OneDrive
			};
		}

		private static void InvalidateConfiguredPathCaches()
		{
			homeDir = null;
			tempDir = null;
			localBackupDir = null;
		}

		private static string NormalizeConfigPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return string.Empty;

			var expanded = Environment.ExpandEnvironmentVariables(path);
			if (expanded.StartsWith("~/", StringComparison.Ordinal))
				expanded = Path.Combine(ResolveSystemHomeDir(), expanded.Substring(2));

			return Path.GetFullPath(expanded);
		}

		private static Dictionary<CloudStorageType, string> DiscoverCloudStorageRoots()
		{
			var roots = new Dictionary<CloudStorageType, string>();
			ApplyConfiguredRoots(roots, Config.Data.Cloud);
			ApplyProviderProbes(roots);
			PersistDiscoveredCloudRoots(roots);
			return roots;
		}

		private static void PersistDiscoveredCloudRoots(IReadOnlyDictionary<CloudStorageType, string> roots)
		{
			if (Config.Data.Cloud.MergeMissingDiscoveredRoots(roots))
				Config.Save();
		}

		private static void ApplyConfiguredRoots(Dictionary<CloudStorageType, string> roots, CloudModel cloudConfig)
		{
			if (cloudConfig == null)
				return;

			foreach (var provider in Enum.GetValues<CloudStorageType>())
			{
				var configuredRoot = cloudConfig.GetRoot(provider)?.Path;
				if (string.IsNullOrWhiteSpace(configuredRoot))
					continue;

				roots[provider] = new RaiPath(configuredRoot).Path;
			}
		}

		private static void ApplyProviderProbes(Dictionary<CloudStorageType, string> roots)
		{
			if (!roots.ContainsKey(CloudStorageType.Dropbox))
				ProbeDropbox(roots);
			if (!roots.ContainsKey(CloudStorageType.OneDrive))
				ProbeOneDrive(roots);
			if (!roots.ContainsKey(CloudStorageType.GoogleDrive))
				ProbeGoogleDrive(roots);
			if (!roots.ContainsKey(CloudStorageType.ICloud))
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
				var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				if (!string.IsNullOrWhiteSpace(appData))
					yield return Path.Combine(appData, "Dropbox", "info.json");
				if (!string.IsNullOrWhiteSpace(localAppData))
					yield return Path.Combine(localAppData, "Dropbox", "info.json");
			}
			else
			{
				yield return Path.Combine(ResolveSystemHomeDir(), ".dropbox", "info.json");
			}
		}

		private static void ProbeOneDrive(Dictionary<CloudStorageType, string> roots)
		{
			TryAddRoot(roots, CloudStorageType.OneDrive, "~/OneDrive");
			TryAddRoot(roots, CloudStorageType.OneDrive, "~/OneDrive - Personal");
			TryAddRoot(roots, CloudStorageType.OneDrive, "~/Library/CloudStorage/OneDrive");

			foreach (var path in SafeEnumerateDirectories(HomeDir, "OneDrive*"))
				TryAddRoot(roots, CloudStorageType.OneDrive, path);

			foreach (var path in SafeEnumerateDirectories(new RaiFile(Path.Combine(HomeDir, "Library", "CloudStorage")).Path, "OneDrive*"))
				TryAddRoot(roots, CloudStorageType.OneDrive, path);
		}

		private static void ProbeGoogleDrive(Dictionary<CloudStorageType, string> roots)
		{
			if (Type == OsType.MacOS)
			{
				foreach (var path in SafeEnumerateDirectories(new RaiFile(Path.Combine(HomeDir, "Library", "CloudStorage")).Path, "GoogleDrive*"))
					TryAddRoot(roots, CloudStorageType.GoogleDrive, GetMacGoogleDriveProbeTarget(path));

				TryAddRoot(roots, CloudStorageType.GoogleDrive, "~/GoogleDrive");
				TryAddRoot(roots, CloudStorageType.GoogleDrive, "~/Google Drive");
				return;
			}

			if (Type == OsType.Windows)
			{
				var user = ResolveSystemHomeDir();
				if (!string.IsNullOrWhiteSpace(user))
				{
					TryAddRoot(roots, CloudStorageType.GoogleDrive, Path.Combine(user, "Google Drive"));
					TryAddRoot(roots, CloudStorageType.GoogleDrive, Path.Combine(user, "My Drive"));
				}
				return;
			}

			TryAddRoot(roots, CloudStorageType.GoogleDrive, "~/GoogleDrive");
			TryAddRoot(roots, CloudStorageType.GoogleDrive, "~/Google Drive");
		}

		internal static string GetMacGoogleDriveProbeTarget(string cloudStorageEntryPath)
		{
			var expanded = ExpandPath(cloudStorageEntryPath);
			if (string.IsNullOrWhiteSpace(expanded))
				return cloudStorageEntryPath;

			var myDrivePath = Path.Combine(expanded, "My Drive");
			if (Directory.Exists(myDrivePath))
				return new RaiPath(myDrivePath).Path;

			return new RaiPath(expanded).Path;
		}

		private static void ProbeICloud(Dictionary<CloudStorageType, string> roots)
		{
			TryAddRoot(roots, CloudStorageType.ICloud, "~/Library/Mobile Documents/com~apple~CloudDocs");
			TryAddRoot(roots, CloudStorageType.ICloud, "~/iCloudDrive");
			TryAddRoot(roots, CloudStorageType.ICloud, "~/Cloud/iCloud");
			TryAddRoot(roots, CloudStorageType.ICloud, "~/rclone/iCloud");

			if (Type == OsType.Windows)
			{
				var user = ResolveSystemHomeDir();
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
			catch
			{
			}

			return Array.Empty<string>();
		}
	}
}
