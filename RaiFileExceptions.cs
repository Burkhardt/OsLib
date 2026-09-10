using System;
using System.IO;

namespace OsLib
{
	/// <summary>
	/// Raised when an operation would violate RAIkeep's cloud-storage pathname
	/// contract before the operating system is allowed to mutate either path.
	/// </summary>
	public sealed class RaiCloudStorageException : IOException
	{
		public RaiCloudStorageException(
			string message,
			string operation,
			string sourcePath,
			string destinationPath)
			: base(message)
		{
			Operation = operation;
			SourcePath = sourcePath;
			DestinationPath = destinationPath;
		}

		/// <summary>The rejected RaiFile or RaiPath operation.</summary>
		public string Operation { get; }

		/// <summary>The source path involved in the rejected operation.</summary>
		public string SourcePath { get; }

		/// <summary>The cloud-backed destination path involved in the rejected operation.</summary>
		public string DestinationPath { get; }
	}

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
