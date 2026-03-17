using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OsLib
{
	public abstract class ConfigFile<TData> : RaiFile where TData : class, new()
	{
		protected ConfigFile(string fullName, bool autoLoad = true) : base(fullName)
		{
			Data = CreateDefaultData();
			if (autoLoad)
				Load();
		}

		public TData Data { get; protected set; }

		public virtual TData Load()
		{
			if (!Exists())
			{
				Data = NormalizeData(CreateDefaultData());
				Save();
				return Data;
			}

			try
			{
				var json = File.ReadAllText(FullName);
				var data = JsonConvert.DeserializeObject<TData>(json, CreateSerializerSettings()) ?? CreateDefaultData();
				Data = NormalizeData(data);
				return Data;
			}
			catch (JsonException ex)
			{
				throw new InvalidDataException($"The configuration file '{FullName}' could not be parsed.", ex);
			}
		}

		protected virtual TData CreateDefaultData() => new TData();

		protected virtual TData NormalizeData(TData data) => data ?? CreateDefaultData();

		public void Save()
		{
			mkdir();
			File.WriteAllText(FullName, JsonConvert.SerializeObject(Data, Formatting.Indented, CreateSerializerSettings()));
		}

		internal bool SetFullName(string fullName)
		{
			var normalized = System.IO.Path.GetFullPath(fullName);
			var current = string.IsNullOrWhiteSpace(FullName) ? string.Empty : System.IO.Path.GetFullPath(FullName);
			var changed = !string.Equals(
				current,
				normalized,
				OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			var directory = System.IO.Path.GetDirectoryName(normalized) ?? string.Empty;
			var extension = System.IO.Path.GetExtension(normalized);

			Path = string.IsNullOrWhiteSpace(directory)
				? string.Empty
				: Os.NormSeperator(directory) + Os.DIRSEPERATOR;
			Name = System.IO.Path.GetFileNameWithoutExtension(normalized);
			Ext = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.TrimStart('.');
			return changed;
		}

		private static JsonSerializerSettings CreateSerializerSettings()
		{
			return new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = Formatting.Indented,
				Converters = { new StringEnumConverter() }
			};
		}
	}
}