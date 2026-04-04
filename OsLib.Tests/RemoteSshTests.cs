using System;
using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class RemoteSshTests
{
	[Fact]
	public void Configured_RemoteObserver_Ssh_Readiness_Probe_Works()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();

		if (!CloudStorageRealTestEnvironment.TryGetReachableRemoteObserver(out var observer, out var reason))
			Assert.Skip(reason);

		var probe = new SshFileProbe(observer.SshTarget);
		var result = probe.ExecuteScript("printf ready");

		Assert.Equal(0, result.ExitCode);
		Assert.Equal("ready", result.StandardOutput.Trim());
	}

	[Fact]
	public void Configured_RemoteObserver_GoogleDrive_Root_Is_Readable_From_Remote_OsConfig()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();

		if (!CloudStorageRealTestEnvironment.TryCreateRemoteCloudSyncProbe(Cloud.GoogleDrive, out var observer, out var probe, out var reason))
			Assert.Skip(reason);

		Assert.True(probe.Observer.DirectoryExists(probe.RemoteCloudRoot.Path), probe.LastFailure);
		Console.WriteLine($"Validated remote {Cloud.GoogleDrive} root via observer '{observer.Name}' at {observer.SshTarget}");
	}
}