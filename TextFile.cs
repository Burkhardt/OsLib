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
		/// Save the TextFile to disk, including dropbox locations.
		/// </summary>
		/// <param name="backup">With backup == false the wait for materializing is not going to work; only use outside dropbox and alike.</param>
		public TextFile Save(bool backup = false)
		{
			if (Changed || !Exists())
			{
				new RaiFile(FullName).mkdir();
				if (backup)
					this.backup(); // calls AwaitVanishing()
				else
					this.rm(); // calls AwaitVanishing()
				File.WriteAllLines(FullName, (lines == null ? new List<string>() : lines), new UTF8Encoding(false));
				AwaitMaterializing(true);
				Changed = false;
			}
			return this;
		}

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
