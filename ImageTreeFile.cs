using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OsLib
{
	/// <summary>
	/// Lightweight color info used by ImageFile/ImageTreeFile naming conventions.
	/// </summary>
	public class ColorInfo
	{
		private static readonly Regex HexColorRegex = new Regex("^#[0-9a-fA-F]{3,12}$", RegexOptions.Compiled);
		private string code = string.Empty;
		public string Code
		{
			get => code;
			set
			{
				if (string.IsNullOrWhiteSpace(value) || !HexColorRegex.IsMatch(value))
					throw new FormatException("colorCode has to start with '#' followed by 3..12 hex digits.");
				code = value;
			}
		}
		public string Name { get; set; }
		public int Count { get; set; }

		public ColorInfo(string colorCode, string colorName = null, int pixelsInThisColor = 0)
		{
			Code = colorCode;
			Name = colorName;
			Count = pixelsInThisColor;
		}
	}
	/// <summary>
	/// Item tree path convention: top/sub folder segments are derived from ItemId prefixes.
	/// </summary>
	public class ItemTreePath : RaiPath
	{
		public string RootPath
		{
			get => rootPath;
			set
			{
				rootPath = NormalizeRootPath(value, ItemId);
				Apply();
			}
		}
		private string rootPath = string.Empty;

		public string ItemId
		{
			get => itemId;
			set
			{
				itemId = string.IsNullOrEmpty(value) ? string.Empty : value;
				rootPath = NormalizeRootPath(rootPath, itemId);
				Apply();
			}
		}
		private string itemId = string.Empty;

		public string Topdir { get; private set; } = string.Empty;
		public string Subdir { get; private set; } = string.Empty;

		private static string NormalizeRootPath(string rootCandidate, string itemId)
		{
			if (string.IsNullOrEmpty(rootCandidate))
				return string.Empty;

			var normalized = new RaiPath(rootCandidate).Path;
			if (string.IsNullOrEmpty(itemId))
				return normalized;

			var top = itemId.Substring(0, Math.Min(itemId.Length, 3));
			if (top.Length == 3 && top.Equals("con", StringComparison.OrdinalIgnoreCase))
				top = "C0N";
			var sub = itemId.Substring(0, Math.Min(itemId.Length, 6));

			var marker = Os.DIRSEPERATOR + top + Os.DIRSEPERATOR + sub;
			var pos = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			return pos >= 0 ? normalized.Remove(pos + 1) : normalized;
		}

		public void Apply()
		{
			Topdir = string.IsNullOrEmpty(ItemId) ? string.Empty : ItemId.Substring(0, Math.Min(ItemId.Length, 3));
			if (Topdir.Length == 3 && Topdir.Equals("con", StringComparison.OrdinalIgnoreCase))
				Topdir = "C0N";
			Subdir = string.IsNullOrEmpty(ItemId) ? string.Empty : ItemId.Substring(0, Math.Min(ItemId.Length, 6));

			var p = RootPath;
			if (!string.IsNullOrEmpty(Topdir))
				p += Topdir + Os.DIRSEPERATOR;
			if (!string.IsNullOrEmpty(Subdir))
				p += Subdir + Os.DIRSEPERATOR;
			Path = p;
		}

		public ItemTreePath(string rootPath, string itemId)
			: base(rootPath)
		{
			this.itemId = string.IsNullOrEmpty(itemId) ? string.Empty : itemId;
			this.rootPath = NormalizeRootPath(rootPath, this.itemId);
			Apply();
		}
	}

	/// <summary>
	/// Image-focused file descriptor that parses and composes names like
	/// ItemId[_Color][_Number][_NameExt][,TileTemplate][-TileNumber].
	/// </summary>
	public class ImageFile : RaiFile
	{
		public const int NoImageNumber = -1;

		private string itemId = string.Empty;
		private string nameExt = string.Empty;
		private int imageNumber = NoImageNumber;
		private string tileTemplate = string.Empty;
		private string tileNumber = string.Empty;

		public virtual string ItemId
		{
			get => itemId;
			set => itemId = string.IsNullOrEmpty(value) ? string.Empty : value;
		}

		public virtual string NameExt
		{
			get => nameExt;
			set => nameExt = string.IsNullOrEmpty(value) ? string.Empty : value;
		}

		public int ImageNumber
		{
			get => imageNumber;
			set => imageNumber = value < 0 ? NoImageNumber : value;
		}

		public string TileTemplate
		{
			get => string.IsNullOrEmpty(tileTemplate) ? string.Empty : tileTemplate;
			set => tileTemplate = value;
		}

		public string TileNumber
		{
			get => string.IsNullOrEmpty(tileNumber) ? string.Empty : tileNumber;
			set => tileNumber = string.IsNullOrEmpty(value) ? string.Empty : value;
		}

		public ColorInfo Color { get; set; }

		public string ShortName
		{
			get
			{
				var n = string.Empty;
				if (!string.IsNullOrEmpty(ItemId))
					n += ItemId;
				if (imageNumber >= 0)
					n += "_" + ImageNumber.ToString("D2");
				return n.Length > 0 ? n : base.Name;
			}
		}

		public override string Name
		{
			get
			{
				var n = string.Empty;
				if (!string.IsNullOrEmpty(ItemId))
					n += ItemId;
				if (Color != null)
					n += "_" + Color.Code.Substring(1);
				if (imageNumber >= 0)
					n += "_" + ImageNumber.ToString("D2");
				if (!string.IsNullOrEmpty(NameExt))
					n += "_" + NameExt;
				if (!string.IsNullOrEmpty(TileTemplate))
					n += "," + TileTemplate;
				if (!string.IsNullOrEmpty(TileNumber))
					n += "-" + TileNumber;
				return n.Length > 0 ? n : base.Name;
			}
			set
			{
				base.Name = value;
				Parse();
			}
		}

		public override string NameWithExtension =>
			string.IsNullOrEmpty(Name) ? string.Empty : Name + (string.IsNullOrEmpty(Ext) ? string.Empty : "." + Ext);

		public override string FullName => Path + NameWithExtension;

		public string BlankToCamelCase(string filename)
		{
			if (filename.Length == 0)
				return filename;
			var array = filename.Split(new[] { ' ', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			for (var i = 1; i < array.Length; i++)
			{
				var x = array[i].ToCharArray();
				x[0] = char.ToUpper(array[i][0]);
				array[i] = new string(x);
			}
			return string.Join("", array);
		}

		protected virtual void Parse()
		{
			base.Name = BlankToCamelCase(
				base.Name
				.Replace("_Film", "Film_")
				.Replace("(", "")
				.Replace(")", ""));

			if (base.Name.ToUpper().StartsWith("WP_20"))
				base.Name = base.Name.Substring(5);
			if (base.Name.ToLower().StartsWith("photo-"))
				base.Name = DateTime.Now.ToString("yyMMdd") + "_" + base.Name.Substring(6);
			if (base.Name.ToLower().StartsWith("photo") || base.Name.ToLower().StartsWith("image"))
				base.Name = DateTime.Now.ToString("yyMMdd") + base.Name.Substring(5);
			if (base.Name.ToUpper().StartsWith("IMG") || base.Name.ToUpper().StartsWith("_MG"))
				base.Name = DateTime.Now.ToString("yyMMdd") + base.Name.Substring(3);

			if (base.Name.StartsWith("20") && base.Name.Length >= 5 && base.Name.Substring(0, 5).Contains('-'))
			{
				var fields = base.Name.Split(new[] { '-', ' ', '.', ':' }, StringSplitOptions.RemoveEmptyEntries);
				if (fields.Length >= 6)
					base.Name = (int.Parse(fields[0]) - 2000).ToString("D2") + fields[1] + fields[2] + fields[3] + "_" + fields[4] + fields[5];
			}

			var csvValues = base.Name.Split(new[] { ',' });
			var parts = csvValues[0].Split(new[] { '_' });

			imageNumber = NoImageNumber;
			Color = null;
			NameExt = string.Empty;

			if (parts.Length == 2)
			{
				if (char.IsLetter(parts[1][0]))
					NameExt = parts[1];
				else
					SetImageNumber(parts[1]);
			}
			else if (parts.Length == 3)
			{
				ColorInfo cInfo = null;
				if (parts[1].Length == 6)
				{
					try { cInfo = new ColorInfo("#" + parts[1]); } catch { cInfo = null; }
				}

				if (cInfo == null)
				{
					SetImageNumber(parts[1]);
					NameExt = BlankToCamelCase(parts[2]);
				}
				else
				{
					Color = cInfo;
					SetImageNumber(parts[2]);
					NameExt = string.Empty;
				}
			}
			else if (parts.Length >= 4)
			{
				ColorInfo cInfo = null;
				if (parts[1].Length == 6)
				{
					try { cInfo = new ColorInfo("#" + parts[1]); } catch { cInfo = null; }
				}

				if (cInfo != null)
				{
					Color = cInfo;
					SetImageNumber(parts[2]);
					NameExt = BlankToCamelCase(parts[3]);
				}
				else
				{
					Color = null;
					SetImageNumber(parts[1]);
					NameExt = BlankToCamelCase(parts[2]);
				}
			}

			ItemId = parts.Length > 0 ? parts[0] : string.Empty;

			if (csvValues.Length > 1)
			{
				tileTemplate = csvValues[1];
				var dash = tileTemplate.IndexOf('-');
				if (dash >= 0)
				{
					var tileNumberString = tileTemplate.Substring(dash + 1);
					var i = 0;
					while (i < tileNumberString.Length && char.IsDigit(tileNumberString[i]))
						i++;
					if (i < tileNumberString.Length)
						tileNumberString = tileNumberString.Remove(i);
					tileNumber = tileNumberString;
					tileTemplate = tileTemplate.Substring(0, dash);
				}
				else
				{
					tileNumber = string.Empty;
				}
			}
			else
			{
				tileTemplate = string.Empty;
				tileNumber = string.Empty;
			}
		}

		public void SetImageNumber(string s)
		{
			if (int.TryParse(s, out var number))
				ImageNumber = number;
			else
				ImageNumber = NoImageNumber;
		}

		public bool ExtendToFirstExistingFile(string extensions, ColorInfo colorInfo = null)
		{
			try
			{
				var itf = new ImageTreeFile(FullName)
				{
					Color = colorInfo ?? new ColorInfo("#0DEAD0"),
					Ext = "*"
				};

				var searchPattern = colorInfo == null
					? itf.NameWithExtension.Replace("_0DEAD0", "*")
					: itf.NameWithExtension;

				var dirEntries = Directory.GetFileSystemEntries(Path, searchPattern);
				var extArray = extensions.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (extArray.Length == 0)
					return false;

				foreach (var dirEntry in dirEntries)
				{
					itf = new ImageTreeFile(dirEntry);
					if (extArray.Contains(itf.Ext, StringComparer.OrdinalIgnoreCase))
					{
						Ext = itf.Ext;
						Color = itf.Color;
						if (ImageNumber == NoImageNumber)
							ImageNumber = itf.ImageNumber;
						return true;
					}
				}
			}
			catch (Exception)
			{
				return false;
			}

			return false;
		}

		public ImageFile(string filename)
			: base(filename)
		{
			ItemId = string.Empty;
			NameExt = string.Empty;
			TileTemplate = string.Empty;
			TileNumber = string.Empty;
			Color = null;
			Parse();
		}
	}

	/// <summary>
	/// ImageFile variant that derives top/subdirectory segments from ItemId.
	/// </summary>
	public class ImageTreeFile : ImageFile, IPathConventionFile
	{
		private ItemTreePath itemTreePath = null;
		public PathConventionType ConventionName => PathConventionType.ItemIdTree;
		public void ApplyPathConvention()
		{
			EnsureItemTreePath();
			itemTreePath.ItemId = ItemId;
			base.Path = itemTreePath.RootPath;
		}

		private void EnsureItemTreePath()
		{
			if (itemTreePath == null)
				itemTreePath = new ItemTreePath(base.Path, base.ItemId);
		}

		public string Topdir
		{
			get
			{
				EnsureItemTreePath();
				return itemTreePath.Topdir;
			}
		}
		public string Subdir
		{
			get
			{
				EnsureItemTreePath();
				return itemTreePath.Subdir;
			}
		}
		public string TopdirRoot
		{
			get
			{
				EnsureItemTreePath();
				return itemTreePath.RootPath;
			}
		}

		public string SubdirRoot
		{
			get
			{
				EnsureItemTreePath();
				var p = itemTreePath.RootPath;
				if (!string.IsNullOrEmpty(itemTreePath.Topdir))
					p += itemTreePath.Topdir + Os.DIRSEPERATOR;
				return p;
			}
		}

		public override string Path
		{
			get
			{
				EnsureItemTreePath();
				return itemTreePath.Path;
			}
			set
			{
				EnsureItemTreePath();
				itemTreePath.RootPath = value;
				base.Path = itemTreePath.RootPath;
			}
		}

		public override string ItemId
		{
			get => base.ItemId;
			set
			{
				base.ItemId = value;
				EnsureItemTreePath();
				itemTreePath.ItemId = value;
				base.Path = itemTreePath.RootPath;
			}
		}

		public new void mkdir()
		{
			RaiFile.mkdir(Path);
		}

		public new bool CopyTo(string[] destDirs)
		{
			try
			{
				foreach (var destDir in destDirs)
				{
					var dest = new ImageFile(FullName) { Path = destDir };
					dest.mkdir();
					if (File.Exists(dest.FullName))
						File.Delete(dest.FullName);
					File.Copy(FullName, dest.FullName);
				}
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

		public void rmdir()
		{
			new RaiFile(Path).rmdir();
			new RaiFile(SubdirRoot).rmdir();
		}

		public ImageTreeFile(string name, string path, string nameExt, string ext)
			: base(name)
		{
			Path = string.IsNullOrEmpty(path) ? null : path;
			NameExt = string.IsNullOrEmpty(nameExt) ? null : nameExt;
			Ext = string.IsNullOrEmpty(ext) ? null : ext;
			itemTreePath = new ItemTreePath(base.Path, base.ItemId);
			ApplyPathConvention();
		}

		public ImageTreeFile(string file)
			: base(file)
		{
			itemTreePath = new ItemTreePath(base.Path, base.ItemId);
			ApplyPathConvention();
		}
	}
}
