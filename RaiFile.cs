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
using System.Runtime.InteropServices;
/*
 * based on RsbFile (C++ version from 1991, C# version 2005)
 */
namespace OsLib
{
	/// <summary>
	/// Convenience extensions for string and CSV handling.
	/// </summary>
	public static class RaiFileExtensions
	{
		public static List<string> MakePolicyCompliant(this List<string> lines, bool tabbed = true)
		{
			var buffer = new List<string>();
			foreach (var line in lines)
			{
				var multi = line.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
				foreach (var one in multi)
				{
					if (tabbed) buffer.Add(one.Replace("    ", "\t").Replace("  ", "\t").Singularize(' ').Singularize('\t').Replace("\t ", "\t"));
					else buffer.Add(one.Singularize(' '));
				}
			}
			var compliant = new List<string>();
			foreach (var line in buffer) if (line.Length > 0) compliant.Add(line);
			return compliant;
		}
		public static string Singularize(this string line, char c, bool trim = true)
		{
			if (line.Length == 0) return line;
			var result = new List<char> { line[0] };
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
		public static List<Dictionary<string, string>> CreateDictionariesFromCsvLines(this string lines, bool tabbed = true)
		{
			var tab = new List<string> { lines }.MakePolicyCompliant(tabbed: true);
			var keys = tab[0].Split(new[] { '\t' });
			var list = new List<Dictionary<string, string>>();
			for (int i = 1; i < tab.Count; i++)
			{
				var v = tab[i].Split(new[] { '\t' });
				var dict = new Dictionary<string, string>();
				for (int j = 0; j < keys.Length; j++)
				{
					if (v.Length < j) throw new FieldAccessException($"csv input - out of bounds for index {j}; field list: {string.Join(',', keys)}");
					dict.Add(keys[j], v[j]);
				}
				list.Add(dict);
			}
			return list;
		}
	}
	public class RaiFile
	{
		private const int maxWaitCount = 60;
		protected string name;
		public bool Cloud;
		public virtual string Name
		{
			get => string.IsNullOrEmpty(name) ? string.Empty : name;
			set
			{
				(_, string n) = RaiPath.splitPathAndName(value);
				NameAndExt = (n, string.Empty);
			}
		}
		/// <summary>
		/// sets Name and Ext and returns them as a tuple; formerly splitNameAndExt
		/// sets Ext only if it was not set before AND found in the nameWithExt parameter
		/// Name will be set accordingly
		/// </summary>
		public (string Name, string Ext) NameAndExt
		{
			get => (Name, Ext);
			set
			{
				var (nameWithExt, extensionOrEmpty) = value;
				(string _name, string _ext) = (string.Empty, ext);
				if (string.IsNullOrEmpty(nameWithExt))
					(_name, _ext) = (string.Empty, string.Empty);
				else if (!string.IsNullOrEmpty(extensionOrEmpty))
					(_name, _ext) = (nameWithExt, extensionOrEmpty);
				else {
					if (string.IsNullOrEmpty(_ext)) {
						int pos = nameWithExt.LastIndexOf('.');
						if (pos >= 0)
							(_name, _ext) = (nameWithExt.Substring(0, pos), nameWithExt.Substring(pos + 1));
						else _name = nameWithExt;
					}
					else _name = nameWithExt;
				}
				(name, ext) = (_name, _ext);
			}
		}
		public virtual string NameWithExtension => (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(ext)) ? string.Empty : Name + (string.IsNullOrEmpty(ext) ? string.Empty : "." + ext);
		private string ext;
		public string Ext
		{
			get => ext;
			set => ext = value;
		}
		private RaiPath path = null;
		public virtual RaiPath Path
		{
			get => path;
			set
			{
				if (value == null) { path = null; Cloud = false; }
				else { path = value; Cloud = value.Cloud; }
			}
		}
		public virtual string FullName => (Path?.FullPath ?? string.Empty) + NameWithExtension;
		public override string ToString() => FullName;
		public bool Exists() => File.Exists(FullName);
		public int rm()
		{
			if (!File.Exists(FullName)) return 0;
			File.Delete(FullName);
			if (this.Cloud) return this.awaitVanishing();
			return 0;
		}
		public int mv(RaiFile from) => mv(from, replace: true, keepBackup: false);
		public int mv(RaiFile from, bool replace) => mv(from, replace, keepBackup: false);
		public int mv(RaiFile from, bool replace, bool keepBackup)
		{
			var dest = FullName;
			var src = from.FullName;
			if (src == dest) return 0;
			mkdir();
			if (!File.Exists(src)) throw new FileNotFoundException("Source file does not exist: " + src);
			var destExists = File.Exists(dest);
			if (destExists && !replace) throw new IOException("Destination file already exists: " + dest);
			if (destExists && replace)
			{
				var bak = new RaiFile(dest) { Ext = "bak" };
				try
				{
					if (bak.Exists()) bak.rm();
					File.Replace(src, dest, bak.FullName, ignoreMetadataErrors: true);
					if (!keepBackup) bak.rm();
					return 0;
				}
				catch (IOException)
				{
					if (keepBackup && File.Exists(dest)) { if (bak.Exists()) bak.rm(); File.Copy(dest, bak.FullName, overwrite: true); }
					rm();
					File.Move(src, dest);
					return 0;
				}
			}
			File.Move(src, dest);
			return 0;
		}
		public int cp(RaiFile from)
		{
			if (from.FullName == FullName) return 0;
			rm();
			File.Copy(from.FullName, FullName, true);
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
		/// Default propagation delay (ms) for <see cref="BackdateCreationTime"/>.
		/// Can be overridden per-call or globally via <c>Os.Config.SyncPropagationDelayMs</c>
		/// in the RAIkeep.json5 config file.
		/// </summary>
		public static int DefaultSyncPropagationDelayMs { get; set; } = 10_000;
		/// <summary>
		/// Backdates the file's <see cref="FileAge"/> (CreationTimeUtc) and waits for
		/// cloud sync providers to propagate the change.
		/// <para>
		/// A sentinel file is written next to the target file before the wait begins
		/// and deleted after the delay.  This nudges cloud sync providers (OneDrive,
		/// Dropbox, rclone/GDrive) to pick up the metadata change promptly.
		/// </para>
		/// <para>
		/// The delay is read from <c>Os.Config.SyncPropagationDelayMs</c> if present,
		/// otherwise falls back to <see cref="DefaultSyncPropagationDelayMs"/> (10 s).
		/// Pass an explicit <paramref name="propagationDelayMs"/> to override both.
		/// </para>
		/// </summary>
		/// <param name="utc">The backdated CreationTimeUtc to set.</param>
		/// <param name="propagationDelayMs">
		/// Override delay in milliseconds.  When null, uses the config value or
		/// <see cref="DefaultSyncPropagationDelayMs"/>.
		/// </param>
		public void BackdateCreationTime(DateTime utc, int? propagationDelayMs = null)
		{
			if (!Exists())
				throw new FileNotFoundException($"Cannot backdate: file does not exist: {FullName}");
			File.SetCreationTimeUtc(FullName, utc);
			// Write a sentinel file to trigger a cloud sync event
			var sentinelName = $"{Name}.backdate.tmp";
			var sentinel = new RaiFile(Path, sentinelName);
			try
			{
				File.WriteAllText(sentinel.FullName, utc.ToString("o"));
			}
			catch { /* best effort — sentinel is optional */ }
			// Wait for propagation
			int delay = propagationDelayMs
				?? (int?)(Os.Config?.SyncPropagationDelayMs)
				?? DefaultSyncPropagationDelayMs;
			if (delay > 0) Thread.Sleep(delay);
			// Clean up sentinel
			try
			{
				if (File.Exists(sentinel.FullName))
					File.Delete(sentinel.FullName);
			}
			catch { /* best effort */ }
		}
		public void cd() => Directory.SetCurrentDirectory(path.ToString());
		public bool HasAbsolutePath()
		{
			if (Path == null) return false;
			var p = path.ToString();
			if (string.IsNullOrEmpty(p)) return false;
			if (Os.IsWindows)
			{
				if (p.StartsWith(Os.DIR)) return true;
				if (p.Length > 2 && p[1] == ':' && p.Substring(2).StartsWith(Os.DIR)) return true;
			}
			else if (p.StartsWith(Os.DIR)) return true;
			return false;
		}
		public int AwaitVanishing() => awaitVanishing();
		public int AwaitMaterializing(bool newFileOldName = false) => awaitMaterializing(newFileOldName);
		private int awaitMaterializing(bool newFileOldName = false)
		{
			var count = 0;
			var done = false;
			while (count < maxWaitCount)
			{
				try
				{
					if (newFileOldName)
					{
						var info = new FileInfo(FullName);
						done = DateTime.Now.Subtract(info.LastWriteTimeUtc).TotalMilliseconds < 100;
					}
					else done = File.Exists(FullName);
				}
				catch { }
				if (done) break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount) throw new FileNotFoundException("ensure failed - timeout.", FullName);
			return -count;
		}
		private int awaitVanishing()
		{
			var count = 0;
			var exists = true;
			while (count < maxWaitCount)
			{
				try { exists = File.Exists(FullName); }
				catch { }
				if (!exists) break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount) throw new IOException("ensure failed - timeout in deleting " + FullName);
			return -count;
		}
		public void rmdir(int depth = 0, bool deleteFiles = false) => Path?.rmdir(depth, deleteFiles);
		public bool DirEmpty => string.IsNullOrEmpty(Path?.ToString()) || !Directory.EnumerateFileSystemEntries(Path.ToString()).Any();
		public RaiPath mkdir() => Path?.mkdir() ?? RaiPath.mkdir();
		public static RaiPath mkdir(string dirname = null) => RaiPath.mkdir(dirname);
		public RaiFile Zip()
		{
			var zipFile = new RaiFile(FullName);
			if (!string.IsNullOrWhiteSpace(zipFile.Ext)) zipFile.Name = zipFile.Name + "." + zipFile.Ext;
			zipFile.Ext = "zip";
			zipFile.rm();
			try
			{
				using var archive = ZipFile.Open(zipFile.FullName, ZipArchiveMode.Create);
				archive.CreateEntryFromFile(FullName, NameWithExtension, CompressionLevel.Optimal);
			}
			catch { return null; }
			return zipFile;
		}
		public bool CopyTo(List<RaiPath> destDirs)
		{
			try
			{
				foreach (var destDir in destDirs)
				{
					var dest = new RaiFile(destDir, name: Name, ext: Ext);
					dest.mkdir();
					dest.cp(this);
				}
			}
			catch { return false; }
			return true;
		}
		public RaiFile backup(bool copy = false)
		{
			if (!File.Exists(FullName)) return null;
			var backupRoot = Os.LocalBackupDir ?? throw new InvalidOperationException("LocalBackupDir not configured.");
			var backupFile = new RaiFile(FullName);
			backupFile.Path = backupRoot / BackupRelativePath(backupFile.Path);
			backupFile.mkdir();
			backupFile.Name = backupFile.Name + "_" + DateTimeOffset.UtcNow.ToString(Os.DATEFORMAT);
			backupFile.Ext = Ext;
			if (copy) backupFile.cp(this);
			else backupFile.mv(this);
			return backupFile;
		}
		internal static RaiRelPath BackupRelativePath(RaiPath sourceDirectoryPath)
		{
			var sourcePath = sourceDirectoryPath?.Path;
			if (string.IsNullOrWhiteSpace(sourcePath)) return new RaiRelPath();
			if (Os.IsWindows && sourcePath.Length > 2 && sourcePath[1] == ':') return new RaiRelPath(sourcePath.Substring(3));
			var stripped = Os.NormSeperator(sourcePath).TrimStart(Os.DIR[0]);
			return string.IsNullOrWhiteSpace(stripped) ? new RaiRelPath() : new RaiRelPath(stripped);
		}
		public string[] DirList => path.ToString().Split(new[] { Os.DIR }, StringSplitOptions.RemoveEmptyEntries);
		public RaiFile(string filename) : this(RaiPath.SplitRaiPathAndName(filename)) { }
		private RaiFile((RaiPath path, string name) parsed) : this(parsed.path, parsed.name, null) { }
		public RaiFile(RaiPath p, string name = null, string ext = null)
		{
			Path = p ?? new RaiPath(string.Empty);
			NameAndExt = (name, ext);
		}
	}
}