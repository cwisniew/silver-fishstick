using Godot; // Required for Token type, even if not a Node itself

public class Combatant
{
	public string Name { get; set; }
	public int Initiative { get; set; }
	public Token LinkedToken { get; set; }
	public long NetworkPlayerId { get; set; } = 0; // 0 if NPC or unassigned, network ID if player controlled

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
