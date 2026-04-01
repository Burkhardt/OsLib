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
			: base(new RaiFile(path, name).FullName)
		{
			ScriptFile = new TextFile(path, name, content);
			EnsureExecutable();
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