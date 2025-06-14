using System.Collections.Generic;

public class CharacterSheet
{
	public string Name { get; set; }
	public int MaxHealthPoints { get; set; }
	public int CurrentHealthPoints { get; set; }
	public int ArmorClass { get; set; }
	public int Speed { get; set; } // Typically in feet per round for TTRPGs

	public Dictionary<string, string> CustomProperties { get; set; }

	public CharacterSheet()
	{
		CustomProperties = new Dictionary<string, string>();
		Name = "Unnamed Creature";
		MaxHealthPoints = 1;
		CurrentHealthPoints = 1;
		ArmorClass = 10;
		Speed = 30; // Default speed
	}

	// Optional: Method to display basic info
	public override string ToString()
	{
		return $"Name: {Name}, HP: {CurrentHealthPoints}/{MaxHealthPoints}, AC: {ArmorClass}, Speed: {Speed}";
	}

	public Godot.Collections.Dictionary ToDictionary()
	{
		var dict = new Godot.Collections.Dictionary
		{
			{ "Name", Name },
			{ "MaxHealthPoints", MaxHealthPoints },
			{ "CurrentHealthPoints", CurrentHealthPoints },
			{ "ArmorClass", ArmorClass },
			{ "Speed", Speed },
		};
		// Need to convert C# Dictionary to Godot Dictionary for CustomProperties
		var customPropsGodot = new Godot.Collections.Dictionary();
		if (CustomProperties != null)
		{
			foreach (var kvp in CustomProperties)
			{
				customPropsGodot[kvp.Key] = kvp.Value;
			}
		}
		dict["CustomProperties"] = customPropsGodot;
		return dict;
	}

	public static CharacterSheet FromDictionary(Godot.Collections.Dictionary dict)
	{
		var sheet = new CharacterSheet();
		if (dict == null) return sheet; // Return default sheet if dict is null

		sheet.Name = dict.GetOrDefault("Name", "Unnamed Creature").ToString();
		sheet.MaxHealthPoints = dict.GetOrDefault("MaxHealthPoints", 10).AsInt32();
		sheet.CurrentHealthPoints = dict.GetOrDefault("CurrentHealthPoints", 10).AsInt32();
		sheet.ArmorClass = dict.GetOrDefault("ArmorClass", 10).AsInt32();
		sheet.Speed = dict.GetOrDefault("Speed", 30).AsInt32();

		if (dict.ContainsKey("CustomProperties") && dict["CustomProperties"].VariantType == Variant.Type.Dictionary)
		{
			var customPropsGodot = dict["CustomProperties"].AsGodotDictionary();
			sheet.CustomProperties = new Dictionary<string, string>(); // Ensure initialized
			foreach (var keyVariant in customPropsGodot.Keys)
			{
				string key = keyVariant.ToString();
				string value = customPropsGodot[keyVariant].ToString();
				sheet.CustomProperties[key] = value;
			}
		}
		else
		{
			sheet.CustomProperties = new Dictionary<string, string>(); // Initialize if not present in dict
		}
		return sheet;
	}
}
// Ensure GodotDictionaryExtensions.GetOrDefault is accessible if used.
// It was defined in CampaignSaveData.cs.
// For simplicity, assuming it's available or re-implement if necessary per file.
// The implementation of GetOrDefault in CampaignSaveData was:
// public static Variant GetOrDefault(this Godot.Collections.Dictionary dict, Variant key, Variant defaultValue)
// {
//     return dict.ContainsKey(key) ? dict[key] : defaultValue;
// }
// This should be fine.
