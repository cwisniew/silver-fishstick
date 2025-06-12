using Godot;
using System;

public partial class DecksPanel : PanelContainer
{
	[Export] private ItemList deckListDisplay;
	[Export] private Button createDeckButton;
	[Export] private Button deleteDeckButton;
	[Export] private Label selectedDeckNameLabel;
	[Export] private Button shuffleButton;
	[Export] private Button drawButton;
	[Export] private Button resetDeckButton;
	[Export] private Label lastDrawnCardLabel;
	[Export] private ItemList discardPileDisplay;

	private Deck _selectedDeck = null;
	private ChatLog _chatLog; // For logging actions
	private SoundManager _soundManager; // For playing sounds
	private LineEdit _deckNameLineEdit; // For the creation dialog
	private AcceptDialog _createDeckDialog;

	public void Initialize(ChatLog chatLog)
	{
		_chatLog = chatLog;
	}

	public override void _Ready()
	{
		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager");
		if (_soundManager == null) GD.PrintErr("DecksPanel: SoundManager Autoload not found!");

		// Connect signals for UI elements
		createDeckButton.Pressed += OnCreateDeckButtonPressed;
		deleteDeckButton.Pressed += OnDeleteDeckButtonPressed;
		deckListDisplay.ItemSelected += OnDeckListItemSelected;

		shuffleButton.Pressed += OnShuffleButtonPressed;
		drawButton.Pressed += OnDrawButtonPressed;
		resetDeckButton.Pressed += OnResetDeckButtonPressed;

		// Subscribe to DeckManager changes
		DeckManager.OnDecksChanged += UpdateDeckListDisplay;
		DeckManager.LoadDecks(); // Load decks when panel is ready (this will also trigger OnDecksChanged)

		UpdateDeckListDisplay(); // Initial population
		UpdateSelectedDeckView(); // Set initial state of selected deck UI
	}

	public override void _ExitTree()
	{
		// Unsubscribe from static event when panel is removed
		DeckManager.OnDecksChanged -= UpdateDeckListDisplay;
	}

	private void UpdateDeckListDisplay()
	{
		deckListDisplay.Clear();
		foreach (var deck in DeckManager.AvailableDecks)
		{
			deckListDisplay.AddItem(deck.Name);
		}
		// If a deck was selected, try to reselect it if it still exists
		if (_selectedDeck != null)
		{
			int index = DeckManager.AvailableDecks.FindIndex(d => d.Name == _selectedDeck.Name);
			if (index != -1)
			{
				deckListDisplay.Select(index);
				deckListDisplay.EnsureCurrentIsVisible();
			}
			else
			{
				_selectedDeck = null; // Previously selected deck was deleted
			}
		}
		UpdateSelectedDeckView();
	}

	private void OnDeckListItemSelected(long index)
	{
		if (index >= 0 && index < DeckManager.AvailableDecks.Count)
		{
			_selectedDeck = DeckManager.AvailableDecks[(int)index];
		}
		else
		{
			_selectedDeck = null;
		}
		UpdateSelectedDeckView();
	}

	private void UpdateSelectedDeckView()
	{
		if (_selectedDeck != null)
		{
			selectedDeckNameLabel.Text = $"Selected: {_selectedDeck.Name} ({_selectedDeck.CardsInDeck.Count} left)";
			shuffleButton.Disabled = false;
			drawButton.Disabled = _selectedDeck.CardsInDeck.Count == 0 && _selectedDeck.DiscardPile.Count == 0;
			resetDeckButton.Disabled = _selectedDeck.DiscardPile.Count == 0;
			discardPileDisplay.Disabled = false;

			discardPileDisplay.Clear();
			foreach (var card in _selectedDeck.DiscardPile)
			{
				discardPileDisplay.AddItem(card.ToString());
			}
		}
		else
		{
			selectedDeckNameLabel.Text = "No Deck Selected";
			shuffleButton.Disabled = true;
			drawButton.Disabled = true;
			resetDeckButton.Disabled = true;
			lastDrawnCardLabel.Text = "Last Drawn: -";
			discardPileDisplay.Clear();
			discardPileDisplay.Disabled = true;
		}
	}

	private void OnCreateDeckButtonPressed()
	{
		if (_createDeckDialog == null)
		{
			_createDeckDialog = new AcceptDialog();
			_createDeckDialog.Title = "Create New Deck";
			_createDeckDialog.DialogHideOnOk = true; // Hide when OK is pressed

			VBoxContainer vbox = new VBoxContainer();
			_deckNameLineEdit = new LineEdit { PlaceholderText = "Enter deck name" };
			vbox.AddChild(new Label { Text = "Deck Name:"});
			vbox.AddChild(_deckNameLineEdit);
			// TODO: Add CheckBox for "Create Standard 52-card deck?"
			_createDeckDialog.AddChild(vbox);
			_createDeckDialog.Confirmed += HandleCreateDeckDialogConfirmed;
			AddChild(_createDeckDialog); // Add to scene tree to make it work
		}
		_deckNameLineEdit.Clear();
		_createDeckDialog.PopupCentered();
		_deckNameLineEdit.GrabFocus();
	}

	private void HandleCreateDeckDialogConfirmed()
	{
		string deckName = _deckNameLineEdit.Text.Trim();
		if (!string.IsNullOrEmpty(deckName))
		{
			// For now, always create a standard 52-card deck.
			// Later, could add a checkbox in the dialog to control this.
			DeckManager.CreateNewDeck(deckName, true);
			_chatLog?.AddMessage($"Deck '{deckName}' created.", Colors.Green);
			// UpdateDeckListDisplay is called by OnDecksChanged event
		}
		else
		{
			_chatLog?.AddMessage("Deck creation cancelled: Name was empty.", Colors.Orange);
		}
	}

	private void OnDeleteDeckButtonPressed()
	{
		if (_selectedDeck != null)
		{
			string deckName = _selectedDeck.Name; // Store name before it's potentially nulled
			DeckManager.DeleteDeck(deckName);
			_chatLog?.AddMessage($"Deck '{deckName}' deleted.", Colors.OrangeRed);
			_selectedDeck = null; // Clear selection
			lastDrawnCardLabel.Text = "Last Drawn: -";
			// UpdateDeckListDisplay is called by OnDecksChanged event
		}
		else
		{
			_chatLog?.AddMessage("No deck selected to delete.", Colors.Yellow);
		}
	}

	private void OnShuffleButtonPressed()
	{
		if (_selectedDeck != null)
		{
			_soundManager?.PlaySfx("card_shuffle.wav.txt");
			_selectedDeck.Shuffle();
			_chatLog?.AddMessage($"Deck '{_selectedDeck.Name}' shuffled.", Colors.CornflowerBlue);
			lastDrawnCardLabel.Text = "Last Drawn: - (Shuffled)";
			UpdateSelectedDeckView();
		}
	}

	private void OnDrawButtonPressed()
	{
		if (_selectedDeck != null)
		{
			Card drawnCard = _selectedDeck.DrawCard();
			if (drawnCard != null)
			{
				_soundManager?.PlaySfx("card_draw.wav.txt");
				_chatLog?.AddMessage($"Drew '{drawnCard}' from '{_selectedDeck.Name}'.", Colors.LightSeaGreen);
				lastDrawnCardLabel.Text = $"Last Drawn: {drawnCard}";
			}
			else
			{
				_chatLog?.AddMessage($"Deck '{_selectedDeck.Name}' is completely empty.", Colors.Yellow);
				lastDrawnCardLabel.Text = "Last Drawn: - (Empty)";
			}
			UpdateSelectedDeckView();
		}
	}

	private void OnResetDeckButtonPressed()
	{
		if (_selectedDeck != null)
		{
			_soundManager?.PlaySfx("card_shuffle.wav.txt");
			_selectedDeck.ResetDeck(); // Reshuffles discard into deck
			_chatLog?.AddMessage($"Deck '{_selectedDeck.Name}' reset and shuffled.", Colors.CornflowerBlue);
			lastDrawnCardLabel.Text = "Last Drawn: - (Reset)";
			UpdateSelectedDeckView();
		}
	}
}
