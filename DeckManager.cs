using Godot;
using System;
using System.Collections.Generic;

public static class DeckManager
{
	public static List<Deck> AvailableDecks { get; private set; } = new List<Deck>();
	private const string DecksFilePath = "user://user_decks.json";
	public static event Action OnDecksChanged;

	static DeckManager()
	{
		// Load decks when the game starts or this class is first accessed
		// LoadDecks(); // Decided to call this explicitly from DecksPanel._Ready() to ensure UI is ready
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
