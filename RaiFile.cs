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


/*
 *	based on RsbFile (C++ version from 1991, C# version 2005)
 */
namespace OsLib     // aka OsLibCore
{
	public enum EscapeMode { noEsc, blankEsc, paramEsc, backslashed };
	public enum OsType { Windows, MacOS, Ubuntu };
	public enum CloudStorageType { Dropbox, OneDrive, GoogleDrive, ICloud };
	/// <summary>
	/// Provides OS-aware environment and path utilities, including platform detection,
	/// home/temp directory discovery, separator normalization, and cloud-root lookup.
	/// </summary>
	public partial class Os
	{
		public static string HomeDir
		{
			get
			{
				if (homeDir == null)
				{
					var configured = Config.Data.HomeDir?.Path?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
					homeDir = string.IsNullOrWhiteSpace(configured)
						? ResolveSystemHomeDir().TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
						: configured;
				}
				return homeDir;
			}
		}
		private static string homeDir = null;
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
		private static OsType? type = null;
		public static string TempDir
		{
			get
			{
				if (tempDir == null)
				{
					var configured = Config.Data.TempDir?.Path;
					tempDir = string.IsNullOrWhiteSpace(configured)
						? ResolveSystemTempDir()
						: configured;
				}
				return tempDir;
			}
		}
		private static string tempDir = null;
		public static string LocalBackupDir
		{
			get
			{
				if (localBackupDir == null)
					localBackupDir = ResolveConfiguredOrDefaultLocalBackupDir();
				return localBackupDir;
			}
		}
		[Obsolete("Use LocalBackupDir instead.")]
		public static string LocalBackupDirUnix => LocalBackupDir;

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

		private static string localBackupDir = null;

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

			return resolved;
		}

		internal static string ResolveSystemTempDir()
		{
			return System.IO.Path.GetTempPath();
		}

		internal static string ResolveSystemLocalBackupDir()
		{
			foreach (var candidate in GetLocalBackupDirCandidates())
			{
				var normalized = NormalizeBackupDirectoryCandidate(candidate);
				if (!string.IsNullOrWhiteSpace(normalized))
					return normalized;
			}

			return new RaiPath(Path.Combine(ResolveSystemTempDir(), "OsLib", "Backup")).Path;
		}

		private static string ResolveConfiguredOrDefaultLocalBackupDir()
		{
			var configured = Config.Data.LocalBackupDir?.Path;
			if (!string.IsNullOrWhiteSpace(configured) && !IsCloudPath(configured))
				return configured;

			foreach (var candidate in GetLocalBackupDirCandidates())
			{
				var normalized = NormalizeBackupDirectoryCandidate(candidate);
				if (!string.IsNullOrWhiteSpace(normalized) && !IsCloudPath(normalized))
					return normalized;
			}

			return ResolveSystemLocalBackupDir();
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

			var expanded = Environment.ExpandEnvironmentVariables(candidate.Trim());
			if (expanded.StartsWith("~/"))
				expanded = $"{HomeDir}{expanded.Substring(1)}";

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

	/// <summary>
	/// Convenience extensions for string and CSV handling.
	/// </summary>
	public static class RaiFileExtensions
	{
		/// <summary>
		/// MakePolicyCompliant
		/// \n will be removed – subsequent string will be put in a new line
		/// \t is allowed and will not be removed, even if multiple (beginning of line and end of line)
		/// 'multiple whitespaces will be reduced to 1, no matter where they occur (use \t for indention of value seperation)
		/// empty lines will be removed
		/// </summary>
		/// <returns>a new List of string</returns>
		public static List<string> MakePolicyCompliant(this List<string> lines, bool tabbed = true)
		{
			var buffer = new List<string>();
			List<string> multi;
			foreach (var line in lines)
			{
				multi = line.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
				foreach (var one in multi)
				{
					if (tabbed)
						buffer.Add(one.Replace("    ", "\t").Replace("  ", "\t").Singularize(' ').Singularize('\t').Replace("\t ", "\t"));
					else buffer.Add(one.Singularize(' '));
				}
			}
			var compliant = new List<string>();
			foreach (var line in buffer)
				if (line.Length > 0)
					compliant.Add(line);
			return compliant;
		}
		/// <summary>
		/// Collapse repeated occurrences of a character into a single instance.
		/// </summary>
		public static string Singularize(this string line, char c, bool trim = true)
		{
			if (line.Length == 0)
				return line;
			var result = new List<char>();
			result.Add(line[0]);
			int j = 1;
			for (int i = 1; i < line.Length; i++)
			{
				if (result[j - 1] != c || line[i] != c)
				{
					result.Add(line[i]);
					j++;
				}
			}
			return trim ? new string(result.ToArray()).Trim(c) : new string(result.ToArray());
		}
		/// <summary>
		/// Convert tab-separated lines into a list of dictionaries keyed by the header row.
		/// </summary>
		public static List<Dictionary<string, string>> CreateDictionariesFromCsvLines(this string lines, bool tabbed = true)
		{
			var tab = (new List<string> { lines }).MakePolicyCompliant(tabbed: true);
			var keys = tab[0].Split(new char[] { '\t' });
			var list = new List<Dictionary<string, string>>();
			for (int i = 1; tab.Count > i; i++)
			{
				var v = tab[i].Split(new char[] { '\t' });
				var dict = new Dictionary<string, string>();
				for (int j = 0; j < keys.Length; j++)
				{
					if (v.Length < j)
						throw new FieldAccessException($"csv input - out of bounds for index {j}; field list: {string.Join(',', keys.AsEnumerable())}");
					dict.Add(keys[j], v[j]);
				}
				list.Add(dict);
			}
			return list;
		}
	}
	/// <summary>
	/// RaiPath – uses operator/ to add a subdirectory to a path
	/// just the path, no filename, no extension
	/// </summary>
	/// <summary>
	/// Represents a directory path and enforces a trailing directory separator.
	/// </summary>
	public class RaiPath
	{
		public string Path
		{
			get
			{
				return path.Path;
			}
			set
			{
				// make sure the last character of the passed path is a directory separator
				path = new RaiFile(value);
				path.Name = string.Empty;
				path.Ext = string.Empty;
			}
		}
		private RaiFile path;
		/// <summary>
		/// Using the / operator to add a subdirectory to a path
		/// </summary>
		/// <param name="self"></param>
		/// <param name="subDir">string</param>
		/// <returns>RaiPath object for daisy chaining reasons</returns>
		public static RaiPath operator /(RaiPath self, string subDir)
		{
			return new RaiPath(self.path.Path + subDir + Os.DIRSEPERATOR);
		}
		/// <summary>
		/// Constructor that takes a string path; the caller knows that this is a directory path; it does not have to exist yet in the file system.
		/// </summary>
		/// <param name="s">if value of s does not end with a directory separator, one will be added</param>
		public RaiPath(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				Path = string.Empty;
				return;
			}
			Path = s[^1] == Os.DIRSEPERATOR[0] ? s : s + Os.DIRSEPERATOR;
		}

		/// <summary>
		/// Constructor that takes a RaiFile object; uses its Path and ignores Name and Ext.
		/// </summary>
		public RaiPath(RaiFile f)
		{
			path = f;
			path.Name = string.Empty;
			path.Ext = string.Empty;
		}
		public void mkdir() => new RaiFile(Path).mkdir();
		public void rmdir(int depth = 0, bool deleteFiles = false) => new RaiFile(Path).rmdir(depth, deleteFiles);
		public override string ToString() => Path;
	}
	/// <summary>
	/// File and directory utility with path parsing and cloud-aware behaviors.
	/// </summary>
	public class RaiFile
	{
		const int maxWaitCount = 60;  // raised from 15 as a result of a failed test case: TestComparePic9AndPic7ZoomTrees, 2012-12-23, RSB.
									  // raised from 20 as a result of a failed test case: TestUserRoleSubscriberAccess, Pic8, 2014-03-03, RSB.
									  // raised from 25 as a result of a failed test runs on Pic8 (which probably has a slow disk compared to other servers), 2014-03-16, RSB.
		private string name;
		public bool Cloud;
		/// <summary>
		/// // without dir structure and without extension
		/// </summary>				
		public virtual string Name
		{
			get { return string.IsNullOrEmpty(name) ? string.Empty : name; }
			set
			{   // sets name and ext; override to set more name components
				name = Os.NormSeperator(value);
				var pos = name.LastIndexOf("/");
				if (pos >= 0 && name.Length > pos)
					name = name.Remove(0, pos + 1);
				pos = name.LastIndexOf(".");
				if (pos >= 0)
				{
					if (name.Length > pos + 1)
					{
						ext = name.Substring(pos + 1);
						name = name.Remove(pos);
					}
					else ext = string.Empty;
				}
				else if (ext == null)
					ext = string.Empty;
			}
		}
		/// <summary>
		/// without dir structure but with "." and with extension, ie 123456.png
		/// </summary>				
		public virtual string NameWithExtension
		{
			get { return string.IsNullOrEmpty(name) && string.IsNullOrEmpty(Ext) ? string.Empty : Name + (string.IsNullOrEmpty(ext) ? string.Empty : "." + Ext); }
		}
		private string ext;
		/// <summary>
		/// extension of the picture without '.', ie "png"
		/// </summary>
		public string Ext
		{
			get { return ext; }
			set { ext = value; }
		}
		private string path = string.Empty;                // the source directory of the picture, ends with a dirSeperator
		/// <summary>
		/// the source directory of the file, ends with a dirSeperator; Ensure will be set to memorize if the file is in the cloud
		/// </summary>
		public virtual string Path
		{
			get { return path; }
			set
			{
				if (string.IsNullOrEmpty(value))
					path = string.Empty;
				else
				{
					path = Os.NormSeperator(value);
					if (path[^1] != Os.DIRSEPERATOR[0])
						path = path + Os.DIRSEPERATOR;
					UpdateCloudFlag();
				}
			}
		}

		private void UpdateCloudFlag()
		{
			Cloud = Os.IsCloudPath(path);
		}

		public virtual string FullName
		{
			get { return Path + NameWithExtension; }
		}
		/// <summary>
		/// Check if the file currently exists in the file system
		/// </summary>
		/// <returns></returns>
		public bool Exists()
		{
			return File.Exists(FullName);
		}
		/// <summary>
		/// Remove this file from the file system.
		/// </summary>
		public int rm() // removes file from the file system 
		{
			var name = FullName;
			if (File.Exists(name))
			{
				File.Delete(name);
				#region double check if file is gone
				if (Cloud)
					return awaitFileVanishing(name);
				#endregion
			}
			return 0;
		}
		public int mv(RaiFile from) => mv(from, replace: true, keepBackup: false);
		public int mv(RaiFile from, bool replace = true) => mv(from, replace, keepBackup: false);

		/// <summary>
		/// Move a file in the file system.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="replace">if true, an existing file at destination will be overwritten</param>
		/// <param name="keepBackup">keeps the backup file if true</param>
		/// <returns>the result of the OS operation</returns>
		public int mv(RaiFile from, bool replace, bool keepBackup)         // relocates file in the file system
		{
			// Semantics: destination is this.FullName, source is from.FullName
			var dest = FullName;        // destination (this)
			var src = from.FullName;   // source (from)

			// make sure from and this do not point to the same file
			if (src == dest)
				return 0;

			mkdir(); // create destination dir if necessary; applies ensure

			if (!File.Exists(src))
				throw new FileNotFoundException("Source file does not exist: " + src);

			var destExists = File.Exists(dest);

			if (destExists && !replace)
				throw new IOException("Destination file already exists: " + dest + " and replace is false");

			// If destination exists and we are allowed to replace: try atomic-ish replace first
			if (destExists && replace)
			{
				var bak = new RaiFile(dest);
				bak.Ext = "bak";

				try
				{
					// Remove stale backup if it exists
					if (bak.Exists())
						bak.rm();

					// System.IO: Replace(source, destination, backup)
					// - destination becomes the content of source
					// - backup receives the old destination
					// - source is removed
					File.Replace(src, dest, bak.FullName, ignoreMetadataErrors: true);

					// keepBackup controls whether the .bak survives
					if (!keepBackup)
						bak.rm();

					if (Cloud)
						return awaitFileVanishing(src) + awaitFileMaterializing(dest);

					return 0;
				}
				catch (IOException)
				{
					// Fallback path: if caller wants backup, copy the old destination before deleting it
					// (File.Replace would have created it; here we emulate that behavior)
					if (keepBackup && File.Exists(dest))
					{
						// Ensure stale backup is gone
						if (bak.Exists())
							bak.rm();

						// Copy old destination to .bak (do NOT use RaiFile.cp because it rm()s destination)
						File.Copy(dest, bak.FullName, overwrite: true);

						if (Cloud)
							awaitFileMaterializing(bak.FullName);
					}

					// Now remove destination and move source into place
					rm();                 // deletes dest; awaits vanishing if Cloud
					File.Move(src, dest); // System.IO: Move(source, destination)

					if (Cloud)
						return awaitFileVanishing(src) + awaitFileMaterializing(dest);

					return 0;
				}
			}

			// Destination does not exist (or replace==true but dest doesn't exist): simple move
			File.Move(src, dest);

			if (Cloud)
				return awaitFileVanishing(src) + awaitFileMaterializing(dest);

			return 0;
		}
		/// <summary>
		/// Copy a file in the file system.
		/// </summary>
		/// <param name="from">will be checked; exception will be thrown if file name does not match RsbFile form requirements</param>
		/// <returns>0 if everything went well</returns>
		public int cp(RaiFile from)
		{
			var src = from.FullName;
			var dest = FullName;

			if (src == dest) // make sure from and this do not point to the same file
				return 0;

			rm(); // make sure it's really gone before we go ahead; awaits Vanishing
			File.Copy(src, dest, true);  // overwrite if exists (which should never happen since we just removed it)
			#region double check if file has moved
			if (Cloud)
				return awaitFileMaterializing(dest);
			#endregion
			return 0;
		}
		public bool IsDirectory() => FullName.EndsWith(Os.DIRSEPERATOR);
		/// <summary>
		/// Change current working directory to the path in the RaiFile or the FullName if it is a directory
		/// </summary>
		public void cd()
		{
			Directory.SetCurrentDirectory(IsDirectory() ? FullName : Path);
		}
		public bool HasAbsolutePath()
		{
			if (string.IsNullOrEmpty(Path))
				return false;
			if (Path.Length > 0 && (Path[0] == '/' || Path[0] == '\\'))
				return true;
			if (Path.Length > 1 && Path[1] == ':')
				return true;
			return false;
		}
		/// <summary>
		/// Cloud Space files sometimes take their time to vanish
		/// </summary>
		/// <returns></returns>
		public int AwaitVanishing()
		{
			return awaitFileVanishing(FullName);
		}
		/// <summary>
		/// Cloud Space files sometimes take their time to materialize
		/// </summary>
		/// <param name="newFileOldName"></param>
		/// <returns></returns>
		public int AwaitMaterializing(bool newFileOldName = false)
		{
			return awaitFileMaterializing(FullName, newFileOldName);
		}
		/// <summary>
		/// Cloud Space directories sometimes take their time to materialize
		/// </summary>
		/// <param name="dirName"></param>
		/// <returns></returns>
		private static int awaitDirMaterializing(string dirName)
		{
			var count = 0;
			var exists = false;
			while (count < maxWaitCount)
			{
				try
				{
					exists = Directory.Exists(dirName);
				}
				catch (Exception) { }   // device not ready exception if Win 2003
				if (exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirMaterializing of dir " + dirName + ".");
			return -count;
		}
		/// <summary>
		/// Cloud Space directories sometimes take their time to vanish
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		/// <exception cref="DirectoryNotFoundException"></exception>
		private static int awaitDirVanishing(string path)
		{
			var count = 0;
			var exists = true;
			while (count < maxWaitCount)
			{
				try
				{
					exists = Directory.Exists(path);
				}
				catch (Exception) { }
				if (!exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirVanishing of dir " + path + ".");
			return -count;
		}
		private static int awaitFileMaterializing(string fileName, bool newFileOldName = false)
		{
			var count = 0;
			var done = false;
			var dt = new TimeSpan(0);
			while (count < maxWaitCount)
			{
				try
				{
					if (newFileOldName)
					{
						var info = new FileInfo(fileName);
						dt = DateTime.Now.Subtract(info.LastWriteTimeUtc);
						done = dt.TotalMilliseconds < 100;  // way too long - time the OS/FileSystem needs to update the FileInfo for a just rewritten file
					}
					else done = File.Exists(fileName);
				}
				catch (Exception) { }   // device not ready exception if Win 2003
				if (done)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new FileNotFoundException("ensure failed - timeout.", fileName);
			return -count;
		}
		private static int awaitFileVanishing(string fileName)
		{
			var count = 0;
			var exists = true;
			while (count < maxWaitCount)
			{
				try
				{
					exists = File.Exists(fileName);
				}
				catch (Exception) { }   // device not ready exception if Win 2003
				if (!exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new IOException("ensure failed - timeout in deleting " + fileName + ".");
			return -count;
		}
		/// <summary>
		/// Removes a directory; Path and its conventions is used to determine the directory that will be deleted
		/// </summary>
		/// <param name="depth">deletes up to depth levels of subdirectories</param>
		/// <param name="deleteFiles">when true, also deletes files in the target directory tree; if false, only deletes empty directories; trying to delete non-empty directories will throw an exception</param>
		public void rmdir(int depth = 0, bool deleteFiles = false)
		{
			if (Path == string.Empty)
				return;     // no directory given => nothing to delete
			if (Directory.EnumerateFileSystemEntries(Path).Any() && depth > 0)
			{
				if (deleteFiles)
				{
					foreach (var file in Directory.EnumerateFiles(Path))
						new RaiFile(file).rm();
				}
				foreach (var subdir in Directory.EnumerateDirectories(Path))
					new RaiFile(new RaiPath(subdir).Path).rmdir(depth - 1, deleteFiles);
			}
			Directory.Delete(Path); // directory may still be not empty here (if deleteFile == false) => throws exception
			if (Path.ToLower().Contains("dropbox"))
				awaitDirVanishing(Path);
		}
		/// <summary>
		/// assumes that Path points to a directory; check if it contains files
		/// </summary>
		/// <value>true if no files in this directory</value>
		public bool dirEmpty
		{
			get
			{
				return !Directory.EnumerateFileSystemEntries(Path).Any();
			}
		}
		/// <summary>
		/// Create a directory if it does not exist yet, using current Path
		/// </summary>
		/// <returns>DirectoryInfo</returns>
		public void mkdir()
		{
			mkdir(Path);
		}
		/// <summary>Create a directory if it does not exist yet</summary>
		/// <param name="dirname">if not given current directory is used</param>
		/// <returns>DirectoryInfo structure; contains properties Exists and CreationDate</returns>
		public static DirectoryInfo mkdir(string dirname)
		{
			dirname = string.IsNullOrEmpty(dirname) ? Directory.GetCurrentDirectory() : dirname;
			var dir = new DirectoryInfo(dirname);
			if (!dir.Exists)  // TODO problems with network drives, i.e. IservSetting.RemoteRootDir
			{
				dir = Directory.CreateDirectory(dirname);
				if (dirname.ToLower().Contains("dropbox"))
					awaitDirMaterializing(dirname);
			}
			return dir;
		}
		/// <summary>
		/// zip this file into archive
		/// </summary>
		/// <returns>the archive name</returns>
		public RaiFile Zip()
		{
			var inFolder = new RaiFile(this.FullName);
			var file = new RaiFile(this.FullName);
			inFolder.Name = file.Name;
			inFolder.Path = inFolder.Path + inFolder.Name;
			inFolder.mv(file);
			file.Ext = file.Ext + ".zip";
			file.rm();   // delete any pre-existing file
			try
			{
				ZipFile.CreateFromDirectory(inFolder.Path, file.FullName);
				//ZipFile.
			}
			catch (Exception)
			{
				return null;
			}
			return file;
		}
		/// <summary>
		/// copies the file on disk identified by the current RaiFile object to multiple destinations
		/// </summary>
		/// <param name="destDirs"></param>
		/// <returns></returns>
		public bool CopyTo(string[] destDirs)
		{
			try
			{
				RaiFile dest;
				foreach (var destDir in destDirs)
				{
					dest = new RaiFile(FullName)
					{
						Path = destDir
					};
					dest.mkdir();
					dest.cp(this);
				}
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}
		/// <summary>create a backup file</summary>
		/// <param name="copy">moves if false, copies otherwise</param>
		/// <returns>name of backupfile, if there was one created</returns>
		/// <remarks>the Os.LocalBackupDir will be used; make sure it's not in replicated cloud storage</remarks>
		public string backup(bool copy = false)
		{
			if (!File.Exists(FullName))
				return null;   // no file no backup
			var backupFile = new RaiFile(FullName);
			var idx = (backupFile.Path.Length > 2 && backupFile.Path[1] == ':') ? 3 : 0;     // works as expected for c:/123 or c:\123, but not for c:123
			var s = backupFile.Path.Substring(idx);
			backupFile.Path = (Os.LocalBackupDir + s).Replace("Dropbox/", "").Replace("dropbox/", "");   // preserves historical backup path shaping while starting from a local backup root
			mkdir(backupFile.Path);
			backupFile.Name = backupFile.Name + " " + DateTimeOffset.UtcNow.ToString(Os.DATEFORMAT);
			backupFile.Ext = Ext;
			if (copy)
				backupFile.cp(this);
			else backupFile.mv(this);
			return backupFile.FullName;
		}

		public string[] DirList
		{
			get => Path.Split(Os.DIRSEPERATOR, StringSplitOptions.RemoveEmptyEntries);
		}

		/// <summary>
		/// Constructor: auto-ensure mode for file systems that do not synchronously wait for the end of an IO operation i.e. Dropbox
		/// </summary>
		/// <remarks>only use the ensure mode if it has to be guaranteed that the IO operation was completely done
		/// when the method call returns; necessary e.g. for Dropbox directories since (currently) Dropbox first updates the
		/// file in the invisible . folder and then asynchronously updates the visible file and all the remote copies of it</remarks>
		/// <param name="filename"></param>
		public RaiFile(string filename)
		{
			// var lowercase = filename.ToLower();
			// Ensure = lowercase.Contains(".dropbox") ? false : lowercase.Contains("dropbox") || lowercase.Contains("onedrive") || lowercase.Contains("cloudstorage");	// sets Ensure also for files inside local .dropbox folder 
			path = string.Empty;
			name = string.Empty;
			ext = string.Empty;
			if (!string.IsNullOrEmpty(filename))
			{
				#region some unix conventions for convenience
				if (filename.StartsWith("~/"))
					filename = $"{Os.HomeDir}{filename.Substring(1)}";
				else if (filename.StartsWith("./"))
					filename = $"{Directory.GetCurrentDirectory()}{filename.Substring(1)}";
				else if (filename.StartsWith("../"))
				{
					var dir = Directory.GetCurrentDirectory();
					while (filename.StartsWith("../"))
					{
						dir = new RaiFile(dir.TrimEnd('/')).Path;   // one up
						filename = filename.Substring(3);           // remove first ../
					}
					filename = $"{dir}{filename}";
				}
				#endregion
				filename = Os.NormSeperator(filename);
				var k = filename.LastIndexOf(Os.DIRSEPERATOR);
				if (k >= 0)
				{
					path = filename.Substring(0, k + 1);
					Name = filename.Substring(k + 1);
				}
				else Name = filename;   // also takes care of ext
			}
			UpdateCloudFlag();
		}
	}
} //namespace OsLib
