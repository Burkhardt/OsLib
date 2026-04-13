using System;

namespace OsLib
{
	internal static class CloudPathWiring
	{
		public static void Initialize()
		{
			// Point the delegate to a proper named method
			RaiPath.CloudEvaluator = EvaluateCloudPath;
		}
		private static bool EvaluateCloudPath(string path)
		{
			// SET BREAKPOINT HERE - It will hit 100% of the time!
			if (!Os.IsConfigLoaded)
				return false;

			if (string.IsNullOrWhiteSpace(path))
				return false;

			try
			{
				var config = Os.Config;
				if (config == null) return false;

				string onedrive = config.Cloud?.OneDrive;
				string dropbox = config.Cloud?.Dropbox;
				string gdrive = config.Cloud?.GoogleDrive;

				return (onedrive != null && path.StartsWith(onedrive))
					|| (dropbox != null && path.StartsWith(dropbox))
					|| (gdrive != null && path.StartsWith(gdrive));
			}
			catch
			{
				return false;
			}
		}
	}
}