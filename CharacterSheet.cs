using Godot;
using System.Collections.Generic;
using System.Linq;

public class CharacterSheet
{
	public string Name { get; set; }
	public int MaxHealthPoints { get; set; }
	public int CurrentHealthPoints { get; set; }
	public int ArmorClass { get; set; }
	public int Speed { get; set; }

	public Dictionary<string, string> CustomProperties { get; set; }
	public List<StatusEffect> ActiveStatusEffects { get; set; }
	public List<InventoryItem> Inventory { get; set; }

	public CharacterSheet()
	{
		CustomProperties = new Dictionary<string, string>();
		ActiveStatusEffects = new List<StatusEffect>();
		Inventory = new List<InventoryItem>();
		Name = "Unnamed Creature";
		MaxHealthPoints = 10;
		CurrentHealthPoints = MaxHealthPoints;
		ArmorClass = 10;
		Speed = 30;
	}

	public override string ToString()
	{
		return $"Name: {Name}, HP: {CurrentHealthPoints}/{MaxHealthPoints}, AC: {ArmorClass}, Speed: {Speed}, Statuses: {ActiveStatusEffects.Count}, Items: {Inventory.Count}";
	}

	// --- Status Effect Management ---
	public bool AddStatusEffect(StatusEffect effectToAdd)
	{
		if (effectToAdd == null || string.IsNullOrEmpty(effectToAdd.Name)) return false;
		StatusEffect existingEffect = ActiveStatusEffects.FirstOrDefault(e => e.Name.Equals(effectToAdd.Name, System.StringComparison.OrdinalIgnoreCase));
		if (existingEffect != null)
		{
			existingEffect.Description = effectToAdd.Description; existingEffect.IconPath = effectToAdd.IconPath; existingEffect.Source = effectToAdd.Source;
			if (effectToAdd.DurationTurns <= 0) { existingEffect.DurationTurns = effectToAdd.DurationTurns; existingEffect.RemainingTurns = effectToAdd.DurationTurns; }
			else { existingEffect.DurationTurns = effectToAdd.DurationTurns; existingEffect.RemainingTurns = effectToAdd.DurationTurns; }
			return true;
		}
		else { ActiveStatusEffects.Add(effectToAdd); return true; }
	}

	public bool RemoveStatusEffect(string effectName)
	{
		if (string.IsNullOrEmpty(effectName)) return false;
		StatusEffect effectToRemove = ActiveStatusEffects.FirstOrDefault(e => e.Name.Equals(effectName, System.StringComparison.OrdinalIgnoreCase));
		if (effectToRemove != null) { ActiveStatusEffects.Remove(effectToRemove); return true; }
		return false;
	}

	public bool DecrementStatusEffectDurations()
	{
		bool changed = false;
		for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
		{
			StatusEffect effect = ActiveStatusEffects[i];
			if (effect.DurationTurns > 0)
			{
				effect.RemainingTurns--; changed = true;
				if (effect.RemainingTurns <= 0) ActiveStatusEffects.RemoveAt(i);
			}
		}
		return changed;
	}

	// --- Inventory Management ---
	public bool AddItem(InventoryItem itemToAdd)
	{
		if (itemToAdd == null) return false;
		// Basic add, no stacking logic for now.
		Inventory.Add(itemToAdd);
		return true;
	}

	public bool RemoveItem(string itemName, int quantityToRemove = 1) // quantityToRemove not used yet
	{
		if (string.IsNullOrEmpty(itemName) || quantityToRemove <= 0) return false;
		InventoryItem item = Inventory.FirstOrDefault(i => i.Name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase));
		if (item != null)
		{
			// Basic remove: removes the first found stack or single item.
			Inventory.Remove(item);
			return true;
		}
		return false;
	}

	public bool ToggleEquipItem(string itemName)
	{
		if (string.IsNullOrEmpty(itemName)) return false;
		InventoryItem item = Inventory.FirstOrDefault(i => i.Name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase));
		if (item != null && item.IsEquippable)
		{
			item.IsEquipped = !item.IsEquipped;
			// Simple toggle. Future: could unequip other items of same type.
			return true;
		}
		return false;
	}

	// --- Serialization Methods ---
	public void ToDictionary(Godot.Collections.Dictionary dict)
	{
		if (dict == null) return;
		dict["Name"] = Name; dict["MaxHealthPoints"] = MaxHealthPoints; dict["CurrentHealthPoints"] = CurrentHealthPoints;
		dict["ArmorClass"] = ArmorClass; dict["Speed"] = Speed;
		var customPropsGodot = new Godot.Collections.Dictionary();
		if (CustomProperties != null) foreach (var kvp in CustomProperties) customPropsGodot[kvp.Key] = kvp.Value;
		dict["CustomProperties"] = customPropsGodot;
		var effectsArray = new Godot.Collections.Array();
		if (ActiveStatusEffects != null) foreach (var e in ActiveStatusEffects) { var d = new Godot.Collections.Dictionary(); e.ToDictionary(d); effectsArray.Add(d); }
		dict["active_status_effects"] = effectsArray;
		var inventoryArray = new Godot.Collections.Array();
		if (Inventory != null) foreach (var i in Inventory) { var d = new Godot.Collections.Dictionary(); i.ToDictionary(d); inventoryArray.Add(d); }
		dict["inventory"] = inventoryArray;
	}

	public static CharacterSheet FromDictionary(Godot.Collections.Dictionary dict)
	{
		var sheet = new CharacterSheet(); if (dict == null) return sheet;
		sheet.Name = dict.GetOrDefault("Name", "Unnamed Creature").ToString();
		sheet.MaxHealthPoints = dict.GetOrDefault("MaxHealthPoints", 10).AsInt32();
		sheet.CurrentHealthPoints = dict.GetOrDefault("CurrentHealthPoints", sheet.MaxHealthPoints).AsInt32();
		sheet.ArmorClass = dict.GetOrDefault("ArmorClass", 10).AsInt32();
		sheet.Speed = dict.GetOrDefault("Speed", 30).AsInt32();
		if (dict.ContainsKey("CustomProperties") && dict["CustomProperties"].VariantType == Variant.Type.Dictionary) { var cp = dict["CustomProperties"].AsGodotDictionary(); sheet.CustomProperties = new Dictionary<string, string>(); foreach (var k in cp.Keys) sheet.CustomProperties[k.ToString()] = cp[k].ToString(); }
		else sheet.CustomProperties = new Dictionary<string, string>();
		sheet.ActiveStatusEffects = new List<StatusEffect>();
		if (dict.ContainsKey("active_status_effects") && dict["active_status_effects"].VariantType == Variant.Type.Array) { var ea = dict["active_status_effects"].AsGodotArray(); foreach (var v in ea) if (v.VariantType == Variant.Type.Dictionary) sheet.ActiveStatusEffects.Add(StatusEffect.FromDictionary(v.AsGodotDictionary())); }
		sheet.Inventory = new List<InventoryItem>();
		if (dict.ContainsKey("inventory") && dict["inventory"].VariantType == Variant.Type.Array) { var ia = dict["inventory"].AsGodotArray(); foreach (var v in ia) if (v.VariantType == Variant.Type.Dictionary) sheet.Inventory.Add(InventoryItem.FromDictionary(v.AsGodotDictionary())); }
		return sheet;
	}
}
// GodotDictionaryExtensions.GetOrDefault is assumed globally accessible
