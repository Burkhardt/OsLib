#nullable enable
using System;
using System.Collections.Generic;

namespace OsLib
{
	/// <summary>Read-only file metadata. All paths and provenance belong to the execution host.</summary>
	public sealed record IorgInventoryItem
	{
		public string Name { get; init; } = string.Empty;
		public string FullPath { get; init; } = string.Empty;
		public string RelativePath { get; init; } = string.Empty;
		public string? CloudRelativePath { get; init; }
		public string? ItemTreePath { get; init; }
		public string Extension { get; init; } = string.Empty;
		public string? MediaKind { get; init; }
		public long SizeBytes { get; init; }
		public string LastModifiedUtc { get; init; } = string.Empty;
		public string? Cloud { get; init; }
		public string ResolvedRoot { get; init; } = string.Empty;
		public string ExecutionHost { get; init; } = string.Empty;
		public string? Sha256 { get; init; }
	}

	/// <summary>Evidence distinguishes confirmed container bytes from a named visual comparison profile.</summary>
	public sealed record IorgMatchEvidence
	{
		public string? Algorithm { get; init; }
		public string? Digest { get; init; }
		public bool ByteComparisonVerified { get; init; }
		public bool AudioCompared { get; init; }
		public string? Profile { get; init; }
		public string? Method { get; init; }
		public double? MeanSsim { get; init; }
		public double? MinimumSsim { get; init; }
		public double? MeanThreshold { get; init; }
		public double? MinimumThreshold { get; init; }
		public long? FramesCompared { get; init; }
		public string? Decoder { get; init; }
		public string? Normalization { get; init; }
	}

	public sealed record IorgMatchResult(IorgInventoryItem File, string MatchMode, IorgMatchEvidence Evidence);

	/// <summary>A literal source returns a match array; a wildcard returns a relative-path dictionary.</summary>
	public sealed record IorgDuplicateRequest(string FileOrPattern, RaiPath? Root)
	{
		/// <summary>Cloud-relative spelling, preserved for resolution by the execution host. Root must be null.</summary>
		public string? RootAddress { get; init; }
		public string Match { get; init; } = "bytes";
		public string? VisualProfile { get; init; }
		public bool IgnoreCase { get; init; }
		public bool Json { get; init; }
		public IorgCommandOptions Options { get; init; } = new();
		public static IorgDuplicateRequest ForCloud(string selector, string cloud, string relativeRoot = ".")
			=> new(selector, null) { RootAddress = relativeRoot, Options = new() { CloudProvider = cloud } };
	}
}
