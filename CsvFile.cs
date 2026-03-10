using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	/// <summary>
	/// Current settings: tab as field separator, field values not quoted.
	/// </summary>
	public class CsvFile : TextFile
	{
		// UndefinedNumber and IsNumber now in DataImage, Extensions.cs
		public int mv(CsvFile src, bool replace = false, bool keepBackup = false) => mv((RaiFile)src, replace, keepBackup);

		private char[] fieldSplitter;
		private bool replaceBlanks = false;
		private Dictionary<string, int> Idx = new Dictionary<string, int>();

		public void AdjustColumnSelectors()
		{
			string line0 = replaceBlanks
				? string.Join(fieldSplitter[0].ToString(), Lines[0].Split(fieldSplitter, StringSplitOptions.RemoveEmptyEntries))
				: Lines[0];
			var q = from fieldName in line0.Split(fieldSplitter) select fieldName.Trim();
			string[] fields = q.ToArray();
			for (int i = 0; i < fields.Length; i++)
				Idx[fields[i]] = i;
		}

		public string[] FieldNames()
		{
			return (from _ in Idx orderby _.Value select _.Key).ToArray();
		}

		public List<JObject> Objects()
		{
			// TODO: combine several keys into one field Key
			var list = new List<JObject>();
			for (int i = 1; i < Lines.Count; i++)
				list.Add(Object(i));
			return list;
		}

		/// <summary>
		/// Get csv row as object.
		/// </summary>
		/// <param name="idx">Data starts at index 1.</param>
		public JObject Object(int idx)
		{
			var obj = new JObject();
			double d = 0.0;
			long l = 0;
			foreach (var elem in this[idx])
			{
				if (elem.Value.Contains("."))
				{
					if (double.TryParse(elem.Value, out d))
						obj[elem.Key] = d;
				}
				else if (long.TryParse(elem.Value, out l))
				{
					obj[elem.Key] = l;
				}
				else
				{
					obj[elem.Key] = elem.Value;
				}
			}
			return obj;
		}

		public new Dictionary<string, string> this[int i]
		{
			get
			{
				var result = new Dictionary<string, string>();
				if (i < 0 || i >= Lines.Count)
					return result;
				var line = replaceBlanks
					? string.Join(fieldSplitter[0].ToString(), Lines[i].Split(fieldSplitter, StringSplitOptions.RemoveEmptyEntries))
					: Lines[i];
				var fields = line.Split(fieldSplitter);
				foreach (var field in Idx)
					result.Add(field.Key, fields[field.Value]);
				return result;
			}
			set
			{
				Changed = true;
				throw new NotImplementedException();
			}
		}

		private void FixLineFeedsWithinFields()
		{
			for (int i = 1; i < Lines.Count; i++)
			{
				var line = replaceBlanks
					? string.Join(fieldSplitter[0].ToString(), Lines[i].Split(fieldSplitter, StringSplitOptions.RemoveEmptyEntries))
					: Lines[i];
				var fields = line.Split(fieldSplitter);
				if (fields.Length != Idx.Count)
				{
					while (line.Split(fieldSplitter).Length < Idx.Count && i < (Lines.Count - 1))
					{
						Lines[i] = line + Lines[i + 1];
						Delete(i + 1);
					}
					if (line.Split(fieldSplitter).Length != Idx.Count)
						Delete(i);
				}
			}
		}

		/// <summary>
		/// Read a csv file into memory.
		/// </summary>
		/// <returns>Number of rows without the header line.</returns>
		public int Read(string externalFieldNames = null, bool replaceBlanks = false)
		{
			this.replaceBlanks = replaceBlanks;
			base.Read();
			if (externalFieldNames != null)
				Insert(0, externalFieldNames);
			AdjustColumnSelectors();
			FixLineFeedsWithinFields();
			return Lines.Count - 1;
		}

		public void ToJsonFile(string destFileName = null)
		{
			var fName = string.IsNullOrEmpty(destFileName) ? new RaiFile(FullName) : new RaiFile(destFileName);
			fName.Ext = "json";
			var jsonFile = new TextFile(fName.FullName);
			jsonFile.rm();
			var fieldNames = FieldNames();
			Dictionary<string, string> item = null;
			string line;
			string value;
			long l;
			double d;
			jsonFile.Append("[");
			for (int i = 1; i < Lines.Count; i++)
			{
				line = "{";
				item = this[i];
				foreach (string name in fieldNames)
				{
					value = item[name];
					if (!(long.TryParse(value, out l) || double.TryParse(value, out d)))
						value = "\"" + value + "\"";
					line += $"\"{name}\": {value},";
				}
				jsonFile.Append(line.Substring(0, line.Length - 1) + "},");
			}
			int llNr = jsonFile.Lines.Count - 1;
			var lastLine = jsonFile.Lines[llNr];
			jsonFile.Delete(llNr);
			jsonFile.Append(lastLine.Substring(0, lastLine.Length - 1) + "]");
			jsonFile.Save();
		}

		public CsvFile(string name, char seperator = '\t')
			: base(name)
		{
			fieldSplitter = new char[] { seperator };
		}
	}
}
