using Godot; // Required for Token type, even if not a Node itself

public class Combatant
{
	public string Name { get; set; } // Can be character name if no token, or overridden
	public int Initiative { get; set; }
	public Token LinkedToken { get; set; } // Reference to the Token node on the map

	// Computed property for display in the tracker
	public string DisplayText
	{
		get
		{
			if (LinkedToken != null && LinkedToken.Sheet != null && !string.IsNullOrWhiteSpace(LinkedToken.Sheet.Name))
			{
				return LinkedToken.Sheet.Name;
			}
			return string.IsNullOrWhiteSpace(Name) ? "Unnamed Combatant" : Name;
		}
	}

	public Combatant(string name, int initiative, Token token = null)
	{
		Name = name;
		Initiative = initiative;
		LinkedToken = token;
	}
}
