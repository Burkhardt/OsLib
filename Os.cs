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
namespace OsLib
{
	public enum EscapeMode { noEsc, blankEsc, paramEsc, backslashed };
	public enum OsType { Windows, MacOS, Ubuntu };
	public static partial class Os
	{
		public static readonly string DefaultConfigFileLocation = "~/.config/RAIkeep.json5";
		private static OsType? type = null;
		private static readonly string DIRSEPERATOR = System.IO.Path.DirectorySeparatorChar.ToString();
		public static readonly string DIR = DIRSEPERATOR;
		public const string ESCAPECHAR = "\\";
		public const string DATEFORMAT = "yyyy-MM-dd HH.mm.ss";
		public static OsType Type
		{
			get
			{
				if (type == null)
				{
					type = OsType.Ubuntu;
					if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
						type = OsType.Windows;
					if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
						type = OsType.MacOS;
				}
				return type.Value;
			}
		}
		public static bool IsWindows => Type == OsType.Windows;
		public static bool IsMacOS => Type == OsType.MacOS;
		public static bool IsUnixLike => Type != OsType.Windows;
		public static bool IsLinuxLike => Type == OsType.Ubuntu;
		public static RaiPath UserHomeDir
		{
			get
			{
				var path = runtime.UserHomeDir;
				LogDebug<OsDiagnosticsLogScope>("Resolved user home directory to {UserHomeDir}", path.Path);
				return path;
			}
		}
		public static RaiPath AppRootDir
		{
			get
			{
				var path = runtime.AppRootDir;
				LogDebug<OsDiagnosticsLogScope>("Resolved application root directory to {AppRootDir}", path.Path);
				return path;
			}
		}
		public static RaiPath LocalBackupDir
		{
			get
			{
				return runtime.LocalBackupDir;
			}
		}
		public static string NewShortId(int length = 4)
		{
			length = Math.Clamp(length, 1, 32);
			return Guid.NewGuid().ToString("N").Substring(0, length);
		}
		public static DateTimeOffset ParseDateTime(string datetimeInDATEFORMAT)
		{
			var a = datetimeInDATEFORMAT.Split(new[] { '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			return new DateTimeOffset(new DateTime(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4]), int.Parse(a[5])));
		}
		public static string EscapeParam(string param) => (param.StartsWith("\"") && param.EndsWith("\"")) ? param : $"\"{param}\"";
		public static string EscapeBlank(string name) => name.Replace(" ", ESCAPECHAR + " ");
		public static string WinInternal(string fullname) => NormSeperator(fullname);
		public static string NormPath(string path) => NormSeperator(path);
		public static string Escape(string s, EscapeMode mode) => mode switch
		{
			EscapeMode.noEsc => s,
			EscapeMode.blankEsc => EscapeBlank(s),
			EscapeMode.paramEsc => EscapeParam(s),
			EscapeMode.backslashed => WinInternal(s),
			_ => s
		};
		public static string NormSeperator(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return s;
			return s.Replace(System.IO.Path.AltDirectorySeparatorChar.ToString(), Os.DIR);
		}
		internal static string ensureTrailingDirSeparator(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) s = ".";
			return s.EndsWith(DIR, StringComparison.Ordinal) ? s : s + DIR;
		}
		internal static string parentDir(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return string.Empty;
			var p = NormSeperator(s);
			if (p.EndsWith(DIR)) p = p.Substring(0, p.Length - DIR.Length);
			if (string.IsNullOrWhiteSpace(p)) return DIR;
			var parent = Path.GetDirectoryName(p);
			if (string.IsNullOrWhiteSpace(parent)) parent = Path.GetPathRoot(p) ?? string.Empty;
			return ensureTrailingDirSeparator(parent);
		}
		internal static string expandLeadingDirSymbols(string dirString)
		{
			if (string.IsNullOrWhiteSpace(dirString)) return dirString;
			dirString = NormSeperator(dirString);
			if (dirString == "~") return ensureTrailingDirSeparator(UserHomeDir.Path);
			var homePrefix = "~" + DIR;
			if (dirString.StartsWith(homePrefix, StringComparison.Ordinal))
				return ensureTrailingDirSeparator(UserHomeDir.Path) + dirString.Substring(homePrefix.Length);
			return ensureTrailingDirSeparator(System.IO.Path.GetFullPath(dirString));
		}
	}
}
