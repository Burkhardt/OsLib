using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OsLib;
using Xunit;

namespace OsLib.Tests
{
	[Collection("CloudStorageEnvironment")]
	public class CloudStorageAgreementTests
	{
		private static void ResetOsCaches()
		{
			var osType = typeof(Os);
			osType.GetField("homeDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("type", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("dIRSEPERATOR", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			Os.ResetCloudStorageCache();
		}

		private static RaiPath NewTestRoot()
		{
			return new RaiPath(Os.TempDir) / "oslib-tests" / "cloud-agreement" / Guid.NewGuid().ToString("N");
		}

		private static void Cleanup(RaiPath root)
		{
			try
			{
				if (Directory.Exists(root.Path))
					new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
			}
			catch
			{
			}
		}

		[Fact]
		public void CloudStorageRoot_UsesDocumentedDefaultOrder()
		{
			var root = NewTestRoot();
			Directory.CreateDirectory(root.Path);

			var home = (root / "home").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var dropbox = (root / "DropboxRoot").Path;
			var googleDrive = (root / "GoogleDriveRoot").Path;
			Directory.CreateDirectory(home);
			Directory.CreateDirectory(dropbox);
			Directory.CreateDirectory(googleDrive);

			var env = new EnvScope(new Dictionary<string, string?>
			{
				["HOME"] = home,
				["USERPROFILE"] = home,
				["APPDATA"] = home,
				["LOCALAPPDATA"] = home,
				["OneDrive"] = null,
				["OneDriveCommercial"] = null,
				["OneDriveConsumer"] = null,
				["OSLIB_CLOUD_ROOT_DROPBOX"] = dropbox,
				["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = googleDrive,
				["OSLIB_CLOUD_ROOT_ONEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_ICLOUD"] = null,
				["OSLIB_CLOUD_CONFIG"] = null
			});

			try
			{
				ResetOsCaches();

				Assert.Equal(new RaiPath(googleDrive).Path, Os.CloudStorageRoot);
				Assert.Equal(new RaiPath(googleDrive).Path, Os.GetCloudStorageRoot(CloudStorageType.GoogleDrive));
				Assert.Null(Os.GetCloudStorageRoot(CloudStorageType.ICloud));
			}
			finally
			{
				env.Dispose();
				ResetOsCaches();
				Cleanup(root);
			}
		}

		[Fact]
		public void CloudStorageRoot_ThrowsWithSetupGuidance_WhenNothingIsDiscovered()
		{
			var root = NewTestRoot();
			Directory.CreateDirectory(root.Path);

			var home = (root / "home").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			Directory.CreateDirectory(home);

			var env = new EnvScope(new Dictionary<string, string?>
			{
				["HOME"] = home,
				["USERPROFILE"] = home,
				["APPDATA"] = home,
				["LOCALAPPDATA"] = home,
				["OneDrive"] = null,
				["OneDriveCommercial"] = null,
				["OneDriveConsumer"] = null,
				["OSLIB_CLOUD_ROOT_DROPBOX"] = null,
				["OSLIB_CLOUD_ROOT_ONEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_ICLOUD"] = null,
				["OSLIB_CLOUD_CONFIG"] = null
			});

			try
			{
				ResetOsCaches();

				var ex = Assert.Throws<DirectoryNotFoundException>(() => _ = Os.CloudStorageRoot);
				Assert.Contains("OSLIB_CLOUD_ROOT_GOOGLEDRIVE", ex.Message);
				Assert.Contains("OSLIB_CLOUD_CONFIG", ex.Message);
			}
			finally
			{
				env.Dispose();
				ResetOsCaches();
				Cleanup(root);
			}
		}

		[Fact]
		public void GetCloudStorageRoots_UsesOnlyConfiguredIniCandidate_AndSupportsAliases()
		{
			var root = NewTestRoot();
			Directory.CreateDirectory(root.Path);

			var home = (root / "home").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var configuredGoogle = (root / "ConfiguredGoogleDrive").Path;
			var configuredICloud = (root / "ConfiguredICloud").Path;
			var defaultDropbox = (root / "DefaultDropbox").Path;
			Directory.CreateDirectory(home);
			Directory.CreateDirectory(configuredGoogle);
			Directory.CreateDirectory(configuredICloud);
			Directory.CreateDirectory(defaultDropbox);

			var configuredIni = new RaiFile((root / "cfg").Path + "configured-cloudstorage.ini");
			RaiFile.mkdir(configuredIni.Path);
			File.WriteAllLines(configuredIni.FullName,
			[
				$"google_drive={configuredGoogle}",
				$"icloud_drive={configuredICloud}"
			]);

			var defaultConfigDir = new RaiPath(home) / ".config" / "oslib";
			defaultConfigDir.mkdir();
			File.WriteAllLines(new RaiFile(defaultConfigDir.Path + "cloudstorage.ini").FullName,
			[
				$"dropbox={defaultDropbox}"
			]);

			var env = new EnvScope(new Dictionary<string, string?>
			{
				["HOME"] = home,
				["OSLIB_CLOUD_CONFIG"] = configuredIni.FullName,
				["OSLIB_CLOUD_ROOT_DROPBOX"] = null,
				["OSLIB_CLOUD_ROOT_ONEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_ICLOUD"] = null
			});

			try
			{
				ResetOsCaches();
				var roots = Os.GetCloudStorageRoots(refresh: true);

				Assert.Equal(new RaiPath(configuredGoogle).Path, roots[CloudStorageType.GoogleDrive]);
				Assert.Equal(new RaiPath(configuredICloud).Path, roots[CloudStorageType.ICloud]);
				Assert.False(roots.ContainsKey(CloudStorageType.Dropbox));
			}
			finally
			{
				env.Dispose();
				ResetOsCaches();
				Cleanup(root);
			}
		}

		[Fact]
		public void GetCloudStorageRoots_ProbesHomeOneDriveVariants_OnUnix()
		{
			if (Os.Type == OsType.Windows)
				return;

			var root = NewTestRoot();
			Directory.CreateDirectory(root.Path);

			var home = (root / "home").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var oneDriveVariant = new RaiPath(home) / "OneDrive - Mzansi";
			Directory.CreateDirectory(home);
			Directory.CreateDirectory(oneDriveVariant.Path);

			var env = new EnvScope(new Dictionary<string, string?>
			{
				["HOME"] = home,
				["OSLIB_CLOUD_ROOT_DROPBOX"] = null,
				["OSLIB_CLOUD_ROOT_ONEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_ICLOUD"] = null,
				["OSLIB_CLOUD_CONFIG"] = null
			});

			try
			{
				ResetOsCaches();
				Assert.Equal(oneDriveVariant.Path, Os.GetCloudStorageRoot(CloudStorageType.OneDrive, refresh: true));
			}
			finally
			{
				env.Dispose();
				ResetOsCaches();
				Cleanup(root);
			}
		}

		[Fact]
		public void RaiFile_UsesDiscoveredGoogleDriveRoot_ForCloudAwareFlag()
		{
			var root = NewTestRoot();
			Directory.CreateDirectory(root.Path);

			var googleRoot = new RaiPath((root / "GoogleDriveCustom").Path);
			var projectDir = googleRoot / "Mzansi" / "JsonPit";
			var metadataDir = googleRoot / ".dropbox" / "cache";
			Directory.CreateDirectory(projectDir.Path);
			Directory.CreateDirectory(metadataDir.Path);

			var env = new EnvScope(new Dictionary<string, string?>
			{
				["OSLIB_CLOUD_ROOT_DROPBOX"] = null,
				["OSLIB_CLOUD_ROOT_ONEDRIVE"] = null,
				["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = googleRoot.Path,
				["OSLIB_CLOUD_ROOT_ICLOUD"] = null,
				["OSLIB_CLOUD_CONFIG"] = null
			});

			try
			{
				ResetOsCaches();

				var projectFile = new RaiFile(projectDir.Path + "project.json");
				var metadataFile = new RaiFile(metadataDir.Path + "state.db");

				Assert.True(projectFile.Cloud);
				Assert.False(metadataFile.Cloud);
			}
			finally
			{
				env.Dispose();
				ResetOsCaches();
				Cleanup(root);
			}
		}

		private sealed class EnvScope : IDisposable
		{
			private readonly Dictionary<string, string?> before = new();

			public EnvScope(Dictionary<string, string?> vars)
			{
				foreach (var kvp in vars)
				{
					before[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
					Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
				}
			}

			public void Dispose()
			{
				foreach (var kvp in before)
					Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
			}
		}
	}
}