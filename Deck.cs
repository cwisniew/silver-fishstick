using Godot;
using System;
using System.Collections.Generic;
using System.Linq; // For ToList on IEnumerable

public class Deck
{
	public string Name { get; set; }
	public List<Card> CardsInDeck { get; private set; }
	public List<Card> DiscardPile { get; private set; }

	private static Random _rng = new Random();

	// Parameterless constructor for deserialization
	public Deck()
	{
		CardsInDeck = new List<Card>();
		DiscardPile = new List<Card>();
	}

	public Deck(string name) : this()
	{
		Name = name;
	}

	public void AddCard(Card card)
	{
		if (card != null)
		{
			CardsInDeck.Add(card);
		}
	}

	public void AddCards(IEnumerable<Card> cards)
	{
		if (cards != null)
		{
			CardsInDeck.AddRange(cards.Where(c => c != null));
		}
	}

	public void Shuffle()
	{
		// Fisher-Yates shuffle
		int n = CardsInDeck.Count;
		while (n > 1)
		{
			n--;
			int k = _rng.Next(n + 1);
			Card value = CardsInDeck[k];
			CardsInDeck[k] = CardsInDeck[n];
			CardsInDeck[n] = value;
		}
	}

	public Card DrawCard()
	{
		if (CardsInDeck.Count == 0)
		{
			if (DiscardPile.Count == 0)
			{
				return null; // Deck and discard pile are empty
			}
			// Reshuffle discard pile into deck
			CardsInDeck.AddRange(DiscardPile);
			DiscardPile.Clear();
			Shuffle();
		}

		if (CardsInDeck.Count > 0) // Should always be true if reshuffled, unless DiscardPile was also empty
		{
			Card drawnCard = CardsInDeck[0];
			CardsInDeck.RemoveAt(0);
			return drawnCard;
		}
		return null; // Should not be reached if logic is correct
	}

	public void Discard(Card card)
	{
		if (card != null)
		{
			DiscardPile.Add(card);
		}
	}

	public void ResetDeck()
	{
		CardsInDeck.AddRange(DiscardPile);
		DiscardPile.Clear();
		Shuffle();
	}

	// For Godot JSON serialization
	public Godot.Collections.Dictionary ToDictionary()
	{
		var deckDict = new Godot.Collections.Dictionary
		{
			{ "Name", Name }
		};

		var cardsArray = new Godot.Collections.Array();
		foreach (var card in CardsInDeck)
		{
			cardsArray.Add(card.ToDictionary());
		}
		deckDict["CardsInDeck"] = cardsArray;

		var discardArray = new Godot.Collections.Array();
		foreach (var card in DiscardPile)
		{
			discardArray.Add(card.ToDictionary());
		}
		deckDict["DiscardPile"] = discardArray;

		return deckDict;
	}

	public static Deck FromDictionary(Godot.Collections.Dictionary dict)
	{
		var deck = new Deck();
		deck.Name = dict.ContainsKey("Name") ? dict["Name"].ToString() : "Unnamed Deck";

		if (dict.ContainsKey("CardsInDeck") && dict["CardsInDeck"].VariantType == Variant.Type.Array)
		{
			var cardsArray = dict["CardsInDeck"].AsGodotArray();
			foreach (var cardData in cardsArray)
			{
				if (cardData.VariantType == Variant.Type.Dictionary)
				{
					deck.CardsInDeck.Add(Card.FromDictionary(cardData.AsGodotDictionary()));
				}
			}
		}

		if (dict.ContainsKey("DiscardPile") && dict["DiscardPile"].VariantType == Variant.Type.Array)
		{
			var discardArray = dict["DiscardPile"].AsGodotArray();
			foreach (var cardData in discardArray)
			{
				if (cardData.VariantType == Variant.Type.Dictionary)
				{
					deck.DiscardPile.Add(Card.FromDictionary(cardData.AsGodotDictionary()));
				}
			}
		}
		return deck;
	}
}
