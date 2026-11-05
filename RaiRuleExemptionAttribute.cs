using System;

namespace OsLib
{
	/// <summary>Declares a justified exception to one RaiGuard rule within a method or class.</summary>
	/// <remarks>Does not exempt other rules or derived classes. Keep the scope as small as possible.</remarks>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
	public sealed class RaiRuleExemptionAttribute : Attribute
	{
		/// <summary>Constructs a rule-specific exemption with a reviewable justification.</summary>
		public RaiRuleExemptionAttribute(string ruleId, string justification)
		{
			if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("A rule ID is required.", nameof(ruleId));
			if (string.IsNullOrWhiteSpace(justification)) throw new ArgumentException("A justification is required.", nameof(justification));
			RuleId = ruleId.Trim().ToUpperInvariant();
			Justification = justification;
		}

		/// <summary>The single RaiGuard rule being exempted.</summary>
		public string RuleId { get; }
		/// <summary>Why this declaration deliberately exercises or otherwise requires the flagged behavior.</summary>
		public string Justification { get; }
	}
}
