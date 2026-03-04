using System;

namespace OsLib
{
	/// <summary>
	/// Canonical path convention: a file lives in a folder named like its stem.
	/// </summary>
	public class CanonicalPath : RaiPath
	{
		private static string NormalizeRootPath(string rootCandidate, string fileStem)
		{
			if (string.IsNullOrEmpty(rootCandidate))
				return string.Empty;

			var normalized = new RaiPath(rootCandidate).Path;
			if (string.IsNullOrEmpty(fileStem))
				return normalized;

			var marker = Os.DIRSEPERATOR + fileStem + Os.DIRSEPERATOR;
			if (normalized.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
				return normalized.Substring(0, normalized.Length - marker.Length + 1);

			return normalized;
		}

		public string RootPath
		{
			get => rootPath;
			set
			{
				rootPath = NormalizeRootPath(value, FileStem);
				Apply();
			}
		}
		private string rootPath = string.Empty;

		public string FileStem
		{
			get => fileStem;
			set
			{
				fileStem = string.IsNullOrEmpty(value) ? string.Empty : value;
				rootPath = NormalizeRootPath(rootPath, fileStem);
				Apply();
			}
		}
		private string fileStem = string.Empty;

		public void Apply()
		{
			Path = string.IsNullOrEmpty(FileStem)
				? RootPath
				: RootPath + FileStem + Os.DIRSEPERATOR;
		}

		public CanonicalPath(string rootPath, string fileStem)
			: base(rootPath)
		{
			this.fileStem = string.IsNullOrEmpty(fileStem) ? string.Empty : fileStem;
			this.rootPath = NormalizeRootPath(rootPath, this.fileStem);
			Apply();
		}
	}

	/// <summary>
	/// Organizes the file into a directory based on its own name.
	/// For example, a file ./123456.json will be moved to ./123456/123456.json
	/// only checked in constructor, not in later operations like Path or Name changes
	/// </summary>
	public class CanonicalFile : RaiFile, IPathConventionFile
	{
		public int mv(CanonicalFile src, bool replace = false, bool keepBackup = false) => mv((RaiFile)src, replace, keepBackup);
		public PathConventionType ConventionName => PathConventionType.CanonicalByName;
		public void ApplyPathConvention()
		{
			var canonicalPath = new CanonicalPath(Path, Name);
			if (!string.Equals(Path, canonicalPath.Path, StringComparison.Ordinal))
			{
				Path = canonicalPath.Path;
				mkdir();
			}
		}
		/// <summary>
		/// A CononicalFile is a file that is inside a directory based on its own name.
		/// </summary>
		/// <param name="fullName">i.e. ~/StorageDir/AfricaStage/AfricaStage.pit or ~/StorageDir/AfricaStage.pit or ~/StorageDir/AfricaStage/</param>
		/// <param name="defaultExt">optional parameter if fullName doesn't come with an extension</param>
		/// <exception cref="Exception"></exception>
		public CanonicalFile(string fullName, string defaultExt = "json") : base(fullName)
		{
			var probe = new RaiFile(fullName);
			var stem = string.IsNullOrEmpty(probe.Name)
				? (probe.DirList.Length > 0 ? probe.DirList[^1] : "file")
				: probe.Name;
			var resolvedExt = string.IsNullOrEmpty(probe.Ext) ? defaultExt : probe.Ext;

			Name = stem;
			Ext = resolvedExt;

			var from = new RaiFile(probe.Path + stem + "." + resolvedExt);

			from.mkdir();
			var before = Path;
			ApplyPathConvention();
			var hadNonCanonicalPath = !string.Equals(before, Path, StringComparison.Ordinal);
			if (hadNonCanonicalPath)
			{
				if (!Exists())
				{
					if (!from.Exists())
					{
						var seed = new TextFile(from.FullName);
						seed.Append("");
						seed.Save();
						mv(from, replace: false, keepBackup: false);
					}
					else
					{
						mv(from, replace: false, keepBackup: false);
					}
				}
			}
		}
	}
}
