using Godot;
using System.Collections.Generic; // For List<StatusEffect>
using System.Linq; // For FirstOrDefault

public class CharacterSheet
{
	public string Name { get; set; }
	public int MaxHealthPoints { get; set; }
	public int CurrentHealthPoints { get; set; }
	public int ArmorClass { get; set; }
	public int Speed { get; set; }

	public Dictionary<string, string> CustomProperties { get; set; }
	public List<StatusEffect> ActiveStatusEffects { get; set; }

	public CharacterSheet()
	{
		CustomProperties = new Dictionary<string, string>();
		ActiveStatusEffects = new List<StatusEffect>();
		Name = "Unnamed Creature";
		MaxHealthPoints = 10; // Defaulted from 1 to 10 for more typical use
		CurrentHealthPoints = MaxHealthPoints; // Default current to max
		ArmorClass = 10;
		Speed = 30;
	}

	public override string ToString()
	{
		return $"Name: {Name}, HP: {CurrentHealthPoints}/{MaxHealthPoints}, AC: {ArmorClass}, Speed: {Speed}, Statuses: {ActiveStatusEffects.Count}";
	}

	public bool AddStatusEffect(StatusEffect effectToAdd)
	{
		if (effectToAdd == null || string.IsNullOrEmpty(effectToAdd.Name)) return false;

		StatusEffect existingEffect = ActiveStatusEffects.FirstOrDefault(e => e.Name.Equals(effectToAdd.Name, System.StringComparison.OrdinalIgnoreCase));

		if (existingEffect != null)
		{
			// Update existing effect
			existingEffect.Description = effectToAdd.Description;
			existingEffect.IconPath = effectToAdd.IconPath;
			existingEffect.Source = effectToAdd.Source;

			// If new effect duration is 0 or less, it's permanent or non-duration based.
			// Existing effect might have had a duration, this new application makes it permanent/non-duration.
			if (effectToAdd.DurationTurns <= 0)
			{
				existingEffect.DurationTurns = effectToAdd.DurationTurns; // Could be 0 or negative (e.g. -1 for permanent)
				existingEffect.RemainingTurns = effectToAdd.DurationTurns; // Match this characteristic
			}
			else // New effect has a positive duration, refresh the existing one
			{
				existingEffect.DurationTurns = effectToAdd.DurationTurns;
				existingEffect.RemainingTurns = effectToAdd.DurationTurns; // Refresh to new full duration
			}
			// GD.Print($"Updated existing status effect: {existingEffect.Name}");
			return true; // Updated existing
		}
		else
		{
			// Add new effect
			ActiveStatusEffects.Add(effectToAdd);
			// GD.Print($"Added new status effect: {effectToAdd.Name}");
			return true; // Added new
		}
	}

	public bool RemoveStatusEffect(string effectName)
	{
		if (string.IsNullOrEmpty(effectName)) return false;
		StatusEffect effectToRemove = ActiveStatusEffects.FirstOrDefault(e => e.Name.Equals(effectName, System.StringComparison.OrdinalIgnoreCase));
		if (effectToRemove != null)
		{
			ActiveStatusEffects.Remove(effectToRemove);
			// GD.Print($"Removed status effect: {effectName}");
			return true;
		}
		// GD.Print($"Status effect not found for removal: {effectName}");
		return false;
	}

	public bool DecrementStatusEffectDurations()
	{
		bool changed = false;
		for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
		{
			StatusEffect effect = ActiveStatusEffects[i];
			if (effect.DurationTurns > 0) // Only decrement if duration is positive (timed effect)
			{
				effect.RemainingTurns--;
				changed = true;
				// GD.Print($"Decremented {effect.Name} to {effect.RemainingTurns} turns.");
				if (effect.RemainingTurns <= 0)
				{
					ActiveStatusEffects.RemoveAt(i);
					// GD.Print($"Effect {effect.Name} expired and removed.");
				}
			}
		}
		return changed;
	}


	// --- Serialization Methods ---
	public void ToDictionary(Godot.Collections.Dictionary dict)
	{
		if (dict == null) return;
		dict["Name"] = Name;
		dict["MaxHealthPoints"] = MaxHealthPoints;
		dict["CurrentHealthPoints"] = CurrentHealthPoints;
		dict["ArmorClass"] = ArmorClass;
		dict["Speed"] = Speed;
		var customPropsGodot = new Godot.Collections.Dictionary();
		if (CustomProperties != null) foreach (var kvp in CustomProperties) customPropsGodot[kvp.Key] = kvp.Value;
		dict["CustomProperties"] = customPropsGodot;
		var effectsArray = new Godot.Collections.Array();
		if (ActiveStatusEffects != null)
		{
			foreach (var effect in ActiveStatusEffects)
			{
				var effectDict = new Godot.Collections.Dictionary();
				effect.ToDictionary(effectDict);
				effectsArray.Add(effectDict);
			}
		}
		dict["active_status_effects"] = effectsArray;
	}

	public static CharacterSheet FromDictionary(Godot.Collections.Dictionary dict)
	{
		var sheet = new CharacterSheet();
		if (dict == null) return sheet;

		sheet.Name = dict.GetOrDefault("Name", "Unnamed Creature").ToString();
		sheet.MaxHealthPoints = dict.GetOrDefault("MaxHealthPoints", 10).AsInt32();
		sheet.CurrentHealthPoints = dict.GetOrDefault("CurrentHealthPoints", sheet.MaxHealthPoints).AsInt32();
		sheet.ArmorClass = dict.GetOrDefault("ArmorClass", 10).AsInt32();
		sheet.Speed = dict.GetOrDefault("Speed", 30).AsInt32();

		if (dict.ContainsKey("CustomProperties") && dict["CustomProperties"].VariantType == Variant.Type.Dictionary)
		{
			var customPropsGodot = dict["CustomProperties"].AsGodotDictionary();
			sheet.CustomProperties = new Dictionary<string, string>();
			foreach (var keyVariant in customPropsGodot.Keys)
				sheet.CustomProperties[keyVariant.ToString()] = customPropsGodot[keyVariant].ToString();
		} else sheet.CustomProperties = new Dictionary<string, string>();

		if (dict.ContainsKey("active_status_effects") && dict["active_status_effects"].VariantType == Variant.Type.Array)
		{
			var effectsArray = dict["active_status_effects"].AsGodotArray();
			sheet.ActiveStatusEffects = new List<StatusEffect>();
			foreach (var effectVariant in effectsArray)
				if (effectVariant.VariantType == Variant.Type.Dictionary)
					sheet.ActiveStatusEffects.Add(StatusEffect.FromDictionary(effectVariant.AsGodotDictionary()));
		} else sheet.ActiveStatusEffects = new List<StatusEffect>();

		return sheet;
	}
}
// Helper extension method (assuming it's globally accessible or defined in one place e.g. CampaignSaveData.cs)
// public static class GodotDictionaryCharacterSheetExtensions
// {
//     public static Variant GetOrDefault(this Godot.Collections.Dictionary dict, Variant key, Variant defaultValue)
//     {
//         return dict.ContainsKey(key) ? dict[key] : defaultValue;
//     }
// }
