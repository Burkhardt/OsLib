using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OsLib
{
	/// <summary>One immutable named byte payload stored inside a <see cref="RaiZipFile"/>.</summary>
	public sealed class RaiZipEntry
	{
		public RaiZipEntry(string name, byte[] content)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("A ZIP entry name is required.", nameof(name));
			if (name.Contains('/') || name.Contains('\\') || name is "." or "..")
				throw new ArgumentException("RaiZipFile entries must use one leaf filename.", nameof(name));
			Name = name;
			Content = content?.ToArray() ?? throw new ArgumentNullException(nameof(content));
		}

		public string Name { get; }
		public byte[] Content { get; }

		public static RaiZipEntry FromFile(RaiFile file)
		{
			if (file is null) throw new ArgumentNullException(nameof(file));
			return new RaiZipEntry(
				file.NameWithExtension,
				file.ReadAllBytesAsync().GetAwaiter().GetResult());
		}

		public static RaiZipEntry FromText(string name, string content)
			=> new(name, new UTF8Encoding(false).GetBytes(content ?? string.Empty));
	}

	public enum RaiZipWriteStatus
	{
		Created,
		ExistingIdentical,
		ExistingDifferent,
		ExistingInvalid
	}

	/// <summary>Outcome of an immutable create-once ZIP operation.</summary>
	public sealed record RaiZipWriteResult(RaiZipWriteStatus Status, string Problem = "")
	{
		public bool Validated => Status is RaiZipWriteStatus.Created or RaiZipWriteStatus.ExistingIdentical;
	}

	/// <summary>
	/// Collection-capable create-once ZIP file. The archive is written at its final pathname,
	/// never through <see cref="Os.TempDir"/>, and an existing pathname is never overwritten.
	/// Existing archives are reused only when their complete entry-name/byte set is identical.
	/// </summary>
	public sealed class RaiZipFile : RaiFile
	{
		private static readonly DateTimeOffset StableEntryTime =
			new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

		public RaiZipFile(string fullName) : base(fullName)
		{
			if (!Ext.Equals("zip", StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException("RaiZipFile requires a .zip pathname.", nameof(fullName));
		}

		public RaiZipFile(RaiPath path, string name) : base(path, name, "zip")
		{
		}

		/// <summary>
		/// Creates this archive once at its final pathname. The parent directory must already
		/// exist; this method never creates or replaces a directory or stages outside it.
		/// </summary>
		public RaiZipWriteResult CreateImmutable(IEnumerable<RaiZipEntry> entries)
		{
			var expected = Normalize(entries);
			if (Path is null || !Path.Exists())
				throw new RaiPathNotFoundException(
					$"The ZIP parent directory does not exist: {Path?.FullPath ?? string.Empty}",
					Path?.FullPath ?? string.Empty);

			if (Exists()) return CompareExisting(expected);

			try
			{
				using (var output = new FileStream(FullName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: new UTF8Encoding(false)))
				{
					foreach (var item in expected.Values)
					{
						var entry = archive.CreateEntry(item.Name, CompressionLevel.Optimal);
						entry.LastWriteTime = StableEntryTime;
						using var target = entry.Open();
						target.Write(item.Content, 0, item.Content.Length);
					}
				}
				AwaitMaterializing();
			}
			catch (IOException) when (Exists())
			{
				// Another writer may have won the create-once race. It is reusable only after
				// complete semantic validation below; no overwrite or delete/recreate occurs.
				return CompareExisting(expected);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
			{
				throw new RaiFileIOException($"Unable to create immutable ZIP '{FullName}'.", FullName, exception);
			}

			var validation = CompareExisting(expected);
			if (!validation.Validated)
				return validation;
			return new RaiZipWriteResult(RaiZipWriteStatus.Created);
		}

		/// <summary>Reads every safe leaf entry without extracting anything to the filesystem.</summary>
		public bool TryReadEntries(out IReadOnlyDictionary<string, RaiZipEntry> entries, out string problem)
		{
			var result = new SortedDictionary<string, RaiZipEntry>(StringComparer.Ordinal);
			try
			{
				using var input = new FileStream(FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: new UTF8Encoding(false));
				foreach (var entry in archive.Entries)
				{
					if (string.IsNullOrEmpty(entry.Name) || entry.FullName != entry.Name || result.ContainsKey(entry.Name))
					{
						entries = result;
						problem = $"Archive contains an unsafe or duplicate entry: {entry.FullName}";
						return false;
					}
					using var source = entry.Open();
					using var memory = new MemoryStream();
					source.CopyTo(memory);
					result.Add(entry.Name, new RaiZipEntry(entry.Name, memory.ToArray()));
				}
				entries = result;
				problem = string.Empty;
				return true;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
			{
				entries = result;
				problem = exception.Message;
				return false;
			}
		}

		private RaiZipWriteResult CompareExisting(IReadOnlyDictionary<string, RaiZipEntry> expected)
		{
			if (!TryReadEntries(out var actual, out var problem))
				return new RaiZipWriteResult(RaiZipWriteStatus.ExistingInvalid, problem);
			if (actual.Count != expected.Count)
				return new RaiZipWriteResult(RaiZipWriteStatus.ExistingDifferent, "Archive entry count differs.");

			foreach (var pair in expected)
			{
				if (!actual.TryGetValue(pair.Key, out var current))
					return new RaiZipWriteResult(RaiZipWriteStatus.ExistingDifferent, $"Archive entry is missing: {pair.Key}");
				if (!current.Content.SequenceEqual(pair.Value.Content))
					return new RaiZipWriteResult(
						RaiZipWriteStatus.ExistingDifferent,
						$"Archive entry differs: {pair.Key} (existing {current.Content.Length} bytes, expected {pair.Value.Content.Length} bytes)");
			}
			return new RaiZipWriteResult(RaiZipWriteStatus.ExistingIdentical);
		}

		private static IReadOnlyDictionary<string, RaiZipEntry> Normalize(IEnumerable<RaiZipEntry> entries)
		{
			if (entries is null) throw new ArgumentNullException(nameof(entries));
			var result = new SortedDictionary<string, RaiZipEntry>(StringComparer.Ordinal);
			foreach (var entry in entries)
			{
				if (entry is null) throw new ArgumentException("ZIP entries cannot contain null.", nameof(entries));
				if (!result.TryAdd(entry.Name, entry))
					throw new ArgumentException($"Duplicate ZIP entry name: {entry.Name}", nameof(entries));
			}
			if (result.Count == 0)
				throw new ArgumentException("At least one ZIP entry is required.", nameof(entries));
			return result;
		}
	}
}
