using Godot; // For potential future Godot-specific features, not strictly needed now

public class Card
{
	public string Suit { get; set; }
	public string Rank { get; set; }
	public string CustomName { get; set; } // For cards like "Joker" or special game cards

	public string Name
	{
		get
		{
			if (!string.IsNullOrEmpty(CustomName))
			{
				return CustomName;
			}
			if (!string.IsNullOrEmpty(Rank) && !string.IsNullOrEmpty(Suit))
			{
				return $"{Rank} of {Suit}";
			}
			return "Unnamed Card";
		}
	}

	// Parameterless constructor for deserialization
	public Card() { }

	public Card(string rank, string suit)
	{
		Rank = rank;
		Suit = suit;
	}

	public Card(string customName)
	{
		CustomName = customName;
		Rank = string.Empty; // Or some other default
		Suit = string.Empty; // Or some other default
	}

	public override string ToString()
	{
		return Name;
	}

	// For Godot JSON serialization (manual conversion to Dictionary)
	public Godot.Collections.Dictionary ToDictionary()
	{
		return new Godot.Collections.Dictionary
		{
			{ "Suit", Suit },
			{ "Rank", Rank },
			{ "CustomName", CustomName }
		};
	}

	public static Card FromDictionary(Godot.Collections.Dictionary dict)
	{
		var card = new Card();
		card.Suit = dict.ContainsKey("Suit") ? dict["Suit"].ToString() : string.Empty;
		card.Rank = dict.ContainsKey("Rank") ? dict["Rank"].ToString() : string.Empty;
		card.CustomName = dict.ContainsKey("CustomName") ? dict["CustomName"].ToString() : string.Empty;
		return card;
	}
}
