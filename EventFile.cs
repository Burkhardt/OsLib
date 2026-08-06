using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	/// <summary>
	/// One immutable canonical-JSON audit event file (CR003, coordinated v3.13.2).
	/// <para>
	/// The caller supplies the owning root path, a logical filename stem, and the event
	/// content as a <see cref="JObject"/> (or a dynamic object that serializes to one
	/// top-level JSON object). OsLib imposes no event-content schema: it canonicalizes
	/// the supplied object, computes the full lowercase SHA-256 of the canonical UTF-8
	/// content, and writes <c>{LogicalStem}_{Sha256}.event</c> under
	/// <c>rootPath / <see cref="EventDirectory.Name"/></c>.
	/// </para>
	/// <para>
	/// Each <c>.event</c> file begins with <c>{</c>, ends with <c>}</c>, and contains
	/// exactly one event object. The same logical stem and canonical content resolve to
	/// the same path and are an idempotent success. The same stem with different content
	/// resolves to a different hash-derived filename. If a hash-derived path already
	/// contains different bytes, the existing artifact is preserved and the new event
	/// receives an additional collision nonce; collision handling never interrupts the
	/// operation being audited.
	/// </para>
	/// </summary>
	public class EventFile : TextFile
	{
		/// <summary>Preferred extension for event files.</summary>
		public const string Extension = "event";

		/// <summary>Logical stem supplied by the caller (without hash or extension).</summary>
		public string LogicalStem { get; }

		/// <summary>Full lowercase SHA-256 of the canonical UTF-8 event content.</summary>
		public string ContentSha256 { get; }

		/// <summary>The canonical compact JSON content of this event.</summary>
		public string CanonicalContent { get; }

		/// <summary>
		/// Creates and immediately writes (create-once) an event file for
		/// <paramref name="content"/> under <c>rootPath / Events</c>, following the
		/// <see cref="TextFile"/> constructor-with-content convention.
		/// </summary>
		/// <param name="rootPath">The owning root; the <c>Events</c> child is derived internally.</param>
		/// <param name="logicalStem">Caller-supplied logical filename stem.</param>
		/// <param name="content">Exactly one top-level JSON object.</param>
		public EventFile(RaiPath rootPath, string logicalStem, JObject content)
			: base(EventsPath(rootPath), FileNameFor(logicalStem, content, out var canonical, out var sha), ext: Extension)
		{
			LogicalStem = logicalStem;
			CanonicalContent = canonical;
			ContentSha256 = sha;
			Lines = new List<string> { CanonicalContent };
			Write();
		}

		/// <summary>
		/// Creates an event file from a dynamic object that serializes to one top-level JSON object.
		/// </summary>
		public EventFile(RaiPath rootPath, string logicalStem, object content)
			: this(rootPath, logicalStem, ToSingleObject(content))
		{
		}

		private static RaiPath EventsPath(RaiPath rootPath)
		{
			if (rootPath is null) throw new ArgumentNullException(nameof(rootPath));
			return rootPath / EventDirectory.Name;
		}

		private static string FileNameFor(string logicalStem, JObject content, out string canonical, out string sha)
		{
			if (string.IsNullOrWhiteSpace(logicalStem))
				throw new ArgumentException("A logical filename stem is required.", nameof(logicalStem));
			if (content is null) throw new ArgumentNullException(nameof(content));
			(canonical, sha) = CanonicalJson.CanonicalizeWithHash(content);
			return $"{logicalStem}_{sha}";
		}

		internal static JObject ToSingleObject(object content)
		{
			if (content is null) throw new ArgumentNullException(nameof(content));
			if (content is JObject jObject) return jObject;
			if (content is JToken token)
				throw new ArgumentException($"An event must be one top-level JSON object; got {token.Type}.", nameof(content));
			return JObject.FromObject(content);
		}

		/// <summary>
		/// Writes the event with create-once semantics and returns the file that now
		/// durably contains this event's canonical content.
		/// <list type="bullet">
		/// <item>Path absent → the event is written (first write creates the <c>Events</c> child).</item>
		/// <item>Path present with identical bytes → idempotent success, nothing rewritten.</item>
		/// <item>Path present with different bytes → the existing artifact is preserved and this
		/// event is written to a nonce-suffixed sibling path instead.</item>
		/// </list>
		/// </summary>
		public EventFile Write()
		{
			mkdir();
			if (Exists())
			{
				string existing;
				try { existing = File.ReadAllText(FullName, new UTF8Encoding(false)); }
				catch (IOException) { existing = null; }
				if (existing == CanonicalContent)
				{
					Changed = false;
					return this; // idempotent success
				}
				// Hash-derived path holds different bytes — preserve it, move to a nonce sibling.
				Name = $"{Name}-{Guid.NewGuid().ToString("N")[..8]}";
				return Write();
			}
			File.WriteAllText(FullName, CanonicalContent, new UTF8Encoding(false));
			AwaitMaterializing(true);
			Changed = false;
			return this;
		}
	}

	/// <summary>
	/// Static aggregator over the opinionated <c>Events</c> child directory of an owning
	/// root path (CR003, coordinated v3.13.2). It has no instances, no cache, and no
	/// refresh method: every call freshly enumerates <c>*.event</c>.
	/// </summary>
	public static class EventDirectory
	{
		/// <summary>Name of the opinionated events child directory.</summary>
		public const string Name = "Events";

		/// <summary>
		/// Returns a newly constructed dictionary using the complete event filename as key
		/// and that file's unchanged parsed object as value. Content fields are not
		/// interpreted; the event schema belongs to the producer (JsonPit / pits).
		/// <para>
		/// A missing <c>Events</c> child returns an empty dictionary and is never created
		/// by reading. A temporarily incomplete, unparseable, or hash-invalid file is
		/// omitted individually from the current result — it neither fails the whole read
		/// nor hides valid events, and it is reconsidered automatically on the next call.
		/// </para>
		/// </summary>
		public static Dictionary<string, JObject> Events(RaiPath rootPath)
		{
			if (rootPath is null) throw new ArgumentNullException(nameof(rootPath));
			var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
			var eventsPath = rootPath / Name;
			if (!eventsPath.Exists()) return result;
			foreach (var file in eventsPath.EnumerateFiles($"*.{EventFile.Extension}"))
			{
				try
				{
					var text = File.ReadAllText(file.FullName, new UTF8Encoding(false));
					if (!IsHashValid(file.Name, text)) continue;
					var trimmed = text.Trim();
					if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}') continue;
					var parsed = JObject.Parse(trimmed);
					result[file.NameWithExtension] = parsed;
				}
				catch (Exception)
				{
					// Individually omitted: incomplete/unreadable/unparseable right now.
				}
			}
			return result;
		}

		/// <summary>
		/// Validates that the trailing hash segment of an event filename (without
		/// extension) matches the SHA-256 of the file's exact content. A collision nonce
		/// suffix of the form <c>-{nonce}</c> after the hash is tolerated.
		/// </summary>
		internal static bool IsHashValid(string fileNameWithoutExtension, string content)
		{
			var lastSeparator = fileNameWithoutExtension.LastIndexOf('_');
			if (lastSeparator < 0 || lastSeparator == fileNameWithoutExtension.Length - 1) return false;
			var hashSegment = fileNameWithoutExtension[(lastSeparator + 1)..];
			var nonceSeparator = hashSegment.IndexOf('-');
			if (nonceSeparator >= 0) hashSegment = hashSegment[..nonceSeparator];
			if (hashSegment.Length != 64 || !hashSegment.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
			return CanonicalJson.Sha256Hex(content) == hashSegment;
		}
	}
}
