using System;

namespace OsLib
{
	internal static class CloudPathWiring
	{
		public static void Initialize()
		{
			// Compatibility bridge: cloud classification now flows through Os.IsCloudPath,
			// backed by the immutable Os runtime snapshot.
		}
	}
}
