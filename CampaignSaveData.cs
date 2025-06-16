using Godot;
using System.Collections.Generic; // For List
using System.Linq; // For Select in ToDictionary

// --- Basic Data Structures ---
public class Vector2Data
{
	public float X { get; set; }
	public float Y { get; set; }

	public Vector2Data() { }
	public Vector2Data(Vector2 vector) { X = vector.X; Y = vector.Y; }
	public Vector2 ToVector2() => new Vector2(X, Y);

	public Godot.Collections.Dictionary ToDictionary() => new Godot.Collections.Dictionary { { "X", X }, { "Y", Y } };
	public static Vector2Data FromDictionary(Godot.Collections.Dictionary dict)
	{
		return new Vector2Data
		{
			X = dict.ContainsKey("X") ? dict["X"].AsSingle() : 0f,
			Y = dict.ContainsKey("Y") ? dict["Y"].AsSingle() : 0f
		};
	}
}

public class ColorData
{
	public float R { get; set; }
	public float G { get; set; }
	public float B { get; set; }
	public float A { get; set; }

	public ColorData() { }
	public ColorData(Color color) { R = color.R; G = color.G; B = color.B; A = color.A; }
	public Color ToColor() => new Color(R, G, B, A);

	public Godot.Collections.Dictionary ToDictionary() => new Godot.Collections.Dictionary { { "R", R }, { "G", G }, { "B", B }, { "A", A } };
	public static ColorData FromDictionary(Godot.Collections.Dictionary dict)
	{
		return new ColorData
		{
			R = dict.ContainsKey("R") ? dict["R"].AsSingle() : 0f,
			G = dict.ContainsKey("G") ? dict["G"].AsSingle() : 0f,
			B = dict.ContainsKey("B") ? dict["B"].AsSingle() : 0f,
			A = dict.ContainsKey("A") ? dict["A"].AsSingle() : 1f
		};
	}
}

// --- Character Sheet & Token ---
public class CharacterSheetData // This DTO will now primarily hold the serialized dictionary of the CharacterSheet
{
	public Godot.Collections.Dictionary SheetAsDictionary { get; set; }
	// We can keep other direct fields if we want quick access to some sheet data without parsing the dictionary,
	// but for full state, SheetAsDictionary is the source of truth from CharacterSheet's own serialization.
	// For this task, let's assume SheetAsDictionary is enough. If we needed Name for quick access:
	// public string Name { get; set; }

	public CharacterSheetData() { SheetAsDictionary = new Godot.Collections.Dictionary(); }

	// Constructor to create DTO from a live CharacterSheet
	public CharacterSheetData(CharacterSheet liveSheet)
	{
		SheetAsDictionary = new Godot.Collections.Dictionary();
		if (liveSheet != null)
		{
			liveSheet.ToDictionary(SheetAsDictionary); // Populate the dictionary
			// If Name was a direct property: Name = liveSheet.Name;
		}
	}

	// Method to convert DTO back to a live CharacterSheet
	public CharacterSheet ToCharacterSheet()
	{
		// If Name was a direct property and needs to be passed to FromDictionary or set after:
		// var sheet = CharacterSheet.FromDictionary(SheetAsDictionary);
		// sheet.Name = this.Name; // If FromDictionary doesn't handle Name from the dict itself
		// return sheet;
		return CharacterSheet.FromDictionary(SheetAsDictionary);
	}

	public Godot.Collections.Dictionary ToDictionary()
	{
		// The DTO itself serializes to a dictionary containing the SheetAsDictionary
		return new Godot.Collections.Dictionary
		{
			{ "SheetAsDictionary", SheetAsDictionary }
			// If Name was a direct property: { "Name", Name }
		};
	}

	public static CharacterSheetData FromDictionary(Godot.Collections.Dictionary dict)
	{
		var data = new CharacterSheetData();
		if (dict.ContainsKey("SheetAsDictionary") && dict["SheetAsDictionary"].VariantType == Variant.Type.Dictionary)
		{
			data.SheetAsDictionary = dict["SheetAsDictionary"].AsGodotDictionary();
		}
		else
		{
			data.SheetAsDictionary = new Godot.Collections.Dictionary(); // Empty if not found
			GD.PrintErr("CharacterSheetData.FromDictionary: 'SheetAsDictionary' key missing or not a Dictionary.");
		}
		// If Name was a direct property: data.Name = dict.GetOrDefault("Name", "Unnamed Sheet DTO").ToString();
		return data;
	}
}

public class TokenData
{
	public Vector2Data Position { get; set; }
	public float RotationDegrees { get; set; }
	public string TexturePath { get; set; }
	public CharacterSheetData SheetData { get; set; }
	public bool HasVision { get; set; } = true;
	public float VisionRangeGameUnits { get; set; } = 6.0f;
	public string NodeName { get; set; }
	public Vector2Data Size {get; set; }
	public long OwningPlayerId { get; set; } = 1; // Default to server/GM

	public TokenData() { }

	public Godot.Collections.Dictionary ToDictionary() => new Godot.Collections.Dictionary
	{
		{ "Position", Position?.ToDictionary() }, { "RotationDegrees", RotationDegrees },
		{ "TexturePath", TexturePath }, { "SheetData", SheetData?.ToDictionary() },
		{ "HasVision", HasVision }, { "VisionRange", VisionRangeGameUnits },
		{ "NodeName", NodeName }, {"Size", Size?.ToDictionary() },
		{ "OwningPlayerId", OwningPlayerId }
	};

	public static TokenData FromDictionary(Godot.Collections.Dictionary dict)
	{
		var data = new TokenData();
		if (dict.ContainsKey("Position")) data.Position = Vector2Data.FromDictionary(dict["Position"].AsGodotDictionary());
		data.RotationDegrees = dict.GetOrDefault("RotationDegrees", 0f).AsSingle();
		data.TexturePath = dict.GetOrDefault("TexturePath", "").ToString();
		if (dict.ContainsKey("SheetData")) data.SheetData = CharacterSheetData.FromDictionary(dict["SheetData"].AsGodotDictionary());
		data.HasVision = dict.GetOrDefault("HasVision", true).AsBool();
		data.VisionRangeGameUnits = dict.GetOrDefault("VisionRange", 6.0f).AsSingle();
		data.NodeName = dict.GetOrDefault("NodeName", "Token").ToString();
		if (dict.ContainsKey("Size")) data.Size = Vector2Data.FromDictionary(dict["Size"].AsGodotDictionary());
		data.OwningPlayerId = dict.GetOrDefault("OwningPlayerId", 1L).AsInt64(); // Default to 1 (long)
		return data;
	}
}

// --- Combat Tracker ---
public class CombatantData
{
	public string Name { get; set; }
	public int Initiative { get; set; }
	public string LinkedTokenNodeName { get; set; }
	public long NetworkPlayerId { get; set; } = 0; // 0 if not player controlled, otherwise network ID

	public CombatantData() { }

	public Godot.Collections.Dictionary ToDictionary() => new Godot.Collections.Dictionary
	{
		{ "Name", Name }, { "Initiative", Initiative },
		{ "LinkedTokenNodeName", LinkedTokenNodeName }, { "NetworkPlayerId", NetworkPlayerId }
	};
	public static CombatantData FromDictionary(Godot.Collections.Dictionary dict) => new CombatantData
	{
		Name = dict.GetOrDefault("Name", "Combatant").ToString(),
		Initiative = dict.GetOrDefault("Initiative", 0).AsInt32(),
		LinkedTokenNodeName = dict.GetOrDefault("LinkedTokenNodeName", "").ToString(),
		NetworkPlayerId = dict.GetOrDefault("NetworkPlayerId", 0L).AsInt64() // Default to 0 (long)
	};
}

public class CombatTrackerData
{
	public List<CombatantData> Combatants { get; set; } = new List<CombatantData>();
	public int CurrentTurnIndex { get; set; } = -1;
	public int RoundNumber { get; set; } = 0;
	public bool CombatStarted { get; set; } = false;

	public CombatTrackerData() { }

	public Godot.Collections.Dictionary ToDictionary() => new Godot.Collections.Dictionary
	{
		{ "Combatants", new Godot.Collections.Array(Combatants.Select(c => c.ToDictionary())) },
		{ "CurrentTurnIndex", CurrentTurnIndex }, { "RoundNumber", RoundNumber }, { "CombatStarted", CombatStarted }
	};
	public static CombatTrackerData FromDictionary(Godot.Collections.Dictionary dict)
	{
		var data = new CombatTrackerData();
		if (dict.ContainsKey("Combatants"))
		{
			foreach (var item in dict["Combatants"].AsGodotArray())
				data.Combatants.Add(CombatantData.FromDictionary(item.AsGodotDictionary()));
		}
		data.CurrentTurnIndex = dict.GetOrDefault("CurrentTurnIndex", -1).AsInt32();
		data.RoundNumber = dict.GetOrDefault("RoundNumber", 0).AsInt32();
		data.CombatStarted = dict.GetOrDefault("CombatStarted", false).AsBool();
		return data;
	}
}

// --- Drawings ---
public class DrawingLineData
{
	public List<Vector2Data> Points { get; set; } = new List<Vector2Data>();
	public ColorData LineColor { get; set; }
	public float Thickness { get; set; }

	public DrawingLineData() { Points = new List<Vector2Data>(); } // Ensure Points is initialized

	public DrawingLineData(List<Vector2> points, Color color, float thickness) // Assumes points are local
	{
		Points = points.Select(p => new Vector2Data(p)).ToList();
		LineColor = new ColorData(color);
		Thickness = thickness;
	}

	public Godot.Collections.Dictionary ToDictionary() => new Godot.Collections.Dictionary
	{
		{ "Points", new Godot.Collections.Array(Points.Select(p => p.ToDictionary())) },
		{ "LineColor", LineColor?.ToDictionary() }, { "Thickness", Thickness }
	};
	public static DrawingLineData FromDictionary(Godot.Collections.Dictionary dict)
	{
		var data = new DrawingLineData();
		if (dict.ContainsKey("Points"))
		{
			foreach (var item in dict["Points"].AsGodotArray())
				data.Points.Add(Vector2Data.FromDictionary(item.AsGodotDictionary()));
		}
		if (dict.ContainsKey("LineColor")) data.LineColor = ColorData.FromDictionary(dict["LineColor"].AsGodotDictionary());
		data.Thickness = dict.GetOrDefault("Thickness", 2f).AsSingle();
		return data;
	}

	public static Godot.Collections.Array ListToGodotArrayOfDictionaries(List<DrawingLineData> list)
	{
		var godotArray = new Godot.Collections.Array();
		if (list == null) return godotArray;
		foreach (var item in list)
		{
			godotArray.Add(item.ToDictionary());
		}
		return godotArray;
	}

	public static List<DrawingLineData> ListFromGodotArray(Godot.Collections.Array godotArray)
	{
		var list = new List<DrawingLineData>();
		if (godotArray == null) return list;
		foreach (var itemVariant in godotArray)
		{
			if (itemVariant.VariantType == Variant.Type.Dictionary)
			{
				list.Add(DrawingLineData.FromDictionary(itemVariant.AsGodotDictionary()));
			}
		}
		return list;
	}
}


// --- Root Save Object ---
public class CampaignRootData
{
	public string CurrentMapPath { get; set; }
	public List<TokenData> Tokens { get; set; } = new List<TokenData>();
	public CombatTrackerData CombatState { get; set; } // Placeholder for now
	public List<DrawingLineData> Drawings { get; set; } = new List<DrawingLineData>(); // Placeholder

	public CampaignRootData() { }

	public Godot.Collections.Dictionary ToDictionary() => new Godot.Collections.Dictionary
	{
		{ "CurrentMapPath", CurrentMapPath },
		{ "Tokens", new Godot.Collections.Array(Tokens.Select(t => t.ToDictionary())) },
		{ "CombatState", CombatState?.ToDictionary() },
		{ "Drawings", new Godot.Collections.Array(Drawings.Select(d => d.ToDictionary())) }
	};

	public static CampaignRootData FromDictionary(Godot.Collections.Dictionary dict)
	{
		var data = new CampaignRootData();
		data.CurrentMapPath = dict.GetOrDefault("CurrentMapPath", "").ToString();
		if (dict.ContainsKey("Tokens"))
		{
			foreach (var item in dict["Tokens"].AsGodotArray())
				data.Tokens.Add(TokenData.FromDictionary(item.AsGodotDictionary()));
		}
		if (dict.ContainsKey("CombatState")) data.CombatState = CombatTrackerData.FromDictionary(dict["CombatState"].AsGodotDictionary());
		if (dict.ContainsKey("Drawings"))
		{
			foreach (var item in dict["Drawings"].AsGodotArray())
				data.Drawings.Add(DrawingLineData.FromDictionary(item.AsGodotDictionary()));
		}
		return data;
	}
}

// Helper for GetOrDefault from Godot.Collections.Dictionary
public static class GodotDictionaryExtensions
{
    public static Variant GetOrDefault(this Godot.Collections.Dictionary dict, Variant key, Variant defaultValue)
    {
        return dict.ContainsKey(key) ? dict[key] : defaultValue;
    }
    public static Godot.Collections.Dictionary<TKey, TValue> AsGodotDictionary<TKey, TValue>(this Variant variant)
    {
        if (variant.VariantType == Variant.Type.Dictionary)
        {
            var godotDict = variant.AsGodotDictionary();
            var result = new Godot.Collections.Dictionary<TKey, TValue>();
            foreach (var key in godotDict.Keys)
            {
                if (key.Obj is TKey tKey && godotDict[key].Obj is TValue tValue) // This casting is tricky
                {
                    result.Add(tKey, tValue);
                } else if (key.VariantType == Variant.Type.String && typeof(TKey) == typeof(string) &&
                           godotDict[key].VariantType == Variant.Type.String && typeof(TValue) == typeof(string))
                {
                     result.Add((TKey)(object)key.ToString(), (TValue)(object)godotDict[key].ToString());
                }
            }
            return result; // This generic conversion is complex with Variant.
                           // For string,string it's simpler. For other types, more robust conversion needed.
                           // The current implementation will likely only work for string,string.
        }
        return new Godot.Collections.Dictionary<TKey, TValue>();
    }
}
