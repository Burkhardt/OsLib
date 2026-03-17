using System;
using System.Diagnostics;
using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudRemoteSyncTests
{
	[Theory]
	[InlineData(CloudStorageType.GoogleDrive)]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	public void TextFile_SyncsWithMzansi(CloudStorageType provider)
	{
		Console.WriteLine(Os.GetCloudConfigurationDiagnosticReport(refresh: true));
		Console.WriteLine(Os.GetRemoteTestConfigurationDiagnosticReport(refresh: true));

		if (!RemoteCloudSyncProbe.TryCreate(provider, "mzansi", out var probe, out var reason))
			Assert.Skip(reason + Environment.NewLine + Os.GetCloudConfigurationDiagnosticReport() + Environment.NewLine + Os.GetRemoteTestConfigurationDiagnosticReport());

		var providerKey = provider.ToString().ToLowerInvariant();
		var relativeDir = $"RAIkeep/oslib-remote-sync-tests/{providerKey}/sample/";
		var relativeFile = relativeDir + "sample.txt";
		var localDir = probe.LocalCloudRoot / "RAIkeep" / "oslib-remote-sync-tests" / providerKey / "sample";
		var localFile = new TextFile(localDir, "sample.txt");
		var remoteDir = probe.GetRemoteDirectory(relativeDir);
		var remoteFile = new RaiFile(probe.GetRemoteFullName(relativeFile));
		var keepArtifactsForInspection = false;

		try
		{
			if (localDir.Exists())
				new RaiFile(localDir.Path).rmdir(depth: 10, deleteFiles: true);

			Assert.True(
				probe.Observer.WaitForMissing(remoteDir.Path, TimeSpan.FromMinutes(2), out _),
				$"Remote baseline directory did not vanish after local cleanup. {probe.LastFailure}");

			var createTimer = Stopwatch.StartNew();
			localFile.Append("alpha").Save();
			createTimer.Stop();

			Assert.True(localFile.Cloud);
			Assert.True(localFile.Exists());
			Assert.True(probe.Observer.WaitForFileContainingAll(remoteFile.FullName, TimeSpan.FromMinutes(2), out var createSeen, "alpha"), probe.LastFailure);

			var updateTimer = Stopwatch.StartNew();
			localFile.DeleteAll().Append("gamma").Append("delta").Save();
			updateTimer.Stop();

			Assert.True(probe.Observer.WaitForFileContainingAll(remoteFile.FullName, TimeSpan.FromMinutes(2), out var updateSeen, "gamma", "delta"), probe.LastFailure);
			Assert.DoesNotContain("alpha", probe.Observer.ReadFile(remoteFile.FullName));

			var deleteTimer = Stopwatch.StartNew();
			localFile.rm();
			deleteTimer.Stop();
			var localExistsAfterDelete = localFile.Exists();

			var deletePropagated = probe.Observer.WaitForMissing(remoteFile.FullName, TimeSpan.FromMinutes(2), out var deleteSeen);
			if (!deletePropagated)
			{
				keepArtifactsForInspection = true;
				var remoteFileExists = probe.Observer.FileExists(remoteFile.FullName);
				var remoteFileState = probe.Observer.DescribePathState(remoteFile.FullName);
				var remoteDirListing = probe.Observer.ListDirectory(remoteDir.Path);
				throw new Xunit.Sdk.XunitException(
					$"Delete did not propagate to Mzansi within timeout. localExistsAfterDelete={localExistsAfterDelete}; remoteFileExists={remoteFileExists}; remoteFile='{remoteFile.FullName}'; remoteDir='{remoteDir.Path}'.\n" +
					$"Remote file state:\n{remoteFileState}\n\nRemote directory listing:\n{remoteDirListing}\n\nLast failure: {probe.LastFailure}");
			}

			Console.WriteLine($"{provider} remote sync via Mzansi: create-local={createTimer.ElapsedMilliseconds}ms create-remote={createSeen.TotalMilliseconds:F0}ms update-local={updateTimer.ElapsedMilliseconds}ms update-remote={updateSeen.TotalMilliseconds:F0}ms delete-local={deleteTimer.ElapsedMilliseconds}ms delete-remote={deleteSeen.TotalMilliseconds:F0}ms remote={probe.SshTarget}");
		}
		finally
		{
			if (!keepArtifactsForInspection)
			{
				try
				{
					if (localDir.Exists())
						new RaiFile(localDir.Path).rmdir(depth: 10, deleteFiles: true);
				}
				catch
				{
				}
			}
			else Console.WriteLine($"Preserving local and remote run directory for inspection: local='{localDir.Path}' remote='{remoteDir.Path}'");
		}
	}
}