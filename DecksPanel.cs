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
		// Cache NetworkManager if in a multiplayer session
		if (Multiplayer.HasMultiplayerPeer())
		{
			_networkManagerCache = GetNodeOrNull<NetworkManager>("/root/MainScene/NetworkManagerNode"); // Added
			if (_networkManagerCache == null) GD.PrintErr("DecksPanel: NetworkManagerNode not found for server RPCs.");
		}


		// Connect signals for UI elements
		createDeckButton.Pressed += OnCreateDeckButtonPressed;
		deleteDeckButton.Pressed += OnDeleteDeckButtonPressed;
		deckListDisplay.ItemSelected += OnDeckListItemSelected;

		shuffleButton.Pressed += OnShuffleButtonPressed;
		drawButton.Pressed += OnDrawButtonPressed;
		resetDeckButton.Pressed += OnResetDeckButtonPressed;

		// Subscribe to DeckManager changes
		DeckManager.OnDecksChanged += UpdateDeckListDisplay; // For server's local changes
		DeckManager.OnDecksExternallyUpdated += UpdateDeckListDisplay; // For client's network updates

		if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
		{
			DeckManager.LoadDecks(); // Server/offline loads from file
		}
		// Clients will receive deck state from server via RPC and NetworkAllDecksStateReceived signal in MainScene.
		// MainScene will call DeckManager.ApplyFullDeckStateFromNetwork, which then invokes OnDecksExternallyUpdated.

		UpdateDeckListDisplay();
		UpdateSelectedDeckView();

		// Disable UI for clients
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
		{
			createDeckButton.Disabled = true;
			deleteDeckButton.Disabled = true;
			// shuffleButton, drawButton, resetButton are handled by UpdateSelectedDeckView based on _selectedDeck
			// but their actions should also be server-only. We will disable them if client.
			// ItemList selection can be disabled by setting FocusMode to None, but it might still be useful for viewing.
			// For now, action buttons being disabled is the main thing.
			if (deckListDisplay != null) deckListDisplay.FocusMode = FocusModeEnum.None; // Make non-interactive
		}
	}

	public override void _ExitTree()
	{
		DeckManager.OnDecksChanged -= UpdateDeckListDisplay;
		DeckManager.OnDecksExternallyUpdated -= UpdateDeckListDisplay;
	}

	private void UpdateDeckListDisplay() // This is now also called by OnDecksExternallyUpdated
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
			DeckManager.CreateNewDeck(deckName, true); // This calls SaveDecks and OnDecksChanged
			_chatLog?.AddMessage($"Deck '{deckName}' created.", Colors.Green);
			BroadcastDeckStateIfServer(); // Server broadcasts the new full state
		}
		else _chatLog?.AddMessage("Deck creation cancelled: Name was empty.", Colors.Orange);
	}

	private void OnDeleteDeckButtonPressed()
	{
		if (_selectedDeck == null) { _chatLog?.AddMessage("No deck selected to delete.", Colors.Yellow); return; }
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) { _chatLog?.AddMessage("Clients cannot delete decks.", Colors.OrangeRed); return; }

		string deckName = _selectedDeck.Name;
		DeckManager.DeleteDeck(deckName); // This calls SaveDecks and OnDecksChanged
		_chatLog?.AddMessage($"Deck '{deckName}' deleted.", Colors.OrangeRed);
		_selectedDeck = null;
		lastDrawnCardLabel.Text = "Last Drawn: -";
		BroadcastDeckStateIfServer();
	}

	private void OnShuffleButtonPressed()
	{
		if (_selectedDeck == null) return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) { _chatLog?.AddMessage("Clients cannot shuffle decks.", Colors.OrangeRed); return; }

		_soundManager?.PlaySfx("card_shuffle.wav.txt");
		_selectedDeck.Shuffle();
		DeckManager.SaveDecks(); // Save change
		_chatLog?.AddMessage($"Deck '{_selectedDeck.Name}' shuffled.", Colors.CornflowerBlue);
		lastDrawnCardLabel.Text = "Last Drawn: - (Shuffled)";
		UpdateSelectedDeckView(); // Update local UI
		BroadcastDeckStateIfServer();
	}

	private void OnDrawButtonPressed()
	{
		if (_selectedDeck == null) return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) { _chatLog?.AddMessage("Clients cannot draw cards.", Colors.OrangeRed); return; }

		Card drawnCard = _selectedDeck.DrawCard();
		DeckManager.SaveDecks(); // Save change (deck/discard state changed)
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
		UpdateSelectedDeckView(); // Update local UI
		BroadcastDeckStateIfServer();
	}

	private void OnResetDeckButtonPressed()
	{
		if (_selectedDeck == null) return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) { _chatLog?.AddMessage("Clients cannot reset decks.", Colors.OrangeRed); return; }

		_soundManager?.PlaySfx("card_shuffle.wav.txt");
		_selectedDeck.ResetDeck();
		DeckManager.SaveDecks(); // Save change
		_chatLog?.AddMessage($"Deck '{_selectedDeck.Name}' reset and shuffled.", Colors.CornflowerBlue);
		lastDrawnCardLabel.Text = "Last Drawn: - (Reset)";
		UpdateSelectedDeckView(); // Update local UI
		BroadcastDeckStateIfServer();
	}

	private void BroadcastDeckStateIfServer()
	{
		if (_networkManagerCache != null && _networkManagerCache.IsServer())
		{
			string allDecksJson = DeckManager.SerializeDecksForNetwork();
			_networkManagerCache.Rpc(nameof(NetworkManager.RpcClientReceiveAllDecksState), allDecksJson);
		}
	}
}
// Need to ensure _networkManagerCache is set in _Ready for DecksPanel
// Also, the Initialize method needs to accept it if it's passed from MainScene.
// For now, assuming DecksPanel gets it from /root/ path.
