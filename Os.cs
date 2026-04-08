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
		private static readonly string defaultConfigFileLocation = "~/.config/RAIkeep/osconfig.json5";   // single machine-local config contract
		public static RaiPath UserHomeDir
		{
			get
			{
				if (userHomeDir == null)
				{
					string resolved;
					if (Type == OsType.Windows)
					{
						resolved = EnsureTrailingDirectorySeparator(Environment.GetEnvironmentVariable("USERPROFILE"));
						if (string.IsNullOrWhiteSpace(resolved))
						{
							var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
							var homePath = EnsureTrailingDirectorySeparator(Environment.GetEnvironmentVariable("HOMEPATH"));
							if (!string.IsNullOrEmpty(homeDrive) && !string.IsNullOrEmpty(homePath))
								resolved = NormSeperator(homeDrive + homePath);
						}
					}
					else
					{
						resolved = EnsureTrailingDirectorySeparator(Environment.GetEnvironmentVariable("HOME"));
					}
					if (string.IsNullOrEmpty(resolved))
						resolved = EnsureTrailingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty);
					if (string.IsNullOrWhiteSpace(resolved))
						LogWarningOnce<OsDiagnosticsLogScope>("fallback:userhome:empty", "User home directory could not be resolved from environment variables. SpecialFolder fallback returned an empty value.");
					userHomeDir = new RaiPath(resolved);
				}
				LogDebug<OsDiagnosticsLogScope>("Resolved user home directory to {UserHomeDir}", userHomeDir.Path);
				return userHomeDir;
			}
		}
		public static RaiPath AppRootDir
		{
			get
			{
				appRootDir ??= new RaiPath(EnsureTrailingDirectorySeparator(Directory.GetCurrentDirectory()));
				LogDebug<OsDiagnosticsLogScope>("Resolved application root directory to {AppRootDir}", appRootDir.Path);
				return appRootDir;
			}
		}
		public static RaiPath CloudStorageRootDir
		{
			get
			{
				if (cloudStorageRootDir != null)
					return cloudStorageRootDir;

				Exception? lastException = null;
				foreach (var provider in GetEffectiveDefaultCloudOrder())
				{
					try
					{
						var candidate = GetCloudStorageRoot(provider, refresh: false);
						if (candidate != null && candidate.Exists())
						{
							cloudStorageRootDir = candidate;
							return cloudStorageRootDir;
						}
					}
					catch (Exception ex)
					{
						lastException = ex;
					}
				}

				var message = $"No cloud storage root is configured or accessible. {GetCloudStorageSetupGuidance()}";
				if (lastException != null)
					LogError<OsDiagnosticsLogScope>(lastException, "{Message}", message);

				throw new DirectoryNotFoundException(message);
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
					var activeConfig = Config as JObject;
					var configuredTempDir = GetConfigString(activeConfig, "TempDir");
					if (string.IsNullOrWhiteSpace(configuredTempDir))
						throw new OsConfigValidationException($"TempDir is missing. {GetCloudStorageSetupGuidance()}");
					tempDir = ResolveConfiguredDirectory(configuredTempDir);
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
				if (localBackupDir != null || localBackupDirDisabled)
					return localBackupDir;

				var activeConfig = Config as JObject;
				var configuredLocalBackupDir = GetConfigString(activeConfig, "LocalBackupDir");
				if (string.IsNullOrWhiteSpace(configuredLocalBackupDir))
				{
					localBackupDirDisabled = true;
					return null;
				}

				localBackupDir = ResolveConfiguredDirectory(configuredLocalBackupDir);
				return localBackupDir;
			}
		}
		private static string dIRSEPERATOR = System.IO.Path.DirectorySeparatorChar.ToString();       // changed internal representation; use EscapeMode.backslashed to convert to "\\"
		/// <summary>
		/// for brevity: same as Os.DIRSEPERATOR, \ or / depending on the operating system
		/// </summary>
		public static string DIR => dIRSEPERATOR;
		public static string DIRSEPERATOR => dIRSEPERATOR; // to save some ToString() calls
		public const string ESCAPECHAR = "\\";
		public const string DATEFORMAT = "yyyy-MM-dd HH.mm.ss"; // missing in older versions
		public static DateTimeOffset ParseDateTime(string datetimeInDATEFORMAT)
		{
			var a = datetimeInDATEFORMAT.Split(new char[] { '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			return new DateTimeOffset(new DateTime(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4]), int.Parse(a[5])));
		}
		public static string EscapeParam(string param)
		{        // "..."
			if (param[0] == '\"')
				return param;
			return '\"' + param + '\"';
		}
		public static string EscapeBlank(string name)
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
		public static string WinInternal(string fullname)
		{  // every / is replaced by a \ in the copy
			char dirChar = Os.DIR[0];
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
						path = path.Replace('/', DIR[0]);
					break;
			}
			return path;
		}
		public static string Escape(string s, EscapeMode mode)
		{
			if (mode == EscapeMode.noEsc)
				return s;
			if (mode == EscapeMode.blankEsc)
				return Os.EscapeBlank(s);
			if (mode == EscapeMode.paramEsc)
				return Os.EscapeParam(s);
			if (mode == EscapeMode.backslashed)
				return Os.WinInternal(s);
			return s;
		}
		public static string NormSeperator(string s)
		{
			return s.Replace(@"\", DIR);
		}
		internal static string EnsureTrailingDirectorySeparator(string s)
		{
			if (string.IsNullOrWhiteSpace(s))
				return string.Empty;

			//s = NormSeperator(s);
			return s.EndsWith(DIR, StringComparison.Ordinal) ? s : s + DIR;
		}
		internal static string ParentDirectory(string s)
		{
			if (string.IsNullOrWhiteSpace(s))
				return string.Empty;

			var trimmed = NormSeperator(s).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (string.IsNullOrWhiteSpace(trimmed))
				return DIR;

			var parent = Path.GetDirectoryName(trimmed);
			if (string.IsNullOrWhiteSpace(parent))
				parent = Path.GetPathRoot(trimmed) ?? string.Empty;

			return EnsureTrailingDirectorySeparator(parent);
		}
		internal static string ExpandLeadingDirectorySymbols(string dirString)
		{
			if (string.IsNullOrWhiteSpace(dirString))
				return dirString;
			//dirString = NormSeperator(dirString);
			if (dirString == "~")
				return EnsureTrailingDirectorySeparator(UserHomeDir.Path);
			if (dirString.StartsWith("~/", StringComparison.Ordinal))
				return EnsureTrailingDirectorySeparator(UserHomeDir.Path) + dirString.Substring(2);
			return EnsureTrailingDirectorySeparator(Path.GetFullPath(dirString));
		}

		private static RaiPath localBackupDir = null;
		private static bool localBackupDirDisabled;
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