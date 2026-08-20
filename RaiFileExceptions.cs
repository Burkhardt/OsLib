using System;
using System.IO;

namespace OsLib
{
	/// <summary>
	/// Wraps an operating-system file I/O failure at the <see cref="RaiFile"/>
	/// boundary while retaining <see cref="IOException"/> compatibility.
	/// </summary>
	public sealed class RaiFileIOException : IOException
	{
		public RaiFileIOException(string message, string fileName, Exception innerException)
			: base(message, innerException)
		{
			FileName = fileName;
		}

		/// <summary>The file involved in the failed operation.</summary>
		public string FileName { get; }
	}
}
