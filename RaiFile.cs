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
using System.Reflection.PortableExecutable;
/*
 *	based on RsbFile (C++ version from 1991, C# version 2005)
 */
namespace OsLib
{
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
	/// File and directory utility with path parsing and cloud-aware behaviors.
	/// </summary>
	public class RaiFile
	{
		const int maxWaitCount = 60;  // raised from 15 as a result of a failed test case: TestComparePic9AndPic7ZoomTrees, 2012-12-23, RSB.
									  // raised from 20 as a result of a failed test case: TestUserRoleSubscriberAccess, Pic8, 2014-03-03, RSB.
									  // raised from 25 as a result of a failed test runs on Pic8 (which probably has a slow disk compared to other servers), 2014-03-16, RSB.
		protected string name;
		public bool Cloud;
		/// <summary>
		/// // without dir structure and without extension
		/// </summary>				
		public virtual string Name
		{
			get { return string.IsNullOrEmpty(name) ? string.Empty : name; }
			set // sets name and ext; override to set more name components
			{
				(_, string n) = RaiPath.SplitPathAndName(value);
				(name, ext) = splitNameAndExt(n, ext);
			}
		}
		private (string n, string e) splitNameAndExt(string nameWithExt, string ext)
		{
			if (!string.IsNullOrEmpty(ext))
				return (nameWithExt, ext);  // e.g. ("otw.software", "txt")
			var pos = nameWithExt.LastIndexOf(".");
			if (pos >= 0)
			{
				var n = nameWithExt.Substring(0, pos);
				var e = nameWithExt.Substring(pos + 1);
				return (n, e);
			}
			return (nameWithExt, string.Empty);
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
		private RaiPath path = null;                // the source directory of the picture, ends with a dirSeperator
		/// <summary>
		/// the source directory of the file, ends with a dirSeperator; Ensure will be set to memorize if the file is in the cloud
		/// </summary>
		public virtual RaiPath Path
		{
			get { return path; }
			set
			{
				if (value == null)
				{
					path = null;
					Cloud = false;
				}
				else
				{
					path = value;
					UpdateCloudFlag();
				}
			}
		}
		private void UpdateCloudFlag()
		{
			Cloud = path != null && Os.IsCloudPath(path.ToString());
		}
		public virtual string FullName
		{
			get { return Path + NameWithExtension; }
		}
		public override string ToString()
		{
			return FullName;
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
		public bool IsDirectory() => FullName.EndsWith(Os.DIR);
		public TimeSpan FileAge
		{
			get
			{
				var info = new System.IO.FileInfo(FullName);
				return DateTimeOffset.UtcNow - info.CreationTimeUtc;
			}
		}
		/// <summary>
		/// Change current working directory to the path in the RaiFile or the FullName if it is a directory
		/// </summary>
		public void cd()
		{
			Directory.SetCurrentDirectory(path.ToString()); //IsDirectory() ? FullName : Path);
		}
		public bool HasAbsolutePath()
		{
			if (Path == null)
				return false;
			var p = path.ToString();
			if (string.IsNullOrEmpty(p))	
				return false;
			if (Os.IsWindows)
			{
				if (p[0] == '\\')
					return true;
				if (p.Length > 2 && p[1] == ':' && p[2] == '\\')
					return true;
			}
			else {
				if (p[0] == '/')
					return true;
			}
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
		/// <param name="depth">deletes up to depth levels of subdirectories, one file and one dir at a time; only waits for the root to vanish</param>
		/// <param name="deleteFiles">when true, also deletes files in the target directory tree; if false, only deletes empty directories; trying to delete non-empty directories will throw an exception</param>
		public void rmdir(int depth = 0, bool deleteFiles = false)
		{
			Path?.rmdir(depth, deleteFiles);
		}
		/// <summary>
		/// assumes that Path points to a directory; check if it contains files
		/// </summary>
		/// <value>true if no files in this directory</value>
		public bool dirEmpty
		{
			get
			{
				var directoryPath = Path?.ToString();
				return string.IsNullOrEmpty(directoryPath) || !Directory.EnumerateFileSystemEntries(directoryPath).Any();
			}
		}
		/// <summary>
		/// Create a directory if it does not exist yet, using current Path
		/// </summary>
		/// <returns>DirectoryInfo</returns>
		public RaiPath mkdir()
		{
			return Path?.mkdir() ?? RaiPath.mkdir();
		}
		/// <summary>Create a directory if it does not exist yet</summary>
		/// <param name="dirname">if not given current directory is used</param>
		/// <returns>created or existing directory as RaiPath</returns>
		public static RaiPath mkdir(string dirname = null)
		{
			return RaiPath.mkdir(dirname);
		}
		/// <summary>
		/// zip this file into archive
		/// </summary>
		/// <returns>the archive as a RaiFile</returns>
		public RaiFile Zip()
		{
			var zipFile = new RaiFile(FullName);
			if (!string.IsNullOrWhiteSpace(zipFile.Ext))
				zipFile.Name = zipFile.Name + "." + zipFile.Ext;
			zipFile.Ext = "zip";
			zipFile.rm();
			try
			{
				using var archive = ZipFile.Open(zipFile.FullName, ZipArchiveMode.Create);
				archive.CreateEntryFromFile(FullName, NameWithExtension, CompressionLevel.Optimal);
			}
			catch (Exception)
			{
				return null;
			}
			return zipFile;
		}
		/// <summary>
		/// copies the file on disk identified by the current RaiFile object to multiple destinations given by a List of RaiPath objects
		/// </summary>
		/// <param name="destDirs"></param>
		/// <returns></returns>
		public bool CopyTo(List<RaiPath> destDirs)
		{
			try
			{
				RaiFile dest;
				foreach (var destDir in destDirs)
				{
					dest = new RaiFile(destDir, name: Name, ext: Ext);
					dest.mkdir();
					dest.cp(this);	// copy: dest <= this
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
		/// <returns>name of backupfile is the same as the original file plus a timestamp; same Ext</returns>
		/// <remarks>the Os.LocalBackupDir will be used</remarks>
		public RaiFile backup(bool copy = false)
		{
			if (!File.Exists(FullName))
				return null;   // no file no backup
			var backupRoot = Os.LocalBackupDir;
			if (backupRoot == null)
				throw new InvalidOperationException($"Local backup is disabled because LocalBackupDir is not configured or is not usable. {Os.GetCloudStorageSetupGuidance()}");
			var backupFile = new RaiFile(FullName);
			backupFile.Path = backupRoot / BackupRelativePath(backupFile.Path);
			backupFile.mkdir();
			backupFile.Name = backupFile.Name + "_" + DateTimeOffset.UtcNow.ToString(Os.DATEFORMAT);
			backupFile.Ext = Ext;
			if (copy)
				backupFile.cp(this);
			else backupFile.mv(this);
			return backupFile;
		}
		/// <summary>
		/// Returns the relative directory tail that gets appended under Os.LocalBackupDir
		/// during backup. Cloud-backed paths are made relative to their configured cloud
		/// root; local absolute paths have their machine-specific root stripped.
		/// </summary>
		internal static RaiRelPath BackupRelativePath(RaiPath sourceDirectoryPath)
		{
			var sourcePath = sourceDirectoryPath?.Path;
			if (string.IsNullOrWhiteSpace(sourcePath))
				return new RaiRelPath();
			foreach (Cloud provider in Enum.GetValues(typeof(Cloud)))
			{
				var cloudRoot = Os.GetCloudStorageRoot(provider)?.Path;
				if (string.IsNullOrWhiteSpace(cloudRoot))
					continue;
				if (!sourcePath.StartsWith(cloudRoot, StringComparison.OrdinalIgnoreCase))
					continue;
				var relative = sourcePath.Substring(cloudRoot.Length)
					.TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
				return string.IsNullOrWhiteSpace(relative)
					? new RaiRelPath()
					: new RaiRelPath(relative);
			}
			if (Os.IsWindows && sourcePath.Length > 2 && sourcePath[1] == ':')
				return new RaiRelPath(sourcePath.Substring(3));
			var stripped = sourcePath.TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
			return string.IsNullOrWhiteSpace(stripped)
				? new RaiRelPath()
				: new RaiRelPath(stripped);
		}
		public string[] DirList
		{
			get => path.ToString().Split(Os.DIR, StringSplitOptions.RemoveEmptyEntries);
		}
		/// <summary>
		/// Constructor: auto-ensure mode for file systems that do not synchronously wait for the end of an IO operation i.e. Dropbox
		/// </summary>
		/// <remarks>only use the ensure mode if it has to be guaranteed that the IO operation was completely done
		/// when the method call returns; necessary e.g. for Dropbox directories since (currently) Dropbox first updates the
		/// file in the invisible . folder and then asynchronously updates the visible file and all the remote copies of it</remarks>
		/// <param name="filename"></param>
		public RaiFile(string filename) : this(RaiPath.SplitRaiPathAndName(filename))
		{
		}
		private RaiFile((RaiPath path, string name) parsed) : this(parsed.path, parsed.name, null)
		{
		}
		/// <summary>
		/// Constructor that takes a RaiPath and optional name and extension to build the full file path.
		/// </summary>
		/// <param name="p"></param>
		/// <param name="name">null or "test" or "test.txt" or "otw.software.txt</param>
		/// <param name="ext">null or "txt"</param>
		public RaiFile(RaiPath p, string name = null, string ext = null)
		{
			Path = p ?? new RaiPath(string.Empty);
			(this.name, this.ext) = splitNameAndExt(name, ext);
		}
	}
} //namespace OsLib
