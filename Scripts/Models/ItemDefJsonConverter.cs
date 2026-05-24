using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccultShop.Models;

public sealed class ItemDefJsonConverter : JsonConverter<ItemDef>
{
	public override ItemDef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException("Expected ItemDef JSON object.");

		var item = new ItemDef();

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
				return item;

			if (reader.TokenType != JsonTokenType.PropertyName)
				throw new JsonException("Expected a property name while reading ItemDef.");

			var propertyName = reader.GetString();
			if (!reader.Read())
				throw new JsonException("Unexpected end of ItemDef JSON.");

			switch (propertyName)
			{
				case "id":
				case "Id":
					item.Id = reader.GetString() ?? "";
					break;
				case "name":
				case "Name":
					item.Name = reader.GetString() ?? "";
					break;
				case "iconPath":
				case "IconPath":
					item.IconPath = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
					break;
				case "description":
				case "Description":
					item.Description = reader.GetString() ?? "";
					break;
				case "tags":
				case "Tags":
					item.Tags = JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? new List<string>();
					break;
				case "quality":
				case "Quality":
					item.Quality = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : 0;
					break;
				case "traits":
				case "Traits":
					item.Traits = JsonSerializer.Deserialize<Dictionary<string, int>>(ref reader, options) ?? new Dictionary<string, int>();
					break;
				case "risks":
				case "Risks":
					item.Risks = JsonSerializer.Deserialize<Dictionary<string, int>>(ref reader, options) ?? new Dictionary<string, int>();
					break;
				case "price":
				case "BasePrice":
				case "basePrice":
					item.BasePrice = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : 0;
					break;
				default:
					reader.Skip();
					break;
			}
		}

		throw new JsonException("Unexpected end of ItemDef JSON.");
	}

	public override void Write(Utf8JsonWriter writer, ItemDef value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteString("id", value.Id);
		writer.WriteString("name", value.Name);

		if (!string.IsNullOrWhiteSpace(value.IconPath))
			writer.WriteString("iconPath", value.IconPath);

		writer.WriteString("description", value.Description);
		writer.WritePropertyName("tags");
		JsonSerializer.Serialize(writer, value.Tags ?? new List<string>(), options);
		writer.WriteNumber("quality", value.Quality);
		writer.WritePropertyName("traits");
		JsonSerializer.Serialize(writer, value.Traits ?? new Dictionary<string, int>(), options);
		writer.WritePropertyName("risks");
		JsonSerializer.Serialize(writer, value.Risks ?? new Dictionary<string, int>(), options);
		writer.WriteNumber("price", value.BasePrice);
		writer.WriteEndObject();
	}
}
