using System;
using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class RemoteSshTests
{
	[Fact]
	public void Mzansi_Ssh_Readiness_Probe_Works()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();

		string sshTarget;
		try
		{
			sshTarget = Os.GetObserverSshTarget("mzansi");
		}
		catch (InvalidOperationException ex)
		{
			Assert.Skip(ex.Message);
			return;
		}

		var probe = new SshFileProbe(sshTarget);
		var result = probe.ExecuteScript("printf ready");

		Assert.Equal(0, result.ExitCode);
		Assert.Equal("ready", result.StandardOutput.Trim());
	}

	[Fact]
	public void Mzansi_GoogleDrive_Root_Is_Readable_From_Remote_OsConfig()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();

		RemoteCloudSyncProbe probe;
		try
		{
			if (!RemoteCloudSyncProbe.TryCreate(Cloud.GoogleDrive, "mzansi", out probe, out var reason))
				Assert.Skip(reason);
		}
		catch (InvalidOperationException ex)
		{
			Assert.Skip(ex.Message);
			return;
		}

		Assert.True(probe.Observer.DirectoryExists(probe.RemoteCloudRoot.Path), probe.LastFailure);
	}
}