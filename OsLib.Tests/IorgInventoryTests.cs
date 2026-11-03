using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OsLib.Tests;

public sealed class IorgInventoryTests
{
	[Fact]
	public void CloudRelativeInventoryAndDuplicatesAreResolvedOnlyByRemoteHost()
	{
		var command = new CaptureIorg();
		var request = IorgListRequest.ForCloud("*.mov", "ICloudDrive") with
		{ Long = true, Json = true, IgnoreCase = true, Options = new() { CloudProvider = "ICloudDrive", RemoteTarget = "rsb@ingwe" } };
		var tokens = command.BuildListArguments(request);
		Assert.Equal(".", tokens[tokens.ToList().IndexOf("--root") + 1]);
		Assert.Contains("--long", tokens); Assert.Contains("--ignore-case", tokens);
		command.List(request);
		Assert.Equal("rsb@ingwe", command.Target);
		Assert.Contains("--root .", command.Command);
		Assert.DoesNotContain(Environment.CurrentDirectory, command.Command);
		command.Duplicates(IorgDuplicateRequest.ForCloud("PLVanguard/IMG_5443.MOV", "ICloudDrive") with
		{ Match = "visual", VisualProfile = "strict-v1", Json = true, Options = request.Options });
		Assert.Contains("duplicates", command.Command); Assert.Contains("strict-v1", command.Command);
	}
	[Fact]
	public void MetadataOptionsRejectInvalidContractsBeforeExecution()
	{
		var command = new IorgCommand(); var request = IorgListRequest.ForCloud("*", "OneDrive");
		Assert.Throws<ArgumentException>(() => command.BuildListArguments(request with { Long = true }));
		Assert.Throws<ArgumentException>(() => command.BuildListArguments(request with { Long = true, Json = true, AllFiles = true, Checksum = "sha256" }));
		Assert.Throws<ArgumentException>(() => command.BuildDuplicateArguments(IorgDuplicateRequest.ForCloud("*", "OneDrive", "../outside")));
		Assert.Throws<ArgumentException>(() => command.BuildDuplicateArguments(IorgDuplicateRequest.ForCloud("*", "OneDrive") with { Match = "visual" }));
	}
	[Fact]
	public async Task BinaryTransportStreamsOutputAndFailsOnNonzeroExitAndCancellation()
	{
		if (OperatingSystem.IsWindows()) return;
		var data = await new RaiSystem("sh", new[] { "-c", "printf 'binary-data'" }).ExecBinaryAsync(async (stream, ct) =>
		{
			using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true); return await reader.ReadToEndAsync(ct);
		});
		Assert.Equal("binary-data", data);
		await Assert.ThrowsAsync<InvalidOperationException>(() => new RaiSystem("sh", new[] { "-c", "printf 'decoder failed' >&2; exit 7" })
			.ExecBinaryAsync((stream, ct) => Task.FromResult(0)));
		using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RaiSystem("sh", new[] { "-c", "sleep 30" })
			.ExecBinaryAsync((stream, ct) => Task.FromResult(0), cancelled.Token));
	}
	private sealed class CaptureIorg : IorgCommand
	{
		public string Target = "", Command = "";
		protected override RaiSystemResult ExecuteRemoteCommand(string remoteTarget, string remoteCommand, int timeoutMilliseconds)
		{
			Target = remoteTarget; Command = remoteCommand;
			return new() { ExitCode = 0, StandardOutput = "[]" };
		}
	}
}
