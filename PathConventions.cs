namespace OsLib
{
	/// <summary>
	/// Shared contract for files that enforce a storage path convention.
	/// </summary>
	public enum PathConventionType
	{
		CanonicalByName,
		ItemIdTree3x3,
		ItemIdTree8x2
	}

	public interface IPathConvention
	{
		PathConventionType Convention { get; }
		void ApplyPathConvention();
	}
}
