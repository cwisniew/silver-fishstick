using Godot; // For GetOrDefault if used here, though not strictly necessary for this class

public class StatusEffect
{
	public string Name { get; set; }
	public string Description { get; set; }
	public int DurationTurns { get; set; }    // 0 or negative for permanent/until removed
	public int RemainingTurns { get; set; } // Meaningful if DurationTurns > 0
	public string IconPath { get; set; }     // Path to an icon texture
	public string Source { get; set; }       // E.g., "Wizard Spell", "Potion of Speed"

	// Parameterless constructor for FromDictionary and general use
	public StatusEffect()
	{
		Name = "Unnamed Effect";
		Description = "";
		DurationTurns = 0;
		RemainingTurns = 0;
		IconPath = "";
		Source = "";
	}

	public StatusEffect(string name, int duration = 0, string description = "", string iconPath = "", string source = "")
	{
		Name = name;
		Description = description;
		DurationTurns = duration;
		RemainingTurns = duration; // Initially, remaining turns is the full duration
		IconPath = iconPath;
		Source = source;
	}

	public void ToDictionary(Godot.Collections.Dictionary dict) // Changed to void and takes dict
	{
		if (dict == null) return; // Or throw argument null exception

		dict["name"] = Name;
		dict["description"] = Description;
		dict["duration_turns"] = DurationTurns;
		dict["remaining_turns"] = RemainingTurns;
		dict["icon_path"] = IconPath;
		dict["source"] = Source;
	}

	public static StatusEffect FromDictionary(Godot.Collections.Dictionary dict)
	{
		StatusEffect effect = new StatusEffect();
		if (dict == null) return effect; // Return default effect if dict is null

		// Using Godot.Collections.Dictionary.GetOrDefault() requires the extension method.
		// Assuming it's accessible (e.g., defined in CampaignSaveData.cs or globally).
		// If not, use dict.ContainsKey(key) ? dict[key] : defaultValue;
		effect.Name = dict.GetOrDefault("name", "Unnamed Effect").ToString();
		effect.Description = dict.GetOrDefault("description", "").ToString();
		effect.DurationTurns = dict.GetOrDefault("duration_turns", 0).AsInt32();
		// Default remaining turns to full duration if not specified or if invalid
		effect.RemainingTurns = dict.GetOrDefault("remaining_turns", effect.DurationTurns).AsInt32();
		effect.IconPath = dict.GetOrDefault("icon_path", "").ToString();
		effect.Source = dict.GetOrDefault("source", "").ToString();

		return effect;
	}

	public override string ToString()
	{
		if (DurationTurns > 0)
		{
			return $"{Name} ({RemainingTurns}/{DurationTurns} turns)";
		}
		else if (DurationTurns < 0) // Negative duration could signify permanent until manually removed
		{
		    return $"{Name} (Permanent)";
		}
		// DurationTurns == 0 could mean instant, or also permanent/conditional until removed.
		// For simplicity, just name if duration isn't explicitly positive.
		return Name;
	}
}
// Helper extension method (if not already defined elsewhere like CampaignSaveData.cs)
public static class GodotDictionaryStatusEffectExtensions
{
    public static Variant GetOrDefault(this Godot.Collections.Dictionary dict, Variant key, Variant defaultValue)
    {
        return dict.ContainsKey(key) ? dict[key] : defaultValue;
    }
}
