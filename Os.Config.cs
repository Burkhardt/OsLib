using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OsLib
{
	public static partial class Os
	{
		private static dynamic config;
		private static string rawTempDir; // Store the string during bootstrap
		private static RaiPath tempDir;   // Instantiate only when asked
		private static bool isLoadedFromDisk = false;
		static Os()
		{
			loadConfig();
		}
		public static dynamic Config => config;
		public static bool IsConfigLoaded => isLoadedFromDisk;
		// The data is already loaded. We only create the RaiPath wrapper on first access.
		public static RaiPath TempDir => tempDir ??= new RaiPath(rawTempDir);
		private static void loadConfig()
		{
			try
			{
				string cf = GetBootstrapConfigPath();
				config = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(cf));
				// NO RaiPath INVOCATION HERE
				rawTempDir = (string)config.TempDir ?? throw new ArgumentNullException("TempDir missing");
				isLoadedFromDisk = true;
			}
			catch
			{
				config = JObject.FromObject(new { TempDir = Path.GetTempPath() });
				// NO RaiPath INVOCATION HERE
				rawTempDir = Path.GetTempPath();
				isLoadedFromDisk = false;
			}
		}
		private static string GetBootstrapConfigPath()
		{
			string rawPath = defaultConfigFileLocation;
			if (rawPath.StartsWith("~/"))
			{
				string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				return home + rawPath.Substring(1);
			}
			return rawPath;
		}
	}
}