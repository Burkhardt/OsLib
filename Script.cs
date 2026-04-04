using System;
using System.IO;

namespace OsLib
{
	public sealed class Script : RaiSystem
	{
		private const UnixFileMode ExecutableMode =
			UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
			UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
			UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

		public TextFile ScriptFile { get; }

		public string FullName => ScriptFile.FullName;

		public Script(RaiPath path, string name, string content)
			: this(path, name, ResolveScriptExtension(name), content)
		{
		}

		public Script(RaiPath path, string name, string ext ="sh", string content = null)
			: base(new RaiFile(path, name, ext).FullName)
		{
			ScriptFile = new TextFile(path: path, name: name, ext: ext, content: content);
			EnsureExecutable();
		}

		private static string ResolveScriptExtension(string name)
		{
			return string.IsNullOrWhiteSpace(Path.GetExtension(name))
				? OperatingSystem.IsWindows() ? "cmd" : "sh"
				: string.Empty;
		}

		public Script Append(string line)
		{
			ScriptFile.Append(line);
			return this;
		}

		public Script Save(bool backup = false)
		{
			ScriptFile.Save(backup);
			EnsureExecutable();
			return this;
		}

		public Script EnsureExecutable()
		{
			if (!OperatingSystem.IsWindows() && System.IO.File.Exists(FullName))
				System.IO.File.SetUnixFileMode(FullName, ExecutableMode);
			return this;
		}
	}
}