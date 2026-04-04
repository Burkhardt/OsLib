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
		[InlineData(Cloud.Dropbox)]
		[InlineData(Cloud.OneDrive)]
		[InlineData(Cloud.GoogleDrive)]
		public void RaiFile_RoundTrip_WorksAgainstRealWritableCloudProvider(Cloud provider)
		{
			using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
			var root = PrepareWritableIntegrationRoot(provider, out var providerRoot);

			try
			{
				var incomingDir = root / "incoming";
				var archiveDir = root / "archive";
				incomingDir.mkdir();
				archiveDir.mkdir();

				var source = new RaiFile(incomingDir, "source.txt");
				var copy = new RaiFile(incomingDir, "copy.txt");
				var moved = new RaiFile(archiveDir, "moved.txt");
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
		[InlineData(Cloud.Dropbox)]
		[InlineData(Cloud.OneDrive)]
		[InlineData(Cloud.GoogleDrive)]
		public void TextFile_SaveAndRead_WorksAgainstRealWritableCloudProvider(Cloud provider)
		{
			using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
			var root = PrepareWritableIntegrationRoot(provider, out var providerRoot);

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

		private static RaiPath PrepareWritableIntegrationRoot(Cloud provider, out string providerRoot)
		{
			var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "oslib-cloud-integration-tests", out providerRoot);

			try
			{
				Cleanup(root);
				root.mkdir();
				return root;
			}
			catch (UnauthorizedAccessException ex)
			{
				throw new UnauthorizedAccessException($"Configured provider root is not writable: {root.Path}. {ex.Message}", ex);
			}
			catch (IOException ex)
			{
				throw new IOException($"Configured provider root is not writable: {root.Path}. {ex.Message}", ex);
			}
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