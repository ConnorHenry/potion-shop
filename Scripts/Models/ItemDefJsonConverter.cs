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
				case "startsKnownInIngredientBook":
				case "StartsKnownInIngredientBook":
					item.StartsKnownInIngredientBook = ReadBooleanValue(ref reader);
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
				case "consumableEffect":
				case "ConsumableEffect":
					item.ConsumableEffect = JsonSerializer.Deserialize<ConsumableEffectDef>(ref reader, options);
					break;
				case "consumableGate":
				case "ConsumableGate":
					item.ConsumableGate = JsonSerializer.Deserialize<ConsumableGateDef>(ref reader, options);
					break;
				case "treatment":
				case "Treatment":
					item.Treatment = JsonSerializer.Deserialize<ItemTreatmentDef>(ref reader, options);
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
		if (value.StartsKnownInIngredientBook)
			writer.WriteBoolean("startsKnownInIngredientBook", true);
		writer.WritePropertyName("tags");
		JsonSerializer.Serialize(writer, value.Tags ?? new List<string>(), options);
		writer.WriteNumber("quality", value.Quality);
		writer.WritePropertyName("traits");
		JsonSerializer.Serialize(writer, value.Traits ?? new Dictionary<string, int>(), options);
		writer.WritePropertyName("risks");
		JsonSerializer.Serialize(writer, value.Risks ?? new Dictionary<string, int>(), options);
		writer.WriteNumber("price", value.BasePrice);
		if (value.ConsumableEffect is not null)
		{
			writer.WritePropertyName("consumableEffect");
			JsonSerializer.Serialize(writer, value.ConsumableEffect, options);
		}
		if (value.ConsumableGate is not null)
		{
			writer.WritePropertyName("consumableGate");
			JsonSerializer.Serialize(writer, value.ConsumableGate, options);
		}
		if (value.Treatment is not null)
		{
			writer.WritePropertyName("treatment");
			JsonSerializer.Serialize(writer, value.Treatment, options);
		}
		writer.WriteEndObject();
	}

	private static bool ReadBooleanValue(ref Utf8JsonReader reader)
	{
		return reader.TokenType switch
		{
			JsonTokenType.True => true,
			JsonTokenType.False => false,
			JsonTokenType.String => bool.TryParse(reader.GetString(), out var parsed) && parsed,
			JsonTokenType.Number => reader.TryGetInt32(out var value) && value != 0,
			_ => false
		};
	}
}
