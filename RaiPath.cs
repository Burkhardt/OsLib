using System;
using System.IO;

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
		/// Using the / operator to add a subdirectory to a path
		/// </summary>
		/// <param name="self"></param>
		/// <param name="subDir">RaiPath</param>
		/// <returns>RaiPath object for daisy chaining reasons</returns>
		public static RaiPath operator /(RaiPath self, RaiPath subDir)
		{
			return new RaiPath(self.path.Path + subDir.Path);
		}
		private static string homePath
		{
			get
			{
				return Os.ResolveSystemHomeDir();
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
			if (s == ".")
				s = Directory.GetCurrentDirectory();
			if (s.StartsWith("./"))
				s = System.IO.Path.Combine(Directory.GetCurrentDirectory(), s.Substring(2));
			if (s == "~")
				s = homePath;
			if (s.StartsWith("~/"))
				s = homePath + s.Substring(2); // relative to home directory
			Path = new RaiFile(s).Path;
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
		public bool Exists() => Directory.Exists(path.Path);
		public RaiPath mkdir() => new RaiFile(Path).mkdir();
		public void rmdir(int depth = 0, bool deleteFiles = false) => new RaiFile(Path).rmdir(depth, deleteFiles);
		public override string ToString() => Path;
	}
}