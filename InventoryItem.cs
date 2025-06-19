using Godot;
using System; // For StringComparison if needed, though not used here

public class InventoryItem
{
	public string Name { get; set; }
	public string Description { get; set; }
	public int Quantity { get; set; }
	public float Weight { get; set; }
	public string Type { get; set; } // E.g., "Weapon", "Armor", "Potion", "Generic"
	public bool IsEquippable { get; set; }
	public bool IsEquipped { get; set; }
	// For future use, can be empty for now:
	// public Godot.Collections.Dictionary Effects { get; set; }

	public InventoryItem()
	{
		Name = "New Item";
		Description = "";
		Quantity = 1;
		Weight = 0.0f;
		Type = "Generic";
		IsEquippable = false;
		IsEquipped = false;
		// Effects = new Godot.Collections.Dictionary();
	}

	public InventoryItem(string name, int quantity = 1, string description = "", string type = "Generic", float weight = 0.0f, bool isEquippable = false)
	{
		Name = name;
		Quantity = quantity;
		Description = description;
		Type = type;
		Weight = weight;
		IsEquippable = isEquippable;
		IsEquipped = false; // Item is not equipped by default when created this way
		// Effects = new Godot.Collections.Dictionary();
	}

	public void ToDictionary(Godot.Collections.Dictionary dict)
	{
		if (dict == null) return;

		dict["name"] = Name;
		dict["description"] = Description;
		dict["quantity"] = Quantity;
		dict["weight"] = Weight;
		dict["type"] = Type;
		dict["is_equippable"] = IsEquippable;
		dict["is_equipped"] = IsEquipped; // Corrected key name
		// dict["effects"] = Effects.Duplicate(true); // For future
	}

	public static InventoryItem FromDictionary(Godot.Collections.Dictionary dict)
	{
		InventoryItem item = new InventoryItem();
		if (dict == null) return item; // Return default item

		// Assuming GodotDictionaryExtensions.GetOrDefault is available
		item.Name = dict.GetOrDefault("name", "New Item").ToString();
		item.Description = dict.GetOrDefault("description", "").ToString();
		item.Quantity = dict.GetOrDefault("quantity", 1).AsInt32();
		item.Weight = (float)dict.GetOrDefault("weight", 0.0).AsDouble(); // Variant to double, then cast
		item.Type = dict.GetOrDefault("type", "Generic").ToString();
		item.IsEquippable = dict.GetOrDefault("is_equippable", false).AsBool();
		item.IsEquipped = dict.GetOrDefault("is_equipped", false).AsBool(); // Corrected key name

		// if (dict.ContainsKey("effects") && dict["effects"].VariantType == Variant.Type.Dictionary)
		// {
		//     item.Effects = dict["effects"].AsGodotDictionary().Duplicate(true);
		// }
		// else
		// {
		//     item.Effects = new Godot.Collections.Dictionary();
		// }
		return item;
	}

	public override string ToString()
	{
		string equippedMarker = IsEquippable && IsEquipped ? " [E]" : "";
		return $"{Name} (Qty: {Quantity}){equippedMarker}";
	}
}

// Reminder: GodotDictionaryExtensions.GetOrDefault should be defined in a globally accessible place
// or within each file that uses it if not using a shared utility script.
// Example:
// public static class GodotDictionaryInventoryItemExtensions
// {
//     public static Variant GetOrDefault(this Godot.Collections.Dictionary dict, Variant key, Variant defaultValue)
//     {
//         return dict.ContainsKey(key) ? dict[key] : defaultValue;
//     }
// }
// For this subtask, we rely on it being defined in CampaignSaveData.cs or StatusEffect.cs
// and being accessible due to C# using resolution.
