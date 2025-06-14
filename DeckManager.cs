using Godot;
using System;
using System.Collections.Generic;

public static class DeckManager
{
	public static List<Deck> AvailableDecks { get; private set; } = new List<Deck>();
	private const string DecksFilePath = "user://user_decks.json";
	public static event Action OnDecksChanged; // For local UI updates (e.g., on server after its own action)
	public static event Action OnDecksExternallyUpdated; // For UI updates when network pushes new state

	// static DeckManager() // Static constructor removed, LoadDecks called explicitly

	public static string SerializeDecksForNetwork()
	{
		var decksListDict = new Godot.Collections.Array();
		foreach (Deck deck in AvailableDecks)
		{
			decksListDict.Add(deck.ToDictionary());
		}
		return Json.Stringify(decksListDict);
	}

	public static void ApplyFullDeckStateFromNetwork(string jsonData)
	{
		AvailableDecks.Clear(); // Clear existing decks
		if (string.IsNullOrEmpty(jsonData))
		{
			OnDecksExternallyUpdated?.Invoke(); // Notify UI to clear itself
			GD.Print("DeckManager: Applied empty deck state from network.");
			return;
		}

		var parsed = Json.ParseString(jsonData);
		if (parsed.VariantType == Variant.Type.Array)
		{
			var decksArray = parsed.AsGodotArray();
			foreach (var deckVariant in decksArray)
			{
				if (deckVariant.VariantType == Variant.Type.Dictionary)
				{
					AvailableDecks.Add(Deck.FromDictionary(deckVariant.AsGodotDictionary()));
				}
			}
			GD.Print($"DeckManager: Applied full deck state from network. {AvailableDecks.Count} decks loaded.");
		}
		else
		{
			GD.PrintErr("DeckManager: Failed to parse deck state from network - root was not an array.");
		}
		OnDecksExternallyUpdated?.Invoke(); // Notify UI to refresh from the new DeckManager data
	}


	public static void CreateNewDeck(string name, bool addStandard52 = false)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			GD.PrintErr("Deck name cannot be empty.");
			return;
		}
		if (GetDeckByName(name) != null)
		{
			GD.PrintErr($"Deck with name '{name}' already exists.");
			return;
		}

		Deck newDeck = new Deck(name);
		if (addStandard52)
		{
			newDeck.AddCards(CreateStandard52Cards());
			newDeck.Shuffle();
		}
		AvailableDecks.Add(newDeck);
		SaveDecks();
		OnDecksChanged?.Invoke();
	}

	public static Deck GetDeckByName(string name)
	{
		return AvailableDecks.Find(deck => deck.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	public static void DeleteDeck(string name)
	{
		Deck deckToRemove = GetDeckByName(name);
		if (deckToRemove != null)
		{
			AvailableDecks.Remove(deckToRemove);
			SaveDecks();
			OnDecksChanged?.Invoke();
		}
		else
		{
			GD.PrintErr($"Deck '{name}' not found for deletion.");
		}
	}

	public static void SaveDecks()
	{
		var decksData = new Godot.Collections.Array();
		foreach (var deck in AvailableDecks)
		{
			decksData.Add(deck.ToDictionary());
		}

		string jsonString = Json.Stringify(decksData, "\t"); // Pretty print with tabs

		using var file = FileAccess.Open(DecksFilePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PrintErr($"Error opening file for saving decks: {FileAccess.GetOpenError()}");
			return;
		}
		file.StoreString(jsonString);
	}

	public static void LoadDecks()
	{
		if (!FileAccess.FileExists(DecksFilePath))
		{
			GD.Print("Decks file not found. No decks loaded.");
			AvailableDecks.Clear(); // Ensure list is empty if file doesn't exist
			OnDecksChanged?.Invoke();
			return;
		}

		using var file = FileAccess.Open(DecksFilePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"Error opening file for loading decks: {FileAccess.GetOpenError()}");
			AvailableDecks.Clear();
			OnDecksChanged?.Invoke();
			return;
		}

		string jsonString = file.GetAsText();
		if (string.IsNullOrWhiteSpace(jsonString))
		{
			GD.Print("Decks file is empty.");
			AvailableDecks.Clear();
			OnDecksChanged?.Invoke();
			return;
		}

		Json json = new Json();
		Error parseError = json.Parse(jsonString);
		if (parseError != Error.Ok)
		{
			GD.PrintErr($"Error parsing decks JSON: {parseError} at line {json.GetErrorLine()}: {json.GetErrorMessage()}");
			AvailableDecks.Clear();
			OnDecksChanged?.Invoke();
			return;
		}

		if (json.Data.VariantType != Variant.Type.Array)
		{
			GD.PrintErr("Invalid decks JSON format: root is not an array.");
			AvailableDecks.Clear();
			OnDecksChanged?.Invoke();
			return;
		}

		var loadedDecksArray = json.Data.AsGodotArray();
		AvailableDecks.Clear();
		foreach (var deckData in loadedDecksArray)
		{
			if (deckData.VariantType == Variant.Type.Dictionary)
			{
				AvailableDecks.Add(Deck.FromDictionary(deckData.AsGodotDictionary()));
			}
		}
		OnDecksChanged?.Invoke();
		GD.Print($"Loaded {AvailableDecks.Count} deck(s).");
	}

	private static List<Card> CreateStandard52Cards()
	{
		var cards = new List<Card>();
		string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
		string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };

		foreach (string suit in suits)
		{
			foreach (string rank in ranks)
			{
				cards.Add(new Card(rank, suit));
			}
		}
		return cards;
	}
}
