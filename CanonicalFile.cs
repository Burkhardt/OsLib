using System;
using System.ComponentModel.Design;
using System.Linq;

namespace OsLib
{
	/// <summary>
	/// Organizes the file into a directory based on its own name.
	/// For example, a file ./123456.json will be moved to ./123456/123456.json
	/// only checked in constructor, not in later operations like Path or Name changes
	/// </summary>
	/// <remarks>
	/// see unit test CanonicalFile_Appends_Folder() for expected behavior 
	/// </remarks>
	public class CanonicalFile : RaiFile, IPathConvention
	{
		public int mv(CanonicalFile src, bool replace = false, bool keepBackup = false) => mv((RaiFile)src, replace, keepBackup);
		public PathConventionType Convention => PathConventionType.CanonicalByName;
		/// <summary>
		/// Only apply when the Name property is set and the Path does not already end with the Name followed by a directory separator.
		/// </summary>
		public void ApplyPathConvention()
		{
			if (!string.IsNullOrEmpty(Name) && !Path.ToString().EndsWith(Name + Os.DIR, StringComparison.Ordinal))
			{
				Path = Path / Name;
			}
		}
		public override string Name {
			get => base.Name;
			set {
				base.Name = value;
				ApplyPathConvention();
			} 
		}
		/// <summary>
		/// A CononicalFile is a file that is inside a directory named after its own name.
		/// </summary>
		/// <param name="fullName">i.e. ~/StorageDir/AfricaStage/AfricaStage.pit or ~/StorageDir/AfricaStage.pit or ~/StorageDir/AfricaStage/</param>
		/// <exception cref="Exception"></exception>
		public CanonicalFile(string fullName) : base(fullName) // this(new RaiPath(fullName), new RaiFile(fullName).NameWithExtension, ext: "")
		{
			ApplyPathConvention();
		}

		/// <summary>
		/// The file must reside in a directory named after its own name
		/// </summary>
		/// <param name="path">i.e. ~/StorageDir/AfricaStage/Person/</param>
		/// <param name="name">i.e.Person or Person.pit</param>
		/// <param name="ext">i.e. pit or "" or string.Empty</param>
		public CanonicalFile(RaiPath path, string name, string ext = "") : base(path, name, ext)
		{
			ApplyPathConvention();
		}
	}
}
