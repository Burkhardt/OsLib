using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OsLib;
using Xunit;

namespace OsLib.Tests
{
	[Collection("CloudStorageEnvironment")]
	public class CloudStorageRealWorldIntegrationTests
	{
		[Theory]
		[InlineData(CloudStorageType.Dropbox)]
		[InlineData(CloudStorageType.OneDrive)]
		[InlineData(CloudStorageType.GoogleDrive)]
		[InlineData(CloudStorageType.ICloud)]
		public void RaiFile_RoundTrip_WorksAgainstRealWritableCloudProvider(CloudStorageType provider)
		{
			if (!TryPrepareWritableIntegrationRoot(provider, out var root, out var providerRoot, out var reason))
				Assert.Skip($"Provider {provider}: {reason}. {Os.GetCloudStorageSetupGuidance()}");

			try
			{
				var incomingDir = root / "incoming";
				var archiveDir = root / "archive";
				incomingDir.mkdir();
				archiveDir.mkdir();

				var source = new RaiFile("source.txt") { Path = incomingDir.Path };
				var copy = new RaiFile("copy.txt") { Path = incomingDir.Path };
				var moved = new RaiFile("moved.txt") { Path = archiveDir.Path };
				var sourceText = new TextFile(source.FullName);
				var copyText = new TextFile(copy.FullName);
				var movedText = new TextFile(moved.FullName);

				var createTimer = Stopwatch.StartNew();
				sourceText.Append("alpha");
				sourceText.Save();
				createTimer.Stop();

				Assert.True(source.Cloud);
				Assert.True(source.Exists());
				Assert.Single(sourceText.Read());
				Assert.Equal("alpha", sourceText.Read()[0]);

				var copyTimer = Stopwatch.StartNew();
				copy.cp(source);
				copyTimer.Stop();

				Assert.True(copy.Cloud);
				Assert.True(copy.Exists());
				Assert.Equal("alpha", copyText.Read()[0]);

				var moveTimer = Stopwatch.StartNew();
				moved.mv(source, replace: true, keepBackup: false);
				moveTimer.Stop();

				Assert.False(source.Exists());
				Assert.True(moved.Cloud);
				Assert.True(moved.Exists());
				Assert.Equal("alpha", movedText.Read()[0]);

				var deleteTimer = Stopwatch.StartNew();
				copy.rm();
				moved.rm();
				deleteTimer.Stop();

				Assert.False(copy.Exists());
				Assert.False(moved.Exists());

				Console.WriteLine($"Provider {provider}: create={createTimer.ElapsedMilliseconds}ms copy={copyTimer.ElapsedMilliseconds}ms move={moveTimer.ElapsedMilliseconds}ms delete={deleteTimer.ElapsedMilliseconds}ms root={providerRoot}");
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Theory]
		[InlineData(CloudStorageType.Dropbox)]
		[InlineData(CloudStorageType.OneDrive)]
		[InlineData(CloudStorageType.GoogleDrive)]
		[InlineData(CloudStorageType.ICloud)]
		public void TextFile_SaveAndRead_WorksAgainstRealWritableCloudProvider(CloudStorageType provider)
		{
			if (!TryPrepareWritableIntegrationRoot(provider, out var root, out var providerRoot, out var reason))
				Assert.Skip($"Provider {provider}: {reason}. {Os.GetCloudStorageSetupGuidance()}");

			try
			{
				var workDir = root / "text-files";
				workDir.mkdir();

				var textFile = new TextFile(workDir.Path + "sample.txt");
				textFile.Append("first");
				textFile.Append("second");

				var saveTimer = Stopwatch.StartNew();
				textFile.Save();
				saveTimer.Stop();

				var readTimer = Stopwatch.StartNew();
				var reloaded = new TextFile(textFile.FullName);
				var lines = reloaded.Read();
				readTimer.Stop();

				Assert.True(textFile.Cloud);
				Assert.True(reloaded.Cloud);
				Assert.Equal(2, lines.Count);
				Assert.Equal("first", lines[0]);
				Assert.Equal("second", lines[1]);

				Console.WriteLine($"Provider {provider}: text-save={saveTimer.ElapsedMilliseconds}ms text-read={readTimer.ElapsedMilliseconds}ms file={textFile.FullName} root={providerRoot}");
			}
			finally
			{
				Cleanup(root);
			}
		}

		private static bool TryPrepareWritableIntegrationRoot(CloudStorageType provider, out RaiPath root, out string providerRoot, out string reason)
		{
			Os.ResetCloudStorageCache();
			providerRoot = Os.GetCloudStorageRoot(provider, refresh: true);
			if (string.IsNullOrWhiteSpace(providerRoot))
			{
				root = new RaiPath(Os.TempDir) / "RAIkeep" / "missing-cloud-root";
				reason = "provider root is not configured or not discoverable on this machine";
				return false;
			}

			providerRoot = new RaiPath(providerRoot).Path;
			if (!Directory.Exists(providerRoot))
			{
				root = new RaiPath(providerRoot) / "RAIkeep" / "oslib-cloud-integration-tests" / provider.ToString();
				reason = $"provider root does not exist: {providerRoot}";
				return false;
			}

			root = new RaiPath(providerRoot) / "RAIkeep" / "oslib-cloud-integration-tests" / provider.ToString();
			reason = string.Empty;

			try
			{
				Cleanup(root);
				root.mkdir();
				return true;
			}
			catch (UnauthorizedAccessException ex)
			{
				reason = $"root is not writable: {ex.Message}";
			}
			catch (IOException ex)
			{
				reason = $"root is not writable: {ex.Message}";
			}

			return false;
		}

		private static void Cleanup(RaiPath root)
		{
			try
			{
				if (Directory.Exists(root.Path))
					new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Cleanup warning for {root.Path}: {ex.Message}");
			}
		}
	}
}