using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace OsLib
{
	/// <summary>
	/// Shared base for directory path types (absolute and relative).
	/// Holds the normalized path string and provides common read-only accessors.
	/// </summary>
	public abstract class RaiBasePath
	{
		public override string ToString() => FullPath;
		/// <summary>
		/// The full path as a string. Always ends with a directory separator when non-empty.
		/// For RaiPath this is the resolved absolute path; for RaiRelPath the normalized
		/// relative path; for ItemTreePath the composed Root+Topdir+Subdir path.
		/// Symmetric with RaiFile.FullName.
		/// </summary>
		public virtual string FullPath => path;
		protected string path = string.Empty;
		/// <summary>
		/// True when this path holds no segments (empty string).
		/// </summary>
		public bool IsEmpty => string.IsNullOrEmpty(path);
		/// <summary>
		/// The individual directory names that make up this path,
		/// split on the platform separator with empty entries removed.
		/// </summary>
		public string[] Segments =>
			string.IsNullOrEmpty(path)
				? Array.Empty<string>()
				: path.Split(Os.DIR[0], StringSplitOptions.RemoveEmptyEntries);
		/// <summary>
		/// Number of directory segments in this path.
		/// </summary>
		public int Depth => Segments.Length;
		protected static bool isAbsoluteLike(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return false;
			var normalized = Os.NormSeperator(value);
			if (normalized == "~" || normalized.StartsWith("~" + Os.DIR, StringComparison.Ordinal)) return true;
			if (normalized.StartsWith(Os.DIR, StringComparison.Ordinal)) return true;
			return normalized.Length > 1 && normalized[1] == ':';
		}
	}
	/// <summary>
	/// Represents a relative directory path. Never resolves to absolute.
	/// Enforces a trailing directory separator when non-empty.
	/// No filesystem operations: a relative path is not actionable until
	/// anchored to a RaiPath via the / operator.
	/// </summary>
	public class RaiRelPath : RaiBasePath
	{
		/// <summary>
		/// The relative directory path string.
		/// Setting this treats the entire input as a directory
		/// (adds trailing separator, rejects absolute paths).
		/// </summary>
		public string Path
		{
			get => path;
			set => path = normalize(value);
		}
		/// <summary>
		/// Normalize a string into a relative directory path.
		/// Strips leading "./", rejects absolute-looking input,
		/// and ensures a trailing directory separator.
		/// </summary>
		internal static string normalize(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return string.Empty;
			var trimmed = Os.NormSeperator(value.Trim());
			if (trimmed == ".") return string.Empty;
			if (trimmed.StartsWith("." + Os.DIR, StringComparison.Ordinal))
				trimmed = trimmed.Substring(1 + Os.DIR.Length);
			if (string.IsNullOrEmpty(trimmed)) return string.Empty;
			if (isAbsoluteLike(trimmed)) throw new ArgumentException("RaiRelPath requires a relative path.", nameof(value));
			return Os.ensureTrailingDirSeparator(trimmed);
		}
		/// <summary>
		/// Split a relative path-and-name string into the directory portion
		/// and the trailing name portion.
		/// "sub/dir/file.txt" -> (path: "sub/dir/", name: "file.txt")
		/// "sub/dir/"         -> (path: "sub/dir/", name: "")
		/// "file.txt"         -> (path: "",          name: "file.txt")
		/// </summary>
		internal static (string path, string name) splitPathAndName(string pathAndName)
		{
			if (string.IsNullOrWhiteSpace(pathAndName)) return (string.Empty, string.Empty);
			var normalized = Os.NormSeperator(pathAndName.Trim());
			if (normalized == ".") return (string.Empty, string.Empty);
			if (normalized.StartsWith("." + Os.DIR, StringComparison.Ordinal))
				normalized = normalized.Substring(1 + Os.DIR.Length);
			if (string.IsNullOrEmpty(normalized)) return (string.Empty, string.Empty);
			if (isAbsoluteLike(normalized)) throw new ArgumentException("RaiRelPath requires a relative path.", nameof(pathAndName));
			if (normalized.EndsWith(Os.DIR, StringComparison.Ordinal)) return (Os.ensureTrailingDirSeparator(normalized), string.Empty);
			var pos = normalized.LastIndexOf(Os.DIR[0]);
			if (pos < 0) return (string.Empty, normalized);
			return (Os.ensureTrailingDirSeparator(normalized.Remove(pos + 1)), normalized.Substring(pos + 1));
		}
		public static (RaiRelPath path, string name) SplitRaiRelPathAndName(string pathAndName)
		{
			(string p, string n) = splitPathAndName(pathAndName);
			return (new RaiRelPath(p), n);
		}
		/// <summary>
		/// Append a subdirectory segment to this relative path.
		/// </summary>
		public static RaiRelPath operator /(RaiRelPath self, string subDir)
		{
			if (self == null) throw new ArgumentNullException(nameof(self));
			if (string.IsNullOrWhiteSpace(subDir)) return new RaiRelPath(self.Path);
			return self / new RaiRelPath(subDir);
		}
		/// <summary>
		/// Concatenate two relative paths.
		/// </summary>
		public static RaiRelPath operator /(RaiRelPath self, RaiRelPath other)
		{
			if (self == null) throw new ArgumentNullException(nameof(self));
			if (other == null) throw new ArgumentNullException(nameof(other));
			return new RaiRelPath(self.Path + other.Path);
		}
		/// <summary>
		/// Construct a relative directory path from a string.
		/// The entire input is treated as a directory (trailing separator added).
		/// Pass "" or omit for an empty (zero-segment) relative path.
		/// </summary>
		public RaiRelPath(string s = "")
		{
			Path = s;
		}
		/// <summary>
		/// Copy constructor: creates a new RaiRelPath from an existing one.
		/// </summary>
		public RaiRelPath(RaiRelPath other)
		{
			path = other?.path ?? string.Empty;
		}
		/// <summary>
		/// Construct a RaiRelPath from any RaiBasePath.
		/// The path string is taken as-is; throws if it looks absolute.
		/// </summary>
		public RaiRelPath(RaiBasePath other)
		{
			Path = other?.FullPath ?? string.Empty;
		}
	}
	/// <summary>
	/// Represents an absolute directory path. Always resolves to a full path
	/// via System.IO.Path.GetFullPath and enforces a trailing directory separator.
	/// Supports the / operator for appending subdirectories.
	/// </summary>
	public class RaiPath : RaiBasePath
	{
		private const int maxWaitCount = 60;
		public bool Cloud { get; private set; }
		/// <summary>
		/// The absolute directory path string.
		/// On set, the value is resolved to an absolute path and any trailing
		/// filename segment is stripped (use RaiFile for files).
		/// </summary>
		public string Path
		{
			get => path;
			set
			{
				var dirName = Os.ensureTrailingDirSeparator(value);
				(path, _) = splitPathAndName(dirName);
				Cloud = Os.IsCloudPath(path);
			}
		}
		internal static (string path, string name) splitPathAndName(string pathAndName)
		{
			if (string.IsNullOrWhiteSpace(pathAndName)) return (string.Empty, string.Empty);
			pathAndName = Os.NormSeperator(pathAndName);
			string n, p;
			if (pathAndName == "~") return (Os.ensureTrailingDirSeparator(Os.UserHomeDir.Path), string.Empty);
			var homePrefix = "~" + Os.DIR;
			if (pathAndName.StartsWith(homePrefix, StringComparison.Ordinal))
				p = Os.UserHomeDir.Path + pathAndName.Substring(homePrefix.Length);
			else
			{
				p = System.IO.Path.GetFullPath(pathAndName);
				if (pathAndName == ".") return (Os.ensureTrailingDirSeparator(p), string.Empty);
			}
			int pos = p.LastIndexOf(Os.DIR, StringComparison.Ordinal);
			if (pos < 0)
			{
				n = p;
				p = string.Empty;
			}
			else
			{
				n = p.Substring(pos + 1);
				p = Os.ensureTrailingDirSeparator(p.Remove(pos + 1));
			}
			return (p, n);
		}
		public static (RaiPath path, string name) SplitRaiPathAndName(string filename)
		{
			(string p, string n) = splitPathAndName(filename);
			return (new RaiPath(p), n);
		}
		/// <summary>
		/// Append a subdirectory name to this absolute path.
		/// The string is wrapped into a RaiRelPath before combining.
		/// </summary>
		public static RaiPath operator /(RaiPath self, string subDir)
		{
			if (self == null) throw new ArgumentNullException(nameof(self));
			if (string.IsNullOrWhiteSpace(subDir)) return new RaiPath(self.Path);
			return self / new RaiRelPath(subDir);
		}
		/// <summary>
		/// Anchor a relative path onto this absolute path,
		/// producing a new absolute RaiPath.
		/// </summary>
		public static RaiPath operator /(RaiPath self, RaiRelPath subDir)
		{
			if (self == null) throw new ArgumentNullException(nameof(self));
			if (subDir == null || subDir.IsEmpty) return new RaiPath(self.Path);
			return new RaiPath(self.Path + subDir.Path);
		}
		public IEnumerable<RaiFile> EnumerateFiles(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			foreach (var file in Directory.EnumerateFiles(Path, searchPattern, searchOption))
			{
				yield return new RaiFile(file);
			}
		}
		public IEnumerable<RaiPath> EnumerateDirectories(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			foreach (var dir in Directory.EnumerateDirectories(Path, searchPattern, searchOption))
			{
				yield return new RaiPath(dir);
			}
		}
		/// <summary>
		/// Constructor that takes a string path; the caller knows that this is a directory path.
		/// It does not have to exist yet in the file system.
		/// "." and "~" are expanded to their absolute equivalents.
		/// If the string does not end with a directory separator the last segment is stripped.
		/// </summary>
		public RaiPath(string s = ".")
		{
			Path = s;
		}
		/// <summary>
		/// Copy constructor: creates a new RaiPath from an existing one.
		/// </summary>
		public RaiPath(RaiPath other)
		{
			path = other?.path ?? string.Empty;
			Cloud = other?.Cloud ?? false;
		}
		/// <summary>
		/// Construct a RaiPath from any RaiBasePath.
		/// The path string is resolved to an absolute path.
		/// </summary>
		public RaiPath(RaiBasePath other) : this(other?.FullPath ?? string.Empty)
		{
		}
		/// <summary>
		/// Constructor that takes a RaiFile object; uses its Path and ignores Name and Ext.
		/// </summary>
		public RaiPath(RaiFile f)
		{
			Path = f?.Path?.ToString() ?? string.Empty;
		}
		public bool Exists() => Directory.Exists(Path);
		/// <summary>
		/// The parent directory of this path, i.e. one level up.
		/// Returns a RaiPath pointing to the parent, or this path
		/// itself when already at the filesystem root.
		/// </summary>
		public RaiPath Parent
		{
			get
			{
				var parent = Os.parentDir(path);
				return string.IsNullOrEmpty(parent) ? this : new RaiPath(parent);
			}
		}
		private int awaitDirMaterializing()
		{
			var count = 0;
			var exists = false;
			while (count < maxWaitCount)
			{
				try { exists = Exists(); }
				catch (Exception) { }
				if (exists) break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount) throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirMaterializing of dir " + path + ".");
			return -count;
		}
		private int awaitDirVanishing()
		{
			var count = 0;
			var exists = true;
			while (count < maxWaitCount)
			{
				try { exists = Exists(); }
				catch (Exception) { }
				if (!exists) break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount) throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirVanishing of dir " + path + ".");
			return -count;
		}
		public RaiPath mkdir() => mkdir(Path);
		public static RaiPath mkdir(string dirname = null)
		{
			var path = new RaiPath(dirname);
			if (!path.Exists())
			{
				Directory.CreateDirectory(path.Path);
				if (path.Cloud) path.awaitDirMaterializing();
			}
			return path;
		}
		/// <summary>
		/// Move a directory from the location given by <paramref name="from"/> to the
		/// location of the current RaiPath instance.
		/// If the current RaiPath already exists, behavior depends on
		/// <paramref name="replace"/>: throws when false, replaces otherwise.
		/// When <paramref name="keepBackup"/> is true and the existing target is being
		/// replaced, the existing target is first moved to <see cref="Os.LocalBackupDir"/>
		/// via <see cref="backup"/> instead of being deleted.
		/// Returns 0 on success.
		/// </summary>
		public int mv(RaiPath from, bool replace, bool keepBackup)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (!from.Exists()) throw new DirectoryNotFoundException("Source directory does not exist: " + from.Path);
			if (Exists())
			{
				if (!replace) throw new IOException("Target directory already exists: " + Path);
				if (keepBackup) backup(copy: false);
				else
				{
					rmdir(depth: int.MaxValue, deleteFiles: true);
					if (Cloud) awaitDirVanishing();
				}
			}
			Directory.Move(from.Path, Path);
			if (Cloud) awaitDirMaterializing();
			return 0; // success
		}
		/// <summary>
		/// Copy a directory tree from the location given by <paramref name="from"/> to
		/// the location of the current RaiPath instance, including all files and
		/// subdirectories. Implemented in terms of <see cref="RaiPath.EnumerateFiles"/>,
		/// <see cref="RaiPath.EnumerateDirectories"/>, <see cref="RaiPath.mkdir()"/> and
		/// <see cref="RaiFile.cp"/> — no direct System.IO calls for traversal.
		/// If the current RaiPath already exists, behavior depends on
		/// <paramref name="replace"/>: throws when false, replaces otherwise.
		/// When <paramref name="keepBackup"/> is true and the existing target is being
		/// replaced, the existing target is first moved to <see cref="Os.LocalBackupDir"/>
		/// via <see cref="backup"/>.
		/// Returns 0 on success.
		/// </summary>
		public int cp(RaiPath from, bool replace, bool keepBackup = false)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (!from.Exists()) throw new DirectoryNotFoundException("Source directory does not exist: " + from.Path);
			if (Exists())
			{
				if (!replace) throw new IOException("Target directory already exists: " + Path);
				if (keepBackup) backup(copy: false);
				else
				{
					rmdir(depth: int.MaxValue, deleteFiles: true);
					if (Cloud) awaitDirVanishing();
				}
			}
			mkdir();
			foreach (var srcFile in from.EnumerateFiles("*").ToList())
			{
				var destFile = new RaiFile(this, srcFile.Name, srcFile.Ext);
				destFile.cp(srcFile);
			}
			foreach (var srcSub in from.EnumerateDirectories("*").ToList())
			{
				var leaf = srcSub.Segments.LastOrDefault();
				if (string.IsNullOrEmpty(leaf)) continue;
				var destSub = this / leaf;
				destSub.cp(srcSub, replace: true, keepBackup: false);
			}
			if (Cloud) awaitDirMaterializing();
			return 0; // success
		}
		/// <summary>
		/// Symmetrical to <see cref="RaiFile.backup"/>: relocates (or copies) this
		/// directory tree into <see cref="Os.LocalBackupDir"/>, mirroring the original
		/// path under that root and appending a UTC timestamp to the leaf segment.
		/// When <paramref name="copy"/> is true the original is left in place;
		/// otherwise it is moved.
		/// Returns the resulting backup path, or null if the source does not exist.
		/// </summary>
		public RaiPath backup(bool copy = false)
		{
			if (!Exists()) return null;
			var backupRoot = Os.LocalBackupDir ?? throw new InvalidOperationException("LocalBackupDir not configured.");
			var trimmed = path.TrimEnd(Os.DIR[0]);
			var pos = trimmed.LastIndexOf(Os.DIR[0]);
			var parentDir = pos < 0 ? string.Empty : trimmed.Substring(0, pos + 1);
			var leaf = pos < 0 ? trimmed : trimmed.Substring(pos + 1);
			var parentRel = RaiFile.BackupRelativePath(new RaiPath(parentDir));
			var stamped = leaf + "_" + DateTimeOffset.UtcNow.ToString(Os.DATEFORMAT);
			var backupTarget = backupRoot / parentRel / stamped;
			backupTarget.Parent.mkdir();
			if (copy) backupTarget.cp(this, replace: false, keepBackup: false);
			else backupTarget.mv(this, replace: false, keepBackup: false);
			return backupTarget;
		}
		public void rmdir(int depth = 0, bool deleteFiles = false)
		{
			if (string.IsNullOrEmpty(Path) || !Exists()) return;
			try
			{
				if (depth > 0)
				{
					if (deleteFiles)
					{
						foreach (var file in this.EnumerateFiles("*").ToList())
						{
							file.rm();
						}
					}
					foreach (var subdir in this.EnumerateDirectories("*").ToList())
					{
						subdir.rmdir(depth - 1, deleteFiles);
					}
				}
				if (Exists())
				{
					Directory.Delete(Path, deleteFiles);
					if (this.Cloud) this.awaitDirVanishing();
				}
			}
			catch (DirectoryNotFoundException) { return; }
		}
		public static char[] InvalidFileNameChars => System.IO.Path.GetInvalidFileNameChars();
	}
}
