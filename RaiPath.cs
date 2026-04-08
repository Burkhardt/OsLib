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
		protected static string EnsureTrailingDirectorySeparator(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;
			var normalized = Os.NormSeperator(value);
			return normalized.EndsWith(Os.DIR, StringComparison.Ordinal) ? normalized : normalized + Os.DIR;
		}
		protected static bool IsAbsoluteLike(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return false;
			var normalized = Os.NormSeperator(value);
			if (normalized == "~" || normalized.StartsWith("~/", StringComparison.Ordinal))
				return true;
			if (normalized.StartsWith(Os.DIR, StringComparison.Ordinal))
				return true;
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
			set => path = Normalize(value);
		}
		/// <summary>
		/// Normalize a string into a relative directory path.
		/// Strips leading "./", rejects absolute-looking input,
		/// and ensures a trailing directory separator.
		/// </summary>
		internal static string Normalize(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;
			var trimmed = Os.NormSeperator(value.Trim());
			if (trimmed == ".")
				return string.Empty;
			if (trimmed.StartsWith("." + Os.DIR, StringComparison.Ordinal))
				trimmed = trimmed.Substring(1 + Os.DIR.Length);
			if (string.IsNullOrEmpty(trimmed))
				return string.Empty;
			if (IsAbsoluteLike(trimmed))
				throw new ArgumentException("RaiRelPath requires a relative path.", nameof(value));
			return EnsureTrailingDirectorySeparator(trimmed);
		}
		/// <summary>
		/// Split a relative path-and-name string into the directory portion
		/// and the trailing name portion.
		/// "sub/dir/file.txt" -> (path: "sub/dir/", name: "file.txt")
		/// "sub/dir/"         -> (path: "sub/dir/", name: "")
		/// "file.txt"         -> (path: "",          name: "file.txt")
		/// </summary>
		internal static (string path, string name) SplitPathAndName(string pathAndName)
		{
			if (string.IsNullOrWhiteSpace(pathAndName))
				return (string.Empty, string.Empty);
			var normalized = Os.NormSeperator(pathAndName.Trim());
			if (normalized == ".")
				return (string.Empty, string.Empty);
			if (normalized.StartsWith("." + Os.DIR, StringComparison.Ordinal))
				normalized = normalized.Substring(1 + Os.DIR.Length);
			if (string.IsNullOrEmpty(normalized))
				return (string.Empty, string.Empty);
			if (IsAbsoluteLike(normalized))
				throw new ArgumentException("RaiRelPath requires a relative path.", nameof(pathAndName));
			if (normalized.EndsWith(Os.DIR, StringComparison.Ordinal))
				return (EnsureTrailingDirectorySeparator(normalized), string.Empty);
			var pos = normalized.LastIndexOf(Os.DIR[0]);
			if (pos < 0)
				return (string.Empty, normalized);
			return (EnsureTrailingDirectorySeparator(normalized.Remove(pos + 1)), normalized.Substring(pos + 1));
		}
		public static (RaiRelPath path, string name) SplitRaiRelPathAndName(string pathAndName)
		{
			(string p, string n) = SplitPathAndName(pathAndName);
			return (new RaiRelPath(p), n);
		}
		/// <summary>
		/// Append a subdirectory segment to this relative path.
		/// </summary>
		public static RaiRelPath operator /(RaiRelPath self, string subDir)
		{
			if (self == null)
				throw new ArgumentNullException(nameof(self));
			if (string.IsNullOrWhiteSpace(subDir))
				return new RaiRelPath(self.Path);
			return new RaiRelPath(self.Path + subDir + Os.DIR);
		}
		/// <summary>
		/// Concatenate two relative paths.
		/// </summary>
		public static RaiRelPath operator /(RaiRelPath self, RaiRelPath other)
		{
			if (self == null)
				throw new ArgumentNullException(nameof(self));
			if (other == null)
				throw new ArgumentNullException(nameof(other));
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
				(path, _) = SplitPathAndName(value);
			}
		}
		internal static (string path, string name) SplitPathAndName(string pathAndName)
		{
			if (string.IsNullOrWhiteSpace(pathAndName))
				return (string.Empty, string.Empty);
			if (pathAndName == "~")
				return (Os.EnsureTrailingDirectorySeparator(Os.UserHomeDir.Path), string.Empty);
			string n, p;
			int pos;
			if (pathAndName.StartsWith("~/", StringComparison.Ordinal))
			{
				p = Os.UserHomeDir.Path + pathAndName.Substring(2);
				pos = p.LastIndexOf(Os.DIR);
				if (pos < 0)
				{
					n = p;
					p = string.Empty;
				}
				else
				{
					n = p.Substring(pos + 1);
					p = p.Remove(pos + 1);
					p = Os.EnsureTrailingDirectorySeparator(p);
				}
			}
			else
			{
				p = System.IO.Path.GetFullPath(pathAndName);
				if (pathAndName == ".")
					return (Os.EnsureTrailingDirectorySeparator(p), string.Empty);
				pos = p.LastIndexOf(Os.DIR);
				if (pos < 0)
				{
					n = p;
					p = string.Empty;
				}
				else
				{
					n = p.Substring(pos + 1);
					p = p.Remove(pos + 1);
					p = Os.EnsureTrailingDirectorySeparator(p);
				}
			}
			return (p, n);
		}
		public static (RaiPath path, string name) SplitRaiPathAndName(string filename)
		{
			(string p, string n) = SplitPathAndName(filename);
			return (new RaiPath(p), n);
		}
		/// <summary>
		/// Append a subdirectory name to this absolute path.
		/// The string is wrapped into a RaiRelPath before combining.
		/// </summary>
		public static RaiPath operator /(RaiPath self, string subDir)
		{
			if (self == null)
				throw new ArgumentNullException(nameof(self));
			if (string.IsNullOrWhiteSpace(subDir))
				return new RaiPath(self.Path);
			return self / new RaiRelPath(subDir);
		}
		/// <summary>
		/// Anchor a relative path onto this absolute path,
		/// producing a new absolute RaiPath.
		/// </summary>
		public static RaiPath operator /(RaiPath self, RaiRelPath subDir)
		{
			if (self == null)
				throw new ArgumentNullException(nameof(self));
			if (subDir == null || subDir.IsEmpty)
				return new RaiPath(self.Path);
			return new RaiPath(self.Path + subDir.Path);
		}
		public IEnumerable<RaiFile> EnumerateFiles(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			foreach (var file in Directory.EnumerateFiles(Path, searchPattern, searchOption))
			{
				yield return new RaiFile(file);
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
				var parent = Os.ParentDirectory(path);
				return string.IsNullOrEmpty(parent) ? this : new RaiPath(parent);
			}
		}
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
				catch (Exception)
				{
				}
				if (exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirMaterializing of dir " + dirName + ".");
			return -count;
		}
		private static int awaitDirVanishing(string dirName)
		{
			var count = 0;
			var exists = true;
			while (count < maxWaitCount)
			{
				try
				{
					exists = Directory.Exists(dirName);
				}
				catch (Exception)
				{
				}
				if (!exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirVanishing of dir " + dirName + ".");
			return -count;
		}
		public RaiPath mkdir() => mkdir(Path);
		public static RaiPath mkdir(string dirname = null)
		{
			dirname = string.IsNullOrEmpty(dirname)
				? Os.EnsureTrailingDirectorySeparator(Directory.GetCurrentDirectory())
				: dirname;
			var path = new RaiPath(dirname);
			if (path.Exists())
				return path;
			var dir = new DirectoryInfo(path.Path);
			if (!dir.Exists)
			{
				dir = Directory.CreateDirectory(path.Path);
				if (Os.IsCloudPath(path.Path))
					awaitDirMaterializing(path.Path);
			}
			return new RaiPath(Os.EnsureTrailingDirectorySeparator(dir.FullName));
		}
		public void rmdir(int depth = 0, bool deleteFiles = false)
		{
			var directoryPath = Path;
			if (string.IsNullOrEmpty(directoryPath))
				return;
			if (!Directory.Exists(directoryPath))
				return;
			var cloudPath = Os.IsCloudPath(directoryPath);
			try
			{
				if (Directory.EnumerateFileSystemEntries(directoryPath).Any() && depth > 0)
				{
					if (deleteFiles)
					{
						foreach (var file in Directory.EnumerateFiles(directoryPath).ToList())
							new RaiFile(file).rm();
					}
					foreach (var subdir in Directory.EnumerateDirectories(directoryPath).ToList())
						new RaiPath(subdir).rmdir(depth - 1, deleteFiles);
				}
				if (Directory.Exists(directoryPath))
					Directory.Delete(directoryPath, deleteFiles);
			}
			catch (DirectoryNotFoundException)
			{
				return;
			}
			if (cloudPath)
				awaitDirVanishing(directoryPath);
		}
		public static char[] InvalidFileNameChars => System.IO.Path.GetInvalidFileNameChars();
	}
}
