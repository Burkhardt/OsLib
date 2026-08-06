using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	/// <summary>
	/// Deterministic JSON canonicalization and content hashing (CR003, coordinated v3.13.2).
	/// <para>
	/// Canonical form recursively orders object properties by ordinal name, preserves
	/// array order, and emits invariant compact JSON without insignificant whitespace.
	/// The same logical content therefore always produces the same canonical text and
	/// the same SHA-256, regardless of the property order the producer happened to use.
	/// </para>
	/// </summary>
	public static class CanonicalJson
	{
		/// <summary>
		/// Returns the canonical compact JSON text for <paramref name="token"/>.
		/// </summary>
		public static string Canonicalize(JToken token)
		{
			if (token is null) throw new ArgumentNullException(nameof(token));
			var sb = new StringBuilder();
			Write(token, sb);
			return sb.ToString();
		}

		/// <summary>
		/// Full lowercase hex SHA-256 of the UTF-8 encoding of <paramref name="text"/>.
		/// </summary>
		public static string Sha256Hex(string text)
		{
			if (text is null) throw new ArgumentNullException(nameof(text));
			return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
		}

		/// <summary>
		/// Canonicalizes <paramref name="token"/> and returns the canonical text plus its SHA-256.
		/// </summary>
		public static (string CanonicalText, string Sha256) CanonicalizeWithHash(JToken token)
		{
			var text = Canonicalize(token);
			return (text, Sha256Hex(text));
		}

		private static void Write(JToken token, StringBuilder sb)
		{
			switch (token)
			{
				case JObject obj:
					sb.Append('{');
					var first = true;
					foreach (var property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
					{
						if (!first) sb.Append(',');
						first = false;
						sb.Append(JsonConvert.ToString(property.Name)).Append(':');
						Write(property.Value, sb);
					}
					sb.Append('}');
					break;
				case JArray arr:
					sb.Append('[');
					for (var i = 0; i < arr.Count; i++)
					{
						if (i > 0) sb.Append(',');
						Write(arr[i], sb);
					}
					sb.Append(']');
					break;
				case JValue value:
					WriteValue(value, sb);
					break;
				default:
					throw new NotSupportedException($"Canonical JSON does not support token type {token.Type}.");
			}
		}

		private static void WriteValue(JValue value, StringBuilder sb)
		{
			switch (value.Type)
			{
				case JTokenType.Null:
				case JTokenType.Undefined:
					sb.Append("null");
					break;
				case JTokenType.Boolean:
					sb.Append((bool)value.Value! ? "true" : "false");
					break;
				case JTokenType.Integer:
					sb.Append(((IFormattable)value.Value!).ToString(null, CultureInfo.InvariantCulture));
					break;
				case JTokenType.Float:
					sb.Append(value.Value switch
					{
						double d => JsonConvert.ToString(d),
						float f => JsonConvert.ToString(f),
						decimal m => JsonConvert.ToString(m),
						_ => Convert.ToString(value.Value, CultureInfo.InvariantCulture)
					});
					break;
				case JTokenType.Date:
					sb.Append(value.Value switch
					{
						DateTimeOffset dto => JsonConvert.ToString(dto.ToUniversalTime().UtcDateTime, DateFormatHandling.IsoDateFormat, DateTimeZoneHandling.Utc),
						DateTime dt => JsonConvert.ToString(dt.ToUniversalTime(), DateFormatHandling.IsoDateFormat, DateTimeZoneHandling.Utc),
						_ => JsonConvert.ToString(Convert.ToString(value.Value, CultureInfo.InvariantCulture))
					});
					break;
				case JTokenType.TimeSpan:
					sb.Append(JsonConvert.ToString((TimeSpan)value.Value!));
					break;
				case JTokenType.Guid:
					sb.Append(JsonConvert.ToString((Guid)value.Value!));
					break;
				case JTokenType.Uri:
					sb.Append(JsonConvert.ToString(((Uri)value.Value!).OriginalString));
					break;
				case JTokenType.Bytes:
					sb.Append(JsonConvert.ToString(Convert.ToBase64String((byte[])value.Value!)));
					break;
				default:
					sb.Append(JsonConvert.ToString(value.Value<string>()));
					break;
			}
		}
	}
}
