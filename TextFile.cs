using System;
using System.Collections.Generic;
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

		public void Append(string line)
		{
			if (lines == null)
				Read();
			if (lines.Count == 1 && lines[0].Length == 0)
				lines[0] = line;
			else
				lines.Add(line);
			Changed = true;
		}

		public void Insert(int beforeLine, string line)
		{
			Lines.Insert(beforeLine, line);
			Changed = true;
		}

		public void Delete(int line)
		{
			Lines.RemoveAt(line);
			Changed = true;
		}

		public void DeleteAll()
		{
			lines = new List<string>();
			Append("");
			Changed = true;
		}

		public void Sort(bool reverse = false)
		{
			var lineArray = Lines.ToArray();
			Array.Sort(lineArray);
			if (reverse)
				Array.Reverse(lineArray);
			this.lines = new List<string>(lineArray);
			Changed = true;
		}

		public List<string> Read()
		{
			lines = File.Exists(FullName) ? new List<string>(File.ReadAllLines(FullName)) : new List<string>();
			Changed = false;
			return Lines;
		}

		/// <summary>
		/// Save the TextFile to disk, including dropbox locations.
		/// </summary>
		/// <param name="backup">With backup == false the wait for materializing is not going to work; only use outside dropbox and alike.</param>
		public void Save(bool backup = false)
		{
			if (Changed || !Exists())
			{
				new RaiFile(FullName).mkdir();
				if (backup)
					this.backup(); // calls AwaitVanishing()
				else
					this.rm(); // calls AwaitVanishing()
				File.WriteAllLines(FullName, (lines == null ? new List<string>() : lines), Encoding.UTF8);
				AwaitMaterializing(true);
				Changed = false;
			}
		}

		public TextFile(string name)
			: base(name)
		{
		}
	}
}
