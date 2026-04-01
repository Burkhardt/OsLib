using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OsLib     // aka OsLibCore
{
	public enum EscapeMode { noEsc, blankEsc, paramEsc, backslashed };
	public enum OsType { Windows, MacOS, Ubuntu };
	/// <summary>
	/// Provides OS-aware environment and path utilities, including platform detection,
	/// home/temp directory discovery, separator normalization, and cloud-root lookup.
	/// </summary>
	public static partial class Os
	{
		private static readonly string defaultConfigFileLocation = "~/.config/RAIkeep/osconfig.json";   // see Os.CloudStorage.cs for ConfigFile<OsConfigModel>
		public static RaiPath UserHomeDir
		{
			get
			{
				userHomeDir ??= new RaiPath(ResolveSystemHomeDir());
				LogDebug<OsDiagnosticsLogScope>("Resolved user home directory to {UserHomeDir}", userHomeDir.Path);
				return userHomeDir;
			}
		}
		public static RaiPath AppRootDir
		{
			get
			{
				appRootDir ??= new RaiPath(Directory.GetCurrentDirectory());
				LogDebug<OsDiagnosticsLogScope>("Resolved application root directory to {AppRootDir}", appRootDir.Path);
				return appRootDir;
			}
		}
		public static RaiPath CloudStorageRootDir
		{
			get
			{
				string provider = null;
				try
				{
					provider = ((IEnumerable<string>)Config.DefaultCloudOrder).FirstOrDefault();
					if (string.IsNullOrWhiteSpace(provider) || !Enum.TryParse<Cloud>(provider, true, out var parsedProvider))
						throw new InvalidDataException($"Invalid cloud storage type '{provider}' in configuration file '{defaultConfigFileLocation}'.");

					cloudStorageRootDir = GetCloudStorageRoot(parsedProvider, refresh: false);
				}
				catch (Exception ex)
				{
					LogError<OsDiagnosticsLogScope>(ex, "Failed to resolve cloud storage root directory for provider {Provider}", provider);
				}
				return cloudStorageRootDir;
			}
		}
		public static bool IsWindows => Type == OsType.Windows;
		public static bool IsMacOS => Type == OsType.MacOS;
		public static OsType Type
		{
			get
			{
				if (type == null)
					type = DetectOsType();
				return (OsType)type;
			}
		}
		public static bool IsUnixLike => Type != OsType.Windows;
		public static bool IsLinuxLike => Type == OsType.Ubuntu;
		private static RaiPath userHomeDir = null;
		private static RaiPath appRootDir = null;
		private static RaiPath cloudStorageRootDir = null;
		private static OsType? type = null;
		public static RaiPath TempDir
		{
			get
			{
				if (tempDir == null)
				{
					string configuredTempDir = null;
					try
					{
						configuredTempDir = (string)Config.TempDir;
					}
					catch (Exception ex)
					{
						LogError<OsDiagnosticsLogScope>(ex, "Failed to resolve configured temp directory. Falling back to operating system temp directory {TempDir}", tempDir.Path);
						configuredTempDir = null;
					}
					tempDir = string.IsNullOrWhiteSpace(configuredTempDir) ? Os.TempDir : new RaiPath(configuredTempDir);
				}
				return tempDir;
			}
		}
		public static string NewShortId(int length = 4)
		{
			if (length < 1)
				length = 1;
			if (length > 32)
				length = 32;

			return Guid.NewGuid().ToString("N").Substring(0, length);
		}
		private static RaiPath tempDir = null;
		public static RaiPath LocalBackupDir
		{
			get
			{
				try
				{
					if (localBackupDir == null)
					{
						var localBackupDir = Config.localBackupDir;
					}
				}
				catch (Exception ex)
				{
					localBackupDir = TempDir;
					LogError<OsDiagnosticsLogScope>(ex, "Failed to resolve configured local backup directory. Falling back to operating system temp directory {localBackupDir}", localBackupDir.Path);
				}
				return localBackupDir;
			}
		}
		public static string DIRSEPERATOR
		{
			get
			{
				if (dIRSEPERATOR == null)
					dIRSEPERATOR = System.IO.Path.DirectorySeparatorChar.ToString();
				return dIRSEPERATOR;
			}
		}
		private static string dIRSEPERATOR = null;       // changed internal representation; use EscapeMode.backslashed to convert to "\\"
		public const string ESCAPECHAR = "\\";
		public const string DATEFORMAT = "yyyy-MM-dd HH.mm.ss"; // missing in older versions
		public static DateTimeOffset ParseDateTime(string datetimeInDATEFORMAT)
		{
			var a = datetimeInDATEFORMAT.Split(new char[] { '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			return new DateTimeOffset(new DateTime(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4]), int.Parse(a[5])));
		}
		public static string escapeParam(string param)
		{        // "..."
			if (param[0] == '\"')
				return param;
			return '\"' + param + '\"';
		}
		public static string escapeBlank(string name)
		{     // every whitespace char will be escaped by insertion of ESCAPECHAR
			var s = name;
			for (int i = 0; i < s.Length; i++)
			{
				if (s[i] == ' ')
				{
					s = s.Insert(i, Os.ESCAPECHAR);
					i++;
				}
			}
			return s;
		}
		public static string winInternal(string fullname)
		{  // every / is replaced by a \ in the copy
			char dirChar = Os.DIRSEPERATOR[0];
			if (fullname != null && dirChar != '/')
				fullname = fullname.Replace('/', dirChar); //fullname = fullname.Replace('/', '\\');
			return fullname;
		}
		/// <summary>
		/// Use NormPath when a new path is created, not for internal functions that use the Os context
		/// </summary>
		/// <param name="path"></param>
		/// <returns>Path adjusted to the operating system the code is currently running on</returns>
		/// <remarks>It is probably fair to assume that the use of unix conventions for paths are standard throughout the code. If the system runs on Windows, NormPath will adjust the path accordingly.</remarks>
		public static string NormPath(string path)
		{
			switch (Os.type)
			{
				case OsType.Windows:    // most of the time this means that the path has to get converted
										// check if the path is already in unix format, no : no \
					if (path != null && !path.Contains(':') && !path.Contains('\\'))
						return path;
					if (path != null)
						path = path.Replace('/', DIRSEPERATOR[0]);
					break;
			}
			return path;
		}
		public static string Escape(string s, EscapeMode mode)
		{
			if (mode == EscapeMode.noEsc)
				return s;
			if (mode == EscapeMode.blankEsc)
				return Os.escapeBlank(s);
			if (mode == EscapeMode.paramEsc)
				return Os.escapeParam(s);
			if (mode == EscapeMode.backslashed)
				return Os.winInternal(s);
			return s;
		}
		public static string NormSeperator(string s)
		{
			return s.Replace(@"\", DIRSEPERATOR);
		}
		private static RaiPath localBackupDir = null;
		internal static string ResolveSystemHomeDir()
		{
			var resolved = string.Empty;
			if (Type == OsType.Windows)
			{
				resolved = Environment.GetEnvironmentVariable("USERPROFILE");
				if (string.IsNullOrEmpty(resolved))
				{
					var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
					var homePath = Environment.GetEnvironmentVariable("HOMEPATH");
					if (!string.IsNullOrEmpty(homeDrive) && !string.IsNullOrEmpty(homePath))
						resolved = homeDrive + homePath;
				}
			}
			else
			{
				resolved = Environment.GetEnvironmentVariable("HOME");
			}
			if (string.IsNullOrEmpty(resolved))
				resolved = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
			if (string.IsNullOrWhiteSpace(resolved))
				LogWarningOnce<OsDiagnosticsLogScope>("fallback:userhome:empty", "User home directory could not be resolved from environment variables. SpecialFolder fallback returned an empty value.");
			return resolved;
		}
		[Obsolete("Use Os.TempDir instead")]
		internal static string ResolveSystemTempDir()
		{
			var resolved = Path.GetTempPath();
			if (string.IsNullOrWhiteSpace(resolved))
				resolved = Directory.GetCurrentDirectory();

			return resolved;
		}
		[Obsolete("Use Os.LocalBackupDir instead")]
		internal static string ResolveSystemLocalBackupDir()
		{
			throw new NotImplementedException();
		}
		[Obsolete("Use Os.LocalBackupDir instead")]
		private static RaiPath ResolveConfiguredOrDefaultLocalBackupDir()
		{
			throw new NotImplementedException();
		}
		private static IEnumerable<string> GetLocalBackupDirCandidates()
		{
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if (!string.IsNullOrWhiteSpace(localAppData))
				yield return Path.Combine(localAppData, "OsLib", "Backup");

			if (Type == OsType.Windows)
			{
				var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
				if (!string.IsNullOrWhiteSpace(userProfile))
					yield return Path.Combine(userProfile, "AppData", "Local", "OsLib", "Backup");
			}
			else
			{
				yield return "~/.local/share/OsLib/Backup";
				yield return "~/.oslib/backup";
				yield return "~/Backup";
			}

			yield return Path.Combine(ResolveSystemTempDir(), "OsLib", "Backup");
		}
		private static string NormalizeBackupDirectoryCandidate(string candidate)
		{
			if (string.IsNullOrWhiteSpace(candidate))
				return null;

			var expanded = candidate.Trim();
			if (expanded.StartsWith("~/"))
				expanded = $"{UserHomeDir.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}{expanded.Substring(1)}";

			return new RaiPath(expanded).Path;
		}
		private static OsType DetectOsType()
		{
			if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
				return OsType.Windows;

			if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
				return OsType.MacOS;

			if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
				return OsType.Ubuntu;

			return OsType.Ubuntu;
		}
		private static bool IsUbuntuRuntime()
		{
			try
			{
				const string osRelease = "/etc/os-release";
				if (!File.Exists(osRelease))
					return false;

				foreach (var line in File.ReadAllLines(osRelease))
				{
					if (!line.StartsWith("ID=", StringComparison.OrdinalIgnoreCase) &&
						!line.StartsWith("ID_LIKE=", StringComparison.OrdinalIgnoreCase))
						continue;

					var value = line.Substring(line.IndexOf('=') + 1).Trim().Trim('"');
					if (value.Contains("ubuntu", StringComparison.OrdinalIgnoreCase))
						return true;
				}
			}
			catch
			{
			}

			return false;
		}

	}
}