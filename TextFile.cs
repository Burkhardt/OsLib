using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace OsLib
{
	public class TextFile : RaiFile
	{
		public int mv(TextFile src, bool replace = false, bool keepBackup = false) => mv((RaiFile)src, replace, keepBackup);

		/// <summary>
		/// Holds info if anything has changed in memory since last read.
		/// </summary>
		public bool Changed { get; set; }

		private List<string> lines;
		public List<string> Lines
		{
			get
			{
				return lines == null ? Read() : lines;
			}
			set { lines = value; }
		}

		/// <summary>
		/// List automatically extends according to List.AddRange behavior.
		/// </summary>
		public string this[int i]
		{
			get
			{
				return Lines[i];
			}
			set
			{
				if (Lines.Capacity < i + 1)
					Lines.AddRange(Enumerable.Range(Lines.Count, i - Lines.Count + 1).Select(x => ""));
				Lines[i] = value;
				Changed = true; // even if Lines[i] had the same value before already
			}
		}

		public TextFile Append(string line)
		{
			if (lines == null)
				Read();
			if (lines.Count == 1 && lines[0].Length == 0)
				lines[0] = line;
			else
				lines.Add(line);
			Changed = true;
			return this;
		}

		public TextFile Insert(int beforeLine, string line)
		{
			Lines.Insert(beforeLine, line);
			Changed = true;
			return this;
		}

		public TextFile Delete(int line)
		{
			Lines.RemoveAt(line);
			Changed = true;
			return this;
		}

		public TextFile DeleteAll()
		{
			lines = new List<string>();
			Append("");
			Changed = true;
			return this;
		}

		public TextFile Sort(bool reverse = false)
		{
			var lineArray = Lines.ToArray();
			Array.Sort(lineArray);
			if (reverse)
				Array.Reverse(lineArray);
			this.lines = new List<string>(lineArray);
			Changed = true;
			return this;
		}

		public List<string> Read()
		{
			lines = Exists() ? new List<string>(File.ReadAllLines(FullName)) : new List<string>();
			Changed = false;
			return Lines;
		}
		/// <summary>
		/// reads directly from disk into the first line of Lines,
		/// replacing the in-memory cache
		/// is faster if you want to read the entire content of a file and need it as a single string
		/// rather than line by line with direct access to the Lines.
		/// Falls back to reading from Lines if only memory representation is available,
		/// which is also the behavior for the second call to ReadAllText().
		/// </summary>
		/// <returns>string containing the entire content of the file</returns>
		public string ReadAllText()
		{
			var exists = Exists();
			if (exists)
			{
				Lines = new List<string>() { File.ReadAllText(FullName) };
				Changed = true;
				return Lines[0];
			}
			if (Lines.Count == 0)
				return string.Empty;
			return string.Join("\n", Lines);
		}

		/// <summary>
		/// Save the TextFile to disk with the no-delete persistence contract
		/// (CR003, coordinated v3.13.2).
		/// <para>
		/// <c>Save(backup: false)</c> creates the pathname when absent or truncates and
		/// writes the existing pathname directly in place. It never deletes, renames,
		/// or replaces the original pathname through a temporary file, so a concurrent
		/// reader in another process never observes the pathname disappearing.
		/// </para>
		/// <para>
		/// <c>Save(backup: true)</c> first <em>copies</em> the previous content to the
		/// configured backup location (<see cref="Os.LocalBackupDir"/>) and then
		/// overwrites the original pathname in place. Backup copies rather than moves,
		/// so the original pathname never disappears either.
		/// </para>
		/// <para>
		/// In-place writing is not atomic reader visibility: a reader may observe
		/// partially written content. Callers that need consistent snapshots (JsonPit)
		/// validate a complete candidate read and retry transient failures.
		/// </para>
		/// </summary>
		/// <param name="backup">Copy the previous content to the backup location before overwriting.</param>
		public TextFile Save(bool backup = false)
		{
			if (Changed || !Exists())
			{
				new RaiFile(FullName).mkdir();
				if (backup)
					this.backup(copy: true); // copy, never move — the original pathname must not disappear
				File.WriteAllLines(FullName, (lines == null ? new List<string>() : lines), new UTF8Encoding(false));
				AwaitMaterializing(true);
				Changed = false;
			}
			return this;
		}

		/// <summary>
		/// Retained for patch-release source and binary compatibility.
		/// Since v3.13.2 <see cref="Save(bool)"/> itself writes in place without a
		/// delete/recreate cycle; this method delegates to <c>Save(backup: false)</c>.
		/// </summary>
		public TextFile SaveInPlace() => Save(backup: false);

		public TextFile(string name, string content = null)
			: base(name)
		{
			if (string.IsNullOrEmpty(Ext))
				Ext = "txt";    // default for TextFile
			if (content != null)
			{
				Append(content);
				Changed = true;
				Save();
			}
		}
		/// <summary>
		/// Create a TextFile at path with name and optional content.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="name">"text", "text.txt", "text.ini, ..."</param>
		/// <param name="ext">file extension, default is txt, json, json5 or alike are supported</param>
		/// <param name="content">to add</param>
		public TextFile(RaiPath path, string name, string ext = "txt", string content = null)
			: base(path, name)
		{
			if (string.IsNullOrEmpty(Ext))
				Ext = ext;
			if (content != null)
			{
				Append(content);
				Save();
			}
		}
	}
}
