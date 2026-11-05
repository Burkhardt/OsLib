#nullable enable
using System;

namespace OsLib
{
	public static partial class Os
	{
		/// <summary>
		/// Reads an environment switch passed to this specific process invocation by a developer or CI runner.
		/// Deliberately does not consult Os.Config: a disposable run-scoped toggle must not leak into
		/// or come from machine configuration. Returns null if unset.
		/// </summary>
		[RaiRuleExemption("RAI007", "Implements the sanctioned read-only process-invocation switch boundary; no machine configuration is consulted.")]
		public static string? RunSwitch(string name) => Environment.GetEnvironmentVariable(name);
	}
}
