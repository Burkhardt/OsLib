using System;

namespace OsLib.Tests;

public class RunSwitchTests
{
	[Fact]
	public void RunSwitchReadsCurrentInvocationValueWithoutCachingOrWritingConfiguration()
	{
		var name = "RAIKEEP_RUN_SWITCH_TEST_" + Guid.NewGuid().ToString("N");
		try
		{
			Assert.Null(Os.RunSwitch(name));
			Environment.SetEnvironmentVariable(name, "first");
			Assert.Equal("first", Os.RunSwitch(name));
			Environment.SetEnvironmentVariable(name, "second");
			Assert.Equal("second", Os.RunSwitch(name));
			Environment.SetEnvironmentVariable(name, null);
			Assert.Null(Os.RunSwitch(name));
		}
		finally { Environment.SetEnvironmentVariable(name, null); }
	}

	[Fact]
	public void RunSwitchDoesNotFallBackToMachineConfiguration()
	{
		Assert.Equal(Environment.GetEnvironmentVariable("TempDir"), Os.RunSwitch("TempDir"));
	}

	[Theory]
	[InlineData("", "reason")]
	[InlineData("RAI015", "")]
	[InlineData("RAI015", "  ")]
	public void ExemptionsRequireAnIdAndNonblankJustification(string id, string reason)
		=> Assert.Throws<ArgumentException>(() => new RaiRuleExemptionAttribute(id, reason));
}
