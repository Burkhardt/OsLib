using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
namespace OsLib
{
	/// <summary>
	/// RaiPath – uses operator/ to add a subdirectory to a path
	/// just the path, no filename, no extension
	/// </summary>
	/// <summary>
	/// Represents a directory path and enforces a trailing directory separator.
	/// </summary>
	public class RaiPath
	{
		private const int maxWaitCount = 60;
		public override string ToString() => Path;
		public string Path
		{
			get => path;
			set
			{
				(path, _) = splitPathAndName(value);
			}
		}
		private string path = string.Empty;
		internal static (string path, string name) splitPathAndName(string pathAndName)
		{
			if (string.IsNullOrWhiteSpace(pathAndName))
				return (string.Empty, string.Empty);
			//pathAndName = NormSeperator(pathAndName);
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
				//p = Os.EnsureTrailingDirectorySeparator(p.Substring(0, p.LastIndexOf(Os.DIR) + 1));
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
				else {
					n = p.Substring(pos + 1);
					p = p.Remove(pos + 1);
					p = Os.EnsureTrailingDirectorySeparator(p);
				}
			}
			return (p, n);
		}
		public static (RaiPath path, string name) SplitRaiPathAndName(string filename)
		{
			(string p, string n) = RaiPath.splitPathAndName(filename);
			return (new RaiPath(p), n);
		}
		/// <summary>
		/// Using the / operator to add a subdirectory to a path
		/// </summary>
		/// <param name="self"></param>
		/// <param name="subDir">string</param>
		/// <returns>RaiPath object for daisy chaining reasons</returns>
		public static RaiPath operator /(RaiPath self, string subDir)
		{
			return new RaiPath(self.Path + subDir + Os.DIR);
		}
		/// <summary>
		/// Using the / operator to add a subdirectory to a path
		/// </summary>
		/// <param name="self"></param>
		/// <param name="subDir">RaiPath</param>
		/// <returns>RaiPath object for daisy chaining reasons</returns>
		public static RaiPath operator /(RaiPath self, RaiPath subDir)
		{
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
		/// Constructor that takes a string path; the caller knows that this is a directory path; it does not have to exist yet in the file system.
		/// </summary>
		/// <param name="s">Directory path or full file name. If s ends with a directory separator it is treated as a directory path; otherwise the last segment is stripped. "." and "~" are expanded.</param>
		public RaiPath(string s = ".")
		{
			Path = s;
		}
		/// <summary>
		/// Constructor that takes a RaiFile object; uses its Path and ignores Name and Ext.
		/// </summary>
		public RaiPath(RaiFile f)
		{
			Path = f?.Path?.ToString() ?? string.Empty;
		}
		public bool Exists() => Directory.Exists(Path);
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
