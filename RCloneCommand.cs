using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public sealed class RCloneCommand : CliCommand
	{
		private readonly RaiPath rclonePath;
		private readonly string commandName;

		public RCloneCommand(RaiPath rclonePath = null, string commandName = "rclone")
			: base(commandName, packageName: "rclone")
		{
			this.rclonePath = rclonePath;
			this.commandName = string.IsNullOrWhiteSpace(commandName) ? "rclone" : commandName;
		}

		public override IEnumerable<string> CandidateExecutables
		{
			get
			{
				if (rclonePath != null)
				{
					var cmd = new RaiFile(commandName);
					cmd.Path = rclonePath;
					yield return cmd.FullName;
				}

				yield return commandName;
			}
		}

		protected override string WindowsPackageId => "Rclone.Rclone";

		public string BuildArguments(string subcommand, string args = "")
		{
			return $"{subcommand} {args}".Trim();
		}

		public RaiSystemResult RunSubcommand(string subcommand, string args = "")
		{
			return Run(BuildArguments(subcommand, args));
		}

		public Task<RaiSystemResult> RunSubcommandAsync(string subcommand, string args = "", CancellationToken cancellationToken = default)
		{
			return RunAsync(BuildArguments(subcommand, args), cancellationToken);
		}
	}
}