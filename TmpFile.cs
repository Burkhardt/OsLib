using System;

namespace OsLib
{
	public class TmpFile : RaiFile
	{
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
			: base(fileName ?? (Os.TempDir / $@"Temp{DateTime.UtcNow:yyyyMMddHH}").Path + Guid.NewGuid().ToString("N"))
		{
			// ImageServer needs access if this library is used from within an IIS app.
			if (ext != null)
				Ext = ext;
		}
	}
}
