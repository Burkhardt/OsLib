using System;

namespace OsLib
{
	public class TmpFile : RaiFile
	{
		private static string CreateDefaultName() =>
			$@"Temp{DateTime.UtcNow:yyyyMMddHH}" + Guid.NewGuid().ToString("N");

		private static string ResolveExtension(string fileName, string ext)
		{
			if (!string.IsNullOrWhiteSpace(ext))
				return ext;
			if (string.IsNullOrWhiteSpace(fileName))
				return "tmp";
			return fileName.Contains('.', StringComparison.Ordinal) ? null : "tmp";
		}

		/// <summary>
		/// Creates the temporary file on disk and ensures missing parent directories are created.
		/// Implementation delegates to <see cref="TextFile.Save(bool)"/>, which calls <see cref="RaiFile.mkdir()"/>.
		/// </summary>
		public void create()
		{
			var text = new TextFile(FullName);
			text.Append("");
			text.Save();
		}

		public int mv(TmpFile src, bool replace = false, bool keepBackup = false) => mv((RaiFile)src, replace, keepBackup);

		/// <summary>
		/// A file in the TempDir, located usually on the fastest drive of the system (SSD or RAM-Disk).
		/// </summary>
		/// <param name="fileName">No file name given: the OS chooses a temp file name.</param>
		/// <param name="ext">Changes the system-generated or given filename, if not null.</param>
		public TmpFile(string fileName = null, string ext = null)
			: base(fileName ?? (Os.TempDir / CreateDefaultName()).Path)
		{
			// ImageServer needs access if this library is used from within an IIS app.
			if (ext != null)
				Ext = ext;
		}
		public TmpFile(RaiPath path, string fileName = null, string ext = null)
			: base(path, name: fileName ?? CreateDefaultName(), ext: ResolveExtension(fileName, ext))
		{
		}
	}
}
