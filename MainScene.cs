using Godot;
using System;
using System.Collections.Generic; // For List
using System.Linq; // For Select

public partial class MainScene : Node2D
{
	// Exports for UI elements (condensed for brevity, ensure all are present)
	[Export] private ChatLog chatLogNode;
	[Export] private LineEdit chatMessageInput;
	[Export] private CombatTracker combatTracker;
	[Export] private Button addTokenButton, nextTurnButton, startCombatButton, resetCombatButton;
	[Export] private Button changeTokenImageButton; [Export] private FileDialog tokenImageFileDialog;
	[Export] private Sprite2D mapBackgroundSprite;
	[Export] private Button loadMapButton; [Export] private FileDialog mapFileDialog;
	[Export] private DrawingOverlay drawingOverlay;
	[Export] private Button toggleDrawModeButton, clearDrawingsButton, toggleMeasureModeButton;
	[Export] private Button shareHandoutButton; [Export] private FileDialog handoutImageFileDialog;
	[Export] private PackedScene handoutDisplayScene;
	[Export] private PanelContainer notesPanel; [Export] private Button toggleNotesButton; [Export] private TextEdit notesTextEdit;
	[Export] private DecksPanel decksPanel; [Export] private Button toggleDecksPanelButton;
	[Export] private Button toggleMusicButton;
	[Export] private PanelContainer macroPanel; [Export] private Button toggleMacroPanelButton; [Export] private TextEdit macroInputTextEdit; [Export] private Button runMacroButton;
	[Export] private FileDialog campaignFileDialog; [Export] private Button saveCampaignButton, loadCampaignButton;
	[Export] private NetworkManager networkManagerNode;
	[Export] private LineEdit serverIpInput, portInput;
	[Export] private Button hostButton, joinButton, disconnectButton;
	[Export] private Label networkStatusLabel;
	[Export] private ItemList playerListDisplay;
	[Export] private Button spawnTestNetworkTokenButton, testModifySheetButton, assignOwnerButton;
	[Export] private TokenDetailsEditorPanel tokenDetailsEditorPanel; // Renamed from statusEffectEditorPanel
	[Export] private Button toggleTokenDetailsEditorButton; // Renamed from toggleTokenFxEditorButton

	private Token _selectedToken = null;
	private Token _locallyDraggedToken = null;
	private PackedScene _tokenScene;
	private bool _isMusicPlaying = false;
	private SoundManager _soundManager;
	private const string NotesFilePath = "user://user_notes.cfg";
	private const string CampaignFileExtension = ".vttcamp";
	private bool _isMeasureModeActive = false;
	private Vector2 _measurementStartPoint = Vector2.Zero, _measurementEndPoint = Vector2.Zero;
	private bool _isMeasuring = false;

	[Export] private float pixelsPerUnit = 50.0f;
	private const float GameUnitsPerGridSquare = 5.0f;
	private const string GameUnitName = "ft";

	private AcceptDialog _initiativeDialog; private LineEdit _initiativeLineEdit;
	private bool _isTokenCurrentlyBeingDragged = false; private bool _dragWasActuallyMovement = false;

	public ChatLog ChatLogNode => chatLogNode;
    public Sprite2D MapBackgroundSprite => mapBackgroundSprite;
    public CombatTracker CombatTracker => combatTracker;
    public DrawingOverlay DrawingOverlayNode => drawingOverlay;


	public override void _Ready()
	{
		if (chatLogNode == null) GD.PrintErr("ChatLog node not found.");
		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager");
		if (networkManagerNode == null) GD.PrintErr("NetworkManagerNode not found!");
		if (combatTracker == null) GD.PrintErr("CombatTracker node not found.");
		else combatTracker.Initialize(chatLogNode, _soundManager, networkManagerNode, GameUnitName);

		if (tokenDetailsEditorPanel == null) GD.PrintErr("TokenDetailsEditorPanel not found!");
		else
		{
			tokenDetailsEditorPanel.AddStatusEffectRequested += OnAddStatusEffectRequested_Server;
			tokenDetailsEditorPanel.RemoveStatusEffectRequested += OnRemoveStatusEffectRequested_Server;
			// Connect new inventory signals
			tokenDetailsEditorPanel.AddItemToInventoryRequested += OnAddItemToInventoryRequested_Server;
			tokenDetailsEditorPanel.RemoveItemFromInventoryRequested += OnRemoveItemFromInventoryRequested_Server;
			tokenDetailsEditorPanel.ToggleEquipItemRequested += OnToggleEquipItemRequested_Server;
		}

		Action<Button, Action, string> connectBtn = (btn, handler, name) => { if (btn != null) btn.Pressed += handler; else GD.PrintErr($"{name} button not found for connection in _Ready."); };
		connectBtn(addTokenButton, OnAddSelectedTokenPressed, "AddToken");
		connectBtn(nextTurnButton, () => combatTracker?.NextTurn(), "NextTurn");
		connectBtn(startCombatButton, () => combatTracker?.StartCombat(), "StartCombat");
		// ... (rest of button connections, ensure toggleTokenDetailsEditorButton is connected) ...
		connectBtn(resetCombatButton, () => combatTracker?.ResetCombat(), "ResetCombat");
		connectBtn(changeTokenImageButton, OnChangeTokenImageButtonPressed, "ChangeTokenImage");
		connectBtn(loadMapButton, OnLoadMapButtonPressed, "LoadMap");
		connectBtn(toggleDrawModeButton, OnToggleDrawModeButtonPressed, "ToggleDrawMode");
		connectBtn(clearDrawingsButton, OnClearDrawingsButtonPressed, "ClearDrawings");
		connectBtn(toggleMeasureModeButton, OnToggleMeasureModeButtonPressed, "ToggleMeasureMode");
		connectBtn(shareHandoutButton, OnShareHandoutButtonPressed, "ShareHandout");
		connectBtn(toggleNotesButton, OnToggleNotesButtonPressed, "ToggleNotes");
		connectBtn(toggleDecksPanelButton, OnToggleDecksPanelButtonPressed, "ToggleDecksPanel");
		connectBtn(toggleMusicButton, OnToggleMusicButtonPressed, "ToggleMusic");
		connectBtn(toggleMacroPanelButton, OnToggleMacroPanelButtonPressed, "ToggleMacroPanel");
		connectBtn(runMacroButton, OnRunMacroButtonPressed, "RunMacro");
		connectBtn(saveCampaignButton, OnSaveCampaignButtonPressed, "SaveCampaign");
		connectBtn(loadCampaignButton, OnLoadCampaignButtonPressed, "LoadCampaign");
		connectBtn(spawnTestNetworkTokenButton, OnSpawnTestNetworkTokenButtonPressed, "SpawnTestNetworkToken");
		connectBtn(testModifySheetButton, OnTestModifySheetButtonPressed_Server, "TestModifySheet");
		connectBtn(assignOwnerButton, OnAssignOwnerButtonPressed_GM, "AssignOwnerButton");
		connectBtn(toggleTokenDetailsEditorButton, OnToggleTokenDetailsEditorButtonPressed, "ToggleTokenDetailsEditorButton"); // Renamed from OnToggleTokenFxEditorButtonPressed

		if (tokenImageFileDialog != null) tokenImageFileDialog.FileSelected += OnTokenImageFileSelected;
		// ... (other FileDialog, TextEdit, etc. connections) ...
		if (mapFileDialog != null) mapFileDialog.FileSelected += OnMapFileSelected;
		if (handoutImageFileDialog != null) handoutImageFileDialog.FileSelected += OnHandoutImageFileSelected;
		if (notesTextEdit != null) notesTextEdit.TextChanged += OnNotesTextChanged;
		if (decksPanel != null) decksPanel.Initialize(chatLogNode);
		if (campaignFileDialog != null) campaignFileDialog.FileSelected += OnCampaignFileSelected;

		if (networkManagerNode != null) { /* ... (all existing NetworkManager signal connections) ... */ }
		if (hostButton != null) hostButton.Pressed += OnHostButtonPressed;
		if (joinButton != null) joinButton.Pressed += OnJoinButtonPressed;
		if (disconnectButton != null) disconnectButton.Pressed += OnDisconnectButtonPressed;

		chatLogNode?.AddMessage("System: Welcome to the VTT!", Colors.Aqua);
		LoadNotes();
		if (_soundManager != null) { _soundManager.PlayMusic("ambient_music.ogg.txt"); _isMusicPlaying = true; UpdateMusicButtonText(); }
		_tokenScene = GD.Load<PackedScene>("res://Token.tscn");
		if (_tokenScene == null) GD.PrintErr("Failed to load Token.tscn");

		UpdateUiForNetworkRole();
		// TestCharacterSheetFullSerialization(); // Comment out after initial test
		// TestStatusEffectLogic();
		// TestInventoryItemSerialization();
	}

	private void UpdateUiForNetworkRole()
	{
		bool isClient = networkManagerNode != null && Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer();
		// ... (GM-Only Buttons) ...
		if (loadMapButton != null) loadMapButton.Disabled = isClient;
		if (spawnTestNetworkTokenButton != null) spawnTestNetworkTokenButton.Disabled = isClient;
		if (testModifySheetButton != null) testModifySheetButton.Disabled = isClient;
		if (shareHandoutButton != null) shareHandoutButton.Disabled = isClient;
		if (assignOwnerButton != null) assignOwnerButton.Disabled = isClient;
		if (saveCampaignButton != null) saveCampaignButton.Disabled = isClient;
		if (loadCampaignButton != null) loadCampaignButton.Disabled = isClient;
		if (toggleTokenDetailsEditorButton != null) toggleTokenDetailsEditorButton.Disabled = isClient; // Updated name

		// ... (Combat Tracker Controls) ...
		Node combatControlsParent = GetNodeOrNull("CombatTrackerPanel/VBoxContainer/ControlsHBox");
		if (combatControlsParent != null) { /* ... disable buttons ... */ }

		// ... (Drawing Controls) ...
		if (toggleDrawModeButton != null) { /* ... */ }
		if (clearDrawingsButton != null) clearDrawingsButton.Disabled = isClient;

		// ... (Other UI states) ...
		if (isClient && tokenDetailsEditorPanel != null) tokenDetailsEditorPanel.Visible = false; // Hide for client
		if (hostButton != null) hostButton.Disabled = (networkManagerNode != null && Multiplayer.HasMultiplayerPeer());
		if (joinButton != null) joinButton.Disabled = (networkManagerNode != null && Multiplayer.HasMultiplayerPeer());
		if (disconnectButton != null) disconnectButton.Disabled = !(networkManagerNode != null && Multiplayer.HasMultiplayerPeer());
	}

	private void OnTokenInputEvent(Node viewport, InputEvent @event, int shapeIdx, Token tokenInstance)
	{
		if (tokenInstance == null) return;
		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseButtonEvent.Pressed)
			{
				_isTokenCurrentlyBeingDragged = true; _dragWasActuallyMovement = false;
				_locallyDraggedToken = tokenInstance; _locallyDraggedToken.StartDrag();
				if (_selectedToken != tokenInstance)
				{
					if (_selectedToken != null) _selectedToken.SetSelectionVisual(false);
					_selectedToken = tokenInstance; _selectedToken.SetSelectionVisual(true);
					chatLogNode?.AddMessage($"Selected: '{_selectedToken.Sheet?.Name ?? _selectedToken.Name}'.", Colors.Cyan);
				}
				tokenDetailsEditorPanel?.SetTargetToken(_selectedToken); // Update Details Editor target
				GetViewport().SetInputAsHandled();
			} else { /* ... (rest of existing OnTokenInputEvent release logic) ... */ }
		}
	}

	private void BroadcastCharacterSheetUpdate(Token token)
	{
		if (token == null || token.Sheet == null || networkManagerNode == null || !networkManagerNode.IsServer()) return;
		var sheetDict = new Godot.Collections.Dictionary();
		token.Sheet.ToDictionary(sheetDict);
		string updatedSheetJson = Json.Stringify(sheetDict);
		networkManagerNode.Rpc(nameof(NetworkManager.RpcClientReceiveFullSheetUpdate), token.Name.ToString(), updatedSheetJson);
	}

	// --- Status Effect & Inventory Editor Event Handlers (Server-Side) ---
	private void OnAddStatusEffectRequested_Server(Token targetToken, StatusEffect effectToAdd)
	{
		if (networkManagerNode == null || !networkManagerNode.IsServer() || targetToken == null || !IsInstanceValid(targetToken) || effectToAdd == null) return;
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (targetToken.Sheet == null) targetToken.Sheet = new CharacterSheet();
		if (targetToken.Sheet.AddStatusEffect(effectToAdd))
		{
			chatLogNode?.AddMessage($"Server: Added/Updated '{effectToAdd.Name}' on '{targetToken.Name}'.", Colors.Orange);
			BroadcastCharacterSheetUpdate(targetToken);
			tokenDetailsEditorPanel?.SetTargetToken(targetToken);
		}
	}
	private void OnRemoveStatusEffectRequested_Server(Token targetToken, string effectNameToRemove)
	{
		if (networkManagerNode == null || !networkManagerNode.IsServer() || targetToken == null || !IsInstanceValid(targetToken) || string.IsNullOrEmpty(effectNameToRemove)) return;
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (targetToken.Sheet != null && targetToken.Sheet.RemoveStatusEffect(effectNameToRemove))
		{
			chatLogNode?.AddMessage($"Server: Removed '{effectNameToRemove}' from '{targetToken.Name}'.", Colors.Orange);
			BroadcastCharacterSheetUpdate(targetToken);
			tokenDetailsEditorPanel?.SetTargetToken(targetToken);
		} else chatLogNode?.AddMessage($"Server: Effect '{effectNameToRemove}' not found on '{targetToken.Name}'.", Colors.Yellow);
	}

	private void OnAddItemToInventoryRequested_Server(Token targetToken, InventoryItem itemData)
	{
		if (networkManagerNode == null || !networkManagerNode.IsServer() || targetToken == null || !IsInstanceValid(targetToken) || itemData == null) return;
		_soundManager?.PlaySfx("ui_click.wav.txt"); // Or a different sound for item add
		if (targetToken.Sheet == null) targetToken.Sheet = new CharacterSheet();
		if (targetToken.Sheet.AddItem(itemData))
		{
			chatLogNode?.AddMessage($"Server: Added '{itemData.Name}' (Qty: {itemData.Quantity}) to '{targetToken.Name}'.", Colors.Orange);
			BroadcastCharacterSheetUpdate(targetToken);
			tokenDetailsEditorPanel?.SetTargetToken(targetToken); // Refresh panel
		}
	}
	private void OnRemoveItemFromInventoryRequested_Server(Token targetToken, string itemName, int quantity)
	{
		if (networkManagerNode == null || !networkManagerNode.IsServer() || targetToken == null || !IsInstanceValid(targetToken) || string.IsNullOrEmpty(itemName)) return;
		_soundManager?.PlaySfx("ui_click.wav.txt"); // Or a different sound for item remove
		if (targetToken.Sheet != null && targetToken.Sheet.RemoveItem(itemName, quantity)) // Quantity currently ignored by RemoveItem, removes whole stack
		{
			chatLogNode?.AddMessage($"Server: Removed '{itemName}' from '{targetToken.Name}'.", Colors.Orange);
			BroadcastCharacterSheetUpdate(targetToken);
			tokenDetailsEditorPanel?.SetTargetToken(targetToken); // Refresh panel
		} else chatLogNode?.AddMessage($"Server: Item '{itemName}' not found in '{targetToken.Name}' inventory.", Colors.Yellow);
	}
	private void OnToggleEquipItemRequested_Server(Token targetToken, string itemName)
	{
		if (networkManagerNode == null || !networkManagerNode.IsServer() || targetToken == null || !IsInstanceValid(targetToken) || string.IsNullOrEmpty(itemName)) return;
		_soundManager?.PlaySfx("ui_click.wav.txt"); // Or a different sound for equip
		if (targetToken.Sheet != null)
		{
			InventoryItem item = targetToken.Sheet.Inventory.FirstOrDefault(i => i.Name == itemName);
			if (item != null && item.IsEquippable)
			{
				targetToken.Sheet.ToggleEquipItem(itemName); // This updates item.IsEquipped
				chatLogNode?.AddMessage($"Server: Toggled equip status for '{itemName}' on '{targetToken.Name}'. Now: {(item.IsEquipped ? "Equipped" : "Unequipped")}", Colors.Orange);
				BroadcastCharacterSheetUpdate(targetToken);
				tokenDetailsEditorPanel?.SetTargetToken(targetToken); // Refresh panel
			} else if (item != null && !item.IsEquippable) {
				chatLogNode?.AddMessage($"Server: Item '{itemName}' on '{targetToken.Name}' is not equippable.", Colors.Yellow);
			} else {
				chatLogNode?.AddMessage($"Server: Item '{itemName}' not found in '{targetToken.Name}' inventory for equip toggle.", Colors.Yellow);
			}
		}
	}

	private void OnToggleTokenDetailsEditorButtonPressed() // Renamed from OnToggleTokenFxEditorButtonPressed
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (tokenDetailsEditorPanel == null) return;
		tokenDetailsEditorPanel.Visible = !tokenDetailsEditorPanel.Visible;
		if (tokenDetailsEditorPanel.Visible)
		{
			tokenDetailsEditorPanel.SetTargetToken(_selectedToken);
			chatLogNode?.AddMessage("Token Details Editor shown.", Colors.DarkCyan);
		} else chatLogNode?.AddMessage("Token Details Editor hidden.", Colors.DarkCyan);
	}

	// --- Placeholder for ALL other methods ---
	// [ Full list of other methods as stubs or full implementations as they were before this subtask ]
	private void TestCharacterSheetFullSerialization() { /* ... */ }
	private void TestSingleItem(InventoryItem originalItem, string testLabel) { /* ... */ }
	private void TestInventoryItemSerialization() { /* ... */ }
	private void TestStatusEffectLogic() { /* ... */ }
	// ... (and so on for all other methods)
}
