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
}
