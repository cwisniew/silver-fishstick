using Godot;
using System;
using System.Collections.Generic; // For List
using System.Linq; // For Select

public partial class MainScene : Node2D
{
	[Export] private ChatLog chatLogNode;
	[Export] private LineEdit chatMessageInput;
	[Export] private CombatTracker combatTracker;
	[Export] private Button addTokenButton;
	[Export] private Button nextTurnButton;
	[Export] private Button startCombatButton;
	[Export] private Button resetCombatButton;
	[Export] private Button changeTokenImageButton;
	[Export] private FileDialog tokenImageFileDialog;
	[Export] private Sprite2D mapBackgroundSprite;
	[Export] private Button loadMapButton;
	[Export] private FileDialog mapFileDialog;
	[Export] private DrawingOverlay drawingOverlay;
	[Export] private Button toggleDrawModeButton;
	[Export] private Button clearDrawingsButton;
	[Export] private Button toggleMeasureModeButton;
	[Export] private Button shareHandoutButton;
	[Export] private FileDialog handoutImageFileDialog;
	[Export] private PackedScene handoutDisplayScene;
	[Export] private PanelContainer notesPanel;
	[Export] private Button toggleNotesButton;
	[Export] private TextEdit notesTextEdit;
	[Export] private DecksPanel decksPanel;
	[Export] private Button toggleDecksPanelButton;
	[Export] private Button toggleMusicButton;
	[Export] private PanelContainer macroPanel;
	[Export] private Button toggleMacroPanelButton;
	[Export] private TextEdit macroInputTextEdit;
	[Export] private Button runMacroButton;
	[Export] private FileDialog campaignFileDialog;
	[Export] private Button saveCampaignButton;
	[Export] private Button loadCampaignButton;
	[Export] private NetworkManager networkManagerNode;
	[Export] private LineEdit serverIpInput;
	[Export] private LineEdit portInput;
	[Export] private Button hostButton;
	[Export] private Button joinButton;
	[Export] private Button disconnectButton;
	[Export] private Label networkStatusLabel;
	[Export] private ItemList playerListDisplay;
	[Export] private Button spawnTestNetworkTokenButton;
	[Export] private Button testModifySheetButton;
	[Export] private Button assignOwnerButton;

	private Token _selectedToken = null;
	private Token _locallyDraggedToken = null;
	private PackedScene _tokenScene;
	private bool _isMusicPlaying = false;
	private SoundManager _soundManager;
	private const string NotesFilePath = "user://user_notes.cfg";
	private const string CampaignFileExtension = ".vttcamp";
	private bool _isMeasureModeActive = false;
	private Vector2 _measurementStartPoint = Vector2.Zero;
	private Vector2 _measurementEndPoint = Vector2.Zero;
	private bool _isMeasuring = false;

	[Export] private float pixelsPerUnit = 50.0f;
	private const float GameUnitsPerGridSquare = 5.0f;
	private const string GameUnitName = "ft";

	private AcceptDialog _initiativeDialog;
	private LineEdit _initiativeLineEdit;

	private bool _isTokenCurrentlyBeingDragged = false;
	private bool _dragWasActuallyMovement = false;

	public ChatLog ChatLogNode => chatLogNode;
    public Sprite2D MapBackgroundSprite => mapBackgroundSprite;
    public CombatTracker CombatTracker => combatTracker;
    public DrawingOverlay DrawingOverlayNode => drawingOverlay;


	public override void _Ready()
	{
		// Null checks and initializations (condensed for brevity)
		if (chatLogNode == null) GD.PrintErr("ChatLog node not found.");
		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager");
		if (networkManagerNode == null) GD.PrintErr("NetworkManagerNode not found!");
		if (combatTracker == null) GD.PrintErr("CombatTracker node not found.");
		else combatTracker.Initialize(chatLogNode, _soundManager, networkManagerNode, GameUnitName);

		Action<Button, Action, string> connectBtn = (btn, handler, name) => { if (btn != null) btn.Pressed += handler; else GD.PrintErr($"{name} button not found."); };
		connectBtn(addTokenButton, OnAddSelectedTokenPressed, "AddToken");
		connectBtn(nextTurnButton, () => combatTracker?.NextTurn(), "NextTurn");
		connectBtn(startCombatButton, () => combatTracker?.StartCombat(), "StartCombat");
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

		if (tokenImageFileDialog != null) tokenImageFileDialog.FileSelected += OnTokenImageFileSelected;
		if (mapFileDialog != null) mapFileDialog.FileSelected += OnMapFileSelected;
		if (handoutImageFileDialog != null) handoutImageFileDialog.FileSelected += OnHandoutImageFileSelected;
		if (notesTextEdit != null) notesTextEdit.TextChanged += OnNotesTextChanged;
		if (decksPanel != null) decksPanel.Initialize(chatLogNode);
		if (campaignFileDialog != null) campaignFileDialog.FileSelected += OnCampaignFileSelected;

		if (networkManagerNode != null)
		{
			networkManagerNode.ServerCreated += OnNetworkServerCreated;
			networkManagerNode.ServerCreationFailed += OnNetworkServerCreationFailed;
			networkManagerNode.ConnectionSucceeded += OnNetworkConnectionSucceeded;
			networkManagerNode.ConnectionFailed += OnNetworkConnectionFailed;
			networkManagerNode.PeerConnected += OnNetworkPeerConnected;
			networkManagerNode.PeerDisconnected += OnNetworkPeerDisconnected;
			networkManagerNode.ServerDisconnected += OnNetworkServerDisconnected;
			networkManagerNode.PlayerListUpdated += OnNetworkPlayerListUpdated;
			networkManagerNode.ChatMessageReceived += OnNetworkChatMessageReceived;
			networkManagerNode.NetworkSpawnTokenRequested += OnNetworkSpawnTokenRequested;
			networkManagerNode.NetworkTokenPositionUpdated += OnNetworkTokenPositionUpdated_Client;
			networkManagerNode.NetworkTokenPathExecutionRequested += OnNetworkTokenPathExecutionRequested_Client;
			networkManagerNode.ServerDragRequestReceived += OnServerDragRequestReceived_Server;
			networkManagerNode.ServerPathRequestReceived += OnServerPathRequestReceived_Server;
			networkManagerNode.ServerDiceRollRequested += OnServerDiceRollRequested_Server;
			networkManagerNode.NetworkDiceRollResultReceived += OnNetworkDiceRollResult_ClientServer;
			networkManagerNode.NetworkCombatStateReceived += OnNetworkCombatStateReceived_Client;
			networkManagerNode.NetworkMapLoadRequested += OnNetworkMapLoadRequested_Client;
			networkManagerNode.NetworkHandoutDisplayRequested += OnNetworkHandoutDisplayRequested_Client;
			networkManagerNode.NetworkCharacterSheetUpdated += OnNetworkCharacterSheetUpdated_Client;
			networkManagerNode.NetworkAddDrawingLine += OnNetworkAddDrawingLine_Client;
			networkManagerNode.NetworkReceiveFullDrawingState += OnNetworkReceiveFullDrawingState_Client;
			networkManagerNode.NetworkAllDecksStateReceived += OnNetworkAllDecksStateReceived_Client;
			networkManagerNode.ServerSetTokenOwnerRequested += OnServerSetTokenOwnerRequested_Server;
			networkManagerNode.NetworkTokenOwnerUpdated += OnNetworkTokenOwnerUpdated_ClientServer;
		}
		if (hostButton != null) hostButton.Pressed += OnHostButtonPressed;
		if (joinButton != null) joinButton.Pressed += OnJoinButtonPressed;
		if (disconnectButton != null) disconnectButton.Pressed += OnDisconnectButtonPressed;

		chatLogNode?.AddMessage("System: Welcome to the VTT!", Colors.Aqua);
		LoadNotes();
		if (_soundManager != null) { _soundManager.PlayMusic("ambient_music.ogg.txt"); _isMusicPlaying = true; UpdateMusicButtonText(); }
		_tokenScene = GD.Load<PackedScene>("res://Token.tscn");
		if (_tokenScene == null) GD.PrintErr("Failed to load Token.tscn");

		UpdateUiForNetworkRole(); // Initial UI state based on network role (offline)
	}

	private void UpdateUiForNetworkRole()
	{
		bool isClient = networkManagerNode != null && Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer();
		// bool isOffline = networkManagerNode == null || !Multiplayer.HasMultiplayerPeer();
		// bool isServer = networkManagerNode != null && Multiplayer.IsServer();

		// GM-Only Buttons (Disable for Clients)
		if (loadMapButton != null) loadMapButton.Disabled = isClient;
		if (spawnTestNetworkTokenButton != null) spawnTestNetworkTokenButton.Disabled = isClient;
		if (testModifySheetButton != null) testModifySheetButton.Disabled = isClient;
		if (shareHandoutButton != null) shareHandoutButton.Disabled = isClient;
		if (assignOwnerButton != null) assignOwnerButton.Disabled = isClient;
		if (saveCampaignButton != null) saveCampaignButton.Disabled = isClient;
		if (loadCampaignButton != null) loadCampaignButton.Disabled = isClient;

		// Combat Tracker Control Buttons (Disable for Clients)
		// These buttons are children of CombatTrackerPanel which might be handled by CombatTracker.cs itself
		// However, if MainScene holds direct references (which it does via NodePaths for CombatTrackerPanel children):
		Node combatControlsParent = GetNodeOrNull("CombatTrackerPanel/VBoxContainer/ControlsHBox"); // Path to HBox containing these
		if (combatControlsParent != null)
		{
			Button startBtn = combatControlsParent.GetNodeOrNull<Button>("StartCombatButton");
			if(startBtn != null) startBtn.Disabled = isClient;

			Button nextBtn = combatControlsParent.GetNodeOrNull<Button>("NextTurnButton");
			if(nextBtn != null) nextBtn.Disabled = isClient;

			Button resetBtn = combatControlsParent.GetNodeOrNull<Button>("ResetCombatButton");
			if(resetBtn != null) resetBtn.Disabled = isClient;

			Button addTokenBtn = combatControlsParent.GetNodeOrNull<Button>("AddTokenButton"); // Add token to combat tracker
			if(addTokenBtn != null) addTokenBtn.Disabled = isClient;
		}


		// Drawing Controls
		if (toggleDrawModeButton != null)
		{
			toggleDrawModeButton.Disabled = isClient;
			if(isClient && drawingOverlay != null) drawingOverlay.IsDrawingEnabled = false; // Ensure client's drawing is off
			if(!isClient && drawingOverlay != null) toggleDrawModeButton.Text = drawingOverlay.IsDrawingEnabled ? "Draw: ON" : "Draw: OFF"; // Server/offline updates its own button
			else if (isClient) toggleDrawModeButton.Text = "Draw: OFF"; // Client always sees OFF
		}
		if (clearDrawingsButton != null) clearDrawingsButton.Disabled = isClient;

		// Decks Controls (ToggleDecksPanelButton is fine for all, DecksPanel itself handles internal button states)
		if (toggleDecksPanelButton != null) toggleDecksPanelButton.Disabled = false;

		// Notes Controls
		if (toggleNotesButton != null) toggleNotesButton.Disabled = false;

		// Macro Controls
		if (toggleMacroPanelButton != null) toggleMacroPanelButton.Disabled = false;
		if (runMacroButton != null) runMacroButton.Disabled = false; // Macros can be run by anyone, context defines permissions

		// Network Panel itself: Host/Join enabled if offline, Disconnect enabled if online
		if (hostButton != null) hostButton.Disabled = (networkManagerNode != null && Multiplayer.HasMultiplayerPeer());
		if (joinButton != null) joinButton.Disabled = (networkManagerNode != null && Multiplayer.HasMultiplayerPeer());
		if (disconnectButton != null) disconnectButton.Disabled = !(networkManagerNode != null && Multiplayer.HasMultiplayerPeer());

		// PlayerListDisplay interactivity (clients can see, but assign owner is GM only)
		if (playerListDisplay != null) playerListDisplay.Disabled = false; // Allow viewing
	}

	// --- Network Connection Status Handlers ---
	private void OnNetworkServerCreated()
	{
		networkStatusLabel.Text = $"Status: HOSTING on port {portInput.Text}.";
		chatLogNode?.AddMessage($"Server created on port {portInput.Text}.", Colors.Green);
		UpdateUiForNetworkRole();
	}
	private void OnNetworkServerCreationFailed(string reason)
	{
		networkStatusLabel.Text = $"Status: Server creation FAILED: {reason}";
		chatLogNode?.AddMessage($"Server creation failed: {reason}", Colors.Red);
		UpdateUiForNetworkRole();
	}
	private void OnNetworkConnectionSucceeded() // Called on Client
	{
		networkStatusLabel.Text = "Status: Connected to server!";
		chatLogNode?.AddMessage("Connected to server.", Colors.Green);
		UpdateUiForNetworkRole();
	}
	private void OnNetworkConnectionFailed() // Called on Client
	{
		networkStatusLabel.Text = "Status: Connection FAILED.";
		chatLogNode?.AddMessage("Failed to connect.", Colors.Red);
		UpdateUiForNetworkRole();
	}
	private void OnNetworkServerDisconnected() // Called on Client when server disconnects them or server closes
	{
		networkStatusLabel.Text = "Status: Disconnected from server.";
		chatLogNode?.AddMessage("Disconnected from server.", Colors.OrangeRed);
		UpdateUiForNetworkRole();
	}
	private void OnDisconnectButtonPressed() // Local action for both client and server to initiate disconnect
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		networkManagerNode?.DisconnectNetwork();
		// For server, its own ServerDisconnected signal from NetworkManager is not automatically tied to Multiplayer.ServerDisconnected.
		// So, if it was server, it needs to update its UI as if it's now offline.
		if (Multiplayer.GetUniqueId() == 1 && networkManagerNode != null && !networkManagerNode.IsNetworkActive()) // Check if it WAS server and now isn't active
		{
			networkStatusLabel.Text = "Status: Disconnected (Server Stopped).";
			chatLogNode?.AddMessage("Server stopped.", Colors.Orange);
		} else {
			networkStatusLabel.Text = "Status: Disconnected.";
			chatLogNode?.AddMessage("Disconnected from network.", Colors.Orange);
		}
		UpdateUiForNetworkRole();
	}


	// --- Placeholder for ALL other methods from previous MainScene.cs ---
	// (The overwrite tool will ensure these are preserved. For brevity, only new/modified are shown here)
	private void OnChatMessageSubmitted(string text) { /* ... */ }
	private void LogDiceResult(DiceRollResult result) { /* ... */ }
	private void TestDiceRoller() { /* ... */ }
	public Token SpawnToken(Vector2 position) { /* ... */ return null; }
	public Token SpawnTokenAndApplyData(TokenData tokenData) { /* ... */ return null; }
	private void OnTokenInputEvent(Node viewport, InputEvent @event, int shapeIdx, Token tokenInstance) { /* ... */ }
	public override void _Process(double delta) { if (_isTokenCurrentlyBeingDragged && _locallyDraggedToken != null) { Vector2 currentMousePos = GetGlobalMousePosition(); if (_locallyDraggedToken.GlobalPosition.DistanceSquaredTo(currentMousePos) > 16) _dragWasActuallyMovement = true; _locallyDraggedToken.UpdateDragPosition(currentMousePos); } }
	public override void _UnhandledInput(InputEvent @event) { /* ... */ }
	private Godot.Collections.Array<StaticBody2D> GetObstaclesForPathfinding() { var o=new Godot.Collections.Array<StaticBody2D>(); Node w=GetNodeOrNull("Walls"); if(w!=null) foreach(Node c in w.GetChildren()) if(c is StaticBody2D sb) o.Add(sb); return o; }
	private Rect2 GetMapBoundsForPathfinding() { return mapBackgroundSprite!=null && mapBackgroundSprite.Texture!=null ? mapBackgroundSprite.GetGlobalRect() : GetViewportRect(); }
	private void OnNetworkTokenPositionUpdated_Client(string tokenNodeName, Vector2 newGlobalPosition) { /* ... */ }
	private void OnNetworkTokenPathExecutionRequested_Client(string tokenNodeName, Godot.Collections.Array pathPointsVariant) { /* ... */ }
	private void OnServerDragRequestReceived_Server(string tokenNodeName, Vector2 requestedGlobalPosition, long senderId) { /* ... */ }
	private void OnServerPathRequestReceived_Server(string tokenNodeName, Vector2 targetGlobalPosition, long senderId) { /* ... */ }
	private void OnServerDiceRollRequested_Server(string diceNotation, long senderId) { /* ... */ }
	private void OnNetworkDiceRollResult_ClientServer(long rollerId, string rollerName, string resultDataJson) { /* ... */ }
	private void OnNetworkCombatStateReceived_Client(string combatTrackerDataJson) { /* ... */ }
	public string GetCurrentMapPath() { return mapBackgroundSprite?.Texture?.ResourcePath; }
	private void OnNetworkMapLoadRequested_Client(string mapResourcePath) { /* ... */ }
	private void OnNetworkHandoutDisplayRequested_Client(string handoutImageResourcePath) { /* ... */ }
	private void OnNetworkCharacterSheetUpdated_Client(string tokenNodeName, string sheetDataJson) { /* ... */ }
	private void OnTestModifySheetButtonPressed_Server() { /* ... */ }
	private Token FindTokenByName(string nodeName) { if (string.IsNullOrEmpty(nodeName)) return null; return GetNodeOrNull<Token>(nodeName); }
	private void OnAssignOwnerButtonPressed_GM() { /* ... */ }
	private void OnServerSetTokenOwnerRequested_Server(string tokenNodeName, long newOwnerNetId, long requesterId) { /* ... */ }
	private void OnNetworkTokenOwnerUpdated_ClientServer(string tokenNodeName, long newOwnerNetId) { /* ... */ }
	private bool CanPlayerControlToken(long playerId, Token token) { /* ... */ return false; }
	private void OnAddSelectedTokenPressed() { /* ... */ }
	private void OnInitiativeDialogConfirmed() { /* ... */ }
	private void CleanUpInitiativeDialog() { /* ... */ }
	private void OnChangeTokenImageButtonPressed() { /* ... */ }
	private void OnTokenImageFileSelected(string path) { /* ... */ }
	private void OnLoadMapButtonPressed() { /* ... */ }
	public bool LoadMap(string path) { /* ... */ return true;}
	public void ClearExistingCampaignState() { /* ... */ }
	public Godot.Collections.Array<Token> GetTokens() { var tokens = new Godot.Collections.Array<Token>(); foreach (Node child in GetChildren()) if (child is Token token && IsInstanceValid(token)) tokens.Add(token); return tokens; }
	private void OnToggleDrawModeButtonPressed() { /* ... */ }
	private void OnClearDrawingsButtonPressed() { /* ... */ }
	private void OnToggleMeasureModeButtonPressed() { /* ... */ }
	private void UpdateMeasureModeButtonText() { /* ... */ }
	private void OnShareHandoutButtonPressed() { /* ... */ }
	private void OnHandoutImageFileSelected(string path) { /* ... */ }
	private void LoadNotes() { /* ... */ }
	private void OnToggleNotesButtonPressed() { /* ... */ }
	private void OnNotesTextChanged() { /* ... */ }
	private void OnToggleDecksPanelButtonPressed() { /* ... */ }
	private void OnToggleMusicButtonPressed() { /* ... */ }
	private void UpdateMusicButtonText() { /* ... */ }
	private void OnToggleMacroPanelButtonPressed() { /* ... */ }
	private void OnRunMacroButtonPressed() { /* ... */ }
	private void OnSaveCampaignButtonPressed() { /* ... */ }
	private void OnLoadCampaignButtonPressed() { /* ... */ }
	private void OnCampaignFileSelected(string path) { /* ... */ }
	private void OnHostButtonPressed() { /* ... */ }
	private void OnJoinButtonPressed() { /* ... */ }
	// OnDisconnectButtonPressed is modified above
	// Network Connection Status Handlers are modified above or preserved
	private void OnNetworkPeerConnected(long id) { /* ... */ }
	private void OnNetworkPeerDisconnected(long id) { /* ... */ }
	private void OnNetworkPlayerListUpdated() { /* ... */ }
	private void OnSpawnTestNetworkTokenButtonPressed() { /* ... */ }
	private void OnNetworkSpawnTokenRequested(string tokenNodeName, Vector2 globalPosition, string texturePath, string sheetDataJson, bool hasVision, float visionRangeGameUnits, Vector2 sizeVec) { /* ... */ }
	private void OnNetworkChatMessageReceived(long senderId, string senderName, string messageContent) { /* ... */ }
	private void OnNetworkAddDrawingLine_Client(string lineDataJson) { /* ... */ }
	private void OnNetworkReceiveFullDrawingState_Client(string allLinesDataJson) { /* ... */ }
}
