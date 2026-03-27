using System;
using System.ComponentModel.Design;
using System.Linq;

namespace OsLib
{
	/// <summary>
	/// Canonical path convention: a file lives in a folder named like its stem.
	/// </summary>
	[Obsolete("Use RaiPath directly instead. CanonicalPath is deprecated.")]
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
		[Obsolete("FileStem sounds like Name or SubDir from imageFile")]
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
		public override string Name { 
			get {
				if (string.IsNullOrEmpty(Name))
				{
					var dirs = Path.Split(Os.DIRSEPERATOR);
					if (dirs.Length > 0)
						base.Name = dirs[^1];
				}
				return base.Name; 
			}
			set {
				base.Name = value;
				if (string.IsNullOrEmpty(Path) || Path == Os.DIRSEPERATOR)
					Path = value;
				else
				{
					var dirs = Path.Split(Os.DIRSEPERATOR);
					if (dirs[^1] != value)
					{
						Path = Path + Os.DIRSEPERATOR + Name; // the trailing Os.DIRSEPERATOR will be added by the setter of RaiFile.Path
					}
				}
			}
		}
		public int mv(CanonicalFile src, bool replace = false, bool keepBackup = false) => mv((RaiFile)src, replace, keepBackup);
		public PathConventionType ConventionName => PathConventionType.CanonicalByName;
		[Obsolete("all methods of CanonicalFileare maintaining the convention")]
		public void ApplyPathConvention()
		{
			var canonicalPath = new CanonicalPath(Path, Name);
			if (!string.Equals(Path, canonicalPath.Path, StringComparison.Ordinal))
			{
				Path = canonicalPath.Path;
				// mkdir();	// absolutely not => no implicit file system contact
			}
		}
		/// <summary>
		/// A CononicalFile is a file that is inside a directory named after its own name.
		/// </summary>
		/// <param name="fullName">i.e. ~/StorageDir/AfricaStage/AfricaStage.pit or ~/StorageDir/AfricaStage.pit or ~/StorageDir/AfricaStage/</param>
		/// <param name="defaultExt">optional parameter if fullName doesn't come with an extension</param>
		/// <exception cref="Exception"></exception>
		public CanonicalFile(string fullName) : this(new RaiPath(fullName), new RaiFile(fullName).NameWithExtension, null)
		{}
		
		/// <summary>
		/// The file must reside in a directory named after its own name
		/// </summary>
		/// <param name="path">i.e. ~/StorageDir/AfricaStage/Person/</param>
		/// <param name="name">i.e.Person or Person.pit</param>
		/// <param name="ext">i.e. pit or null or string.Empty</param>
		public CanonicalFile(RaiPath path, string name, string ext = null) : base(path, name, ext)
		{
			// check if there is a name - if not take it from the leaf-level directory
			if (string.IsNullOrEmpty(name))
			{
				var dirs = path.Path.Split(Os.DIRSEPERATOR);
				Name = dirs.Length > 0 ? dirs[^1] : null;   // was empty, stays empty
			}
			// if name is given as name.ext, split it into name and ext ... RaiFile did this already
			// at this point, we have a Name, and Ext and a Path; all we have to do is see if the LeafDir is equal to Name
			// are they already equal?
			if (!Path.EndsWith(Name + Os.DIRSEPERATOR, StringComparison.Ordinal))
			{	// not canonical, adjust the path
				Path = Path + Name + Os.DIRSEPERATOR;
			}
		}
	}
}
