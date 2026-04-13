using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class OsEnvironmentPathTests
{
	private static string ensureTrailingSeparator(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var normalized = Os.NormSeperator(value);
		return normalized.EndsWith(Os.DIR, StringComparison.Ordinal) ? normalized : normalized + Os.DIR;
	}

}
