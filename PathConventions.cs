namespace OsLib
{
	/// <summary>
	/// Shared contract for files that enforce a storage path convention.
	/// </summary>
	public enum PathConventionType
	{
		CanonicalByName,
		ItemIdTree
	}

	public interface IPathConventionFile
	{
		PathConventionType ConventionName { get; }
		void ApplyPathConvention();
	}
}
