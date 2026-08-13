using System;

namespace OsLib
{
	/// <summary>Base exception for failures concerning a <see cref="RaiPath"/>.</summary>
	public class RaiPathException : Exception
	{
		public RaiPathException(string message, string pathName) : base(message)
		{
			PathName = pathName;
		}

		public RaiPathException(string message, string pathName, Exception innerException)
			: base(message, innerException)
		{
			PathName = pathName;
		}

		/// <summary>The directory path involved in the failure, when known.</summary>
		public string PathName { get; }
	}

	/// <summary>Thrown when a required <see cref="RaiPath"/> does not exist or materialize.</summary>
	public sealed class RaiPathNotFoundException : RaiPathException
	{
		public RaiPathNotFoundException(string message, string pathName) : base(message, pathName)
		{
		}

		public RaiPathNotFoundException(string message, string pathName, Exception innerException)
			: base(message, pathName, innerException)
		{
		}
	}
}
