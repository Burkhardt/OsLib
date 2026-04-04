using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;

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
		public override string ToString() => Path;
		public string Path
		{
			get
			{
				return path.ToString();
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
			return new RaiPath(self.path.Path + subDir + Os.DIR);
		}
		/// <summary>
		/// Using the / operator to add a subdirectory to a path
		/// </summary>
		/// <param name="self"></param>
		/// <param name="subDir">RaiPath</param>
		/// <returns>RaiPath object for daisy chaining reasons</returns>
		public static RaiPath operator /(RaiPath self, RaiPath subDir)
		{
			return new RaiPath(self.path.Path + subDir.Path);
		}
		public List<RaiFile> GetFiles(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			List<RaiFile> files = new List<RaiFile>();
			foreach (var file in Directory.GetFiles(Path, searchPattern, searchOption))
			{
				files.Add(new RaiFile(file));
			}
			return files;
		}
		public IEnumerable<RaiFile> EnumerateFiles(string searchPattern, bool recursive = false)
		{
			foreach (var file in Directory.EnumerateFiles(Path, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
			{
				yield return new RaiFile(file);
			}
		}
		/// <summary>
		/// Constructor that takes a string path; the caller knows that this is a directory path; it does not have to exist yet in the file system.
		/// </summary>
		/// <param name="s">if value of s does not end with a directory separator, one will be added; "." gets current directory, "~" gets user home directory</param>
		public RaiPath(string s = ".")
		{
			if (string.IsNullOrWhiteSpace(s))
			{
				Path = string.Empty;
				return;
			}
			s = Os.ExpandLeadingDirectorySymbols(s);
			var p = new RaiFile(s);
			p.Name = string.Empty;
			p.Ext = string.Empty;
			Path = p.ToString();
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
		public bool Exists() => Directory.Exists(path.ToString());
		public RaiPath mkdir() => path.mkdir();
		public void rmdir(int depth = 0, bool deleteFiles = false) => path.rmdir(depth, deleteFiles);
		public static char[] InvalidFileNameChars => System.IO.Path.GetInvalidFileNameChars();
	}
}
