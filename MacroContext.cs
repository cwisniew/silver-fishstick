using Godot; // For Godot types like Color, Node references
using System.Collections.Generic;

// Forward declarations or assume these types exist and are accessible
// using CombatTracker = Node; // Replace with actual type if defined
// using Token = Node;        // Replace with actual type if defined
// using ChatLog = Node;      // Replace with actual type if defined
// using MainScene = Node;    // Replace with actual type if defined

public partial class MacroContext // Partial if it needs to be extended by Godot nodes, but plain C# class is fine
{
	public ChatLog Chat { get; set; }
	public Token SelectedToken { get; set; }
	// DiceRoller is static, so no instance needed here, can be called directly from MacroEngine
	// public DiceRoller Roller { get; } // If it were an instance
	public CombatTracker Combat { get; set; }
	public MainScene MainSceneInstance { get; set; } // For access to other game elements if needed

	// Variables for the macro session
	public Dictionary<string, object> Variables { get; private set; }

	public MacroContext()
	{
		Variables = new Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);
		// Roller = new DiceRoller(); // If DiceRoller was instance-based
	}
}
