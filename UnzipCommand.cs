using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib;

/// <summary>Info-ZIP extraction through a cancellable child process on macOS and Linux.</summary>
public sealed class UnzipCommand : CliCommand
{
    public UnzipCommand(string executableName = "unzip") : base(executableName, "unzip") { }

    /// <summary>
    /// Extracts a prevalidated archive into a new temporary directory. The caller must
    /// validate archive paths, entry types and quotas before calling this method.
    /// No shell, interactive input, inherited unzip options, or implicit timeout is used.
    /// A failed/cancelled extraction may leave partial files for the caller to clean up.
    /// </summary>
    public async Task<RaiSystemResult> ExtractAsync(RaiFile archive, RaiPath destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (!archive.Exists()) throw new FileNotFoundException("ZIP archive does not exist.", archive.FullName);
        if (destination.Exists() || File.Exists(destination.FullPath))
            throw new IOException("ZIP extraction requires a new temporary directory.");
        if (!TryResolveExecutable(out var executable))
            throw new FileNotFoundException("ZIP import requires 'unzip' on PATH (Ubuntu: install the unzip package).");
        destination.mkdir();
        return await new RaiSystem(executable, new[] { "-q", "-n", archive.FullName, "-d", destination.FullPath })
        {
            StandardInput = string.Empty,
            EnvironmentOverrides = new Dictionary<string, string>
            {
                ["UNZIP"] = null, ["UNZIPOPT"] = null, ["ZIPINFO"] = null, ["ZIPINFOOPT"] = null
            }
        }.ExecAsync(0, cancellationToken).ConfigureAwait(false);
    }
}
