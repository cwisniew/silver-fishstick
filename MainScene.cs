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
    public DrawingOverlay DrawingOverlay => drawingOverlay;


	public override void _Ready()
	{
		if (chatLogNode == null) GD.PrintErr("ChatLog node not found.");
		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager");
		if (_soundManager == null) GD.PrintErr("SoundManager Autoload not found!");

		if (networkManagerNode == null) GD.PrintErr("NetworkManagerNode not found!");

		if (combatTracker == null) GD.PrintErr("CombatTracker node not found.");
		else combatTracker.Initialize(chatLogNode, _soundManager, networkManagerNode, GameUnitName);

		if (addTokenButton != null) addTokenButton.Pressed += OnAddSelectedTokenPressed;
		if (nextTurnButton != null) nextTurnButton.Pressed += () => combatTracker?.NextTurn();
		if (startCombatButton != null) startCombatButton.Pressed += () => combatTracker?.StartCombat();
		if (resetCombatButton != null) resetCombatButton.Pressed += () => combatTracker?.ResetCombat();
		if (changeTokenImageButton != null) changeTokenImageButton.Pressed += OnChangeTokenImageButtonPressed;
		if (tokenImageFileDialog != null) tokenImageFileDialog.FileSelected += OnTokenImageFileSelected;
		if (loadMapButton != null) loadMapButton.Pressed += OnLoadMapButtonPressed;
		if (mapFileDialog != null) mapFileDialog.FileSelected += OnMapFileSelected;
		if (toggleDrawModeButton != null) toggleDrawModeButton.Pressed += OnToggleDrawModeButtonPressed;
		if (clearDrawingsButton != null) clearDrawingsButton.Pressed += () => drawingOverlay?.ClearDrawings();
		if (toggleMeasureModeButton != null) toggleMeasureModeButton.Pressed += OnToggleMeasureModeButtonPressed;
		if (shareHandoutButton != null) shareHandoutButton.Pressed += OnShareHandoutButtonPressed;
		if (handoutImageFileDialog != null) handoutImageFileDialog.FileSelected += OnHandoutImageFileSelected;
		if (toggleNotesButton != null) toggleNotesButton.Pressed += OnToggleNotesButtonPressed;
		if (notesTextEdit != null) notesTextEdit.TextChanged += OnNotesTextChanged;
		if (decksPanel != null) decksPanel.Initialize(chatLogNode);
		if (toggleDecksPanelButton != null) toggleDecksPanelButton.Pressed += OnToggleDecksPanelButtonPressed;
		if (toggleMusicButton != null) toggleMusicButton.Pressed += OnToggleMusicButtonPressed;
		if (toggleMacroPanelButton != null) toggleMacroPanelButton.Pressed += OnToggleMacroPanelButtonPressed;
		if (runMacroButton != null) runMacroButton.Pressed += OnRunMacroButtonPressed;
		if (campaignFileDialog != null) campaignFileDialog.FileSelected += OnCampaignFileSelected;
		if (saveCampaignButton != null) saveCampaignButton.Pressed += OnSaveCampaignButtonPressed;
		if (loadCampaignButton != null) loadCampaignButton.Pressed += OnLoadCampaignButtonPressed;
		if (spawnTestNetworkTokenButton != null) spawnTestNetworkTokenButton.Pressed += OnSpawnTestNetworkTokenButtonPressed;

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
			networkManagerNode.NetworkHandoutDisplayRequested += OnNetworkHandoutDisplayRequested_Client; // Connect handout signal
		}
		if (hostButton != null) hostButton.Pressed += OnHostButtonPressed;
		if (joinButton != null) joinButton.Pressed += OnJoinButtonPressed;
		if (disconnectButton != null) disconnectButton.Pressed += OnDisconnectButtonPressed;

		if (chatMessageInput == null) GD.PrintErr("ChatMessageInput node not found.");
		if (serverIpInput == null) GD.PrintErr("ServerIpInput not found!");
		if (portInput == null) GD.PrintErr("PortInput not found!");
		if (networkStatusLabel == null) GD.PrintErr("NetworkStatusLabel not found!");
		if (playerListDisplay == null) GD.PrintErr("PlayerListDisplay not found!");
		if (mapBackgroundSprite == null) GD.PrintErr("MapBackgroundSprite not found.");
        if (drawingOverlay == null) GD.PrintErr("DrawingOverlay node not found.");
        if (notesPanel == null) GD.PrintErr("NotesPanel not found.");
        if (macroPanel == null) GD.PrintErr("MacroPanel not found.");

		chatLogNode?.AddMessage("System: Welcome to the VTT!", Colors.Aqua);
		chatLogNode?.AddMessage("System: Place custom images in 'assets/tokens/' and 'assets/maps/'.", Colors.CornflowerBlue);
		LoadNotes();

		if (_soundManager != null)
		{
			_soundManager.PlayMusic("ambient_music.ogg.txt");
			_isMusicPlaying = true;
			UpdateMusicButtonText();
		}

		_tokenScene = GD.Load<PackedScene>("res://Token.tscn");
		if (_tokenScene == null) GD.PrintErr("Failed to load Token.tscn");
	}

	private void OnChatMessageSubmitted(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) { if (chatMessageInput != null) chatMessageInput.Clear(); return; }
		string notation = "";
		if (text.Trim().StartsWith("/roll ") || text.Trim().StartsWith("/r ")) notation = text.Trim().Substring(text.IndexOf(" ", StringComparison.Ordinal) + 1).Trim();

		if (!string.IsNullOrEmpty(notation))
		{
			if (networkManagerNode != null && networkManagerNode.IsNetworkActive())
			{
				if (networkManagerNode.IsServer()) { long id = Multiplayer.GetUniqueId(); string name = networkManagerNode.GetPlayerList().FirstOrDefault(p=>p.Id==id)?.Name??"Host"; DiceRollResult res=DiceRoller.Roll(notation); DiceRollResultData data=DiceRollResultData.FromDiceRollResult(res); networkManagerNode.Rpc(nameof(NetworkManager.RpcClientDisplayDiceRollResult),id,name,Json.Stringify(data.ToDictionary()));}
				else networkManagerNode.RpcId(1, nameof(NetworkManager.RpcServerDoDiceRoll), notation);
			} else { DiceRollResult res=DiceRoller.Roll(notation); DiceRollResultData data=DiceRollResultData.FromDiceRollResult(res); OnNetworkDiceRollResult_ClientServer(Multiplayer.GetUniqueId(),"Player (Offline)",Json.Stringify(data.ToDictionary()));}
		} else {
			if (networkManagerNode != null && networkManagerNode.IsNetworkActive())
			{
				if (networkManagerNode.IsServer()) { long id=Multiplayer.GetUniqueId(); string name=networkManagerNode.GetPlayerList().FirstOrDefault(p=>p.Id==id)?.Name??"Host"; networkManagerNode.Rpc(nameof(NetworkManager.RpcClientReceiveChatMessage),id,name,text);}
				else networkManagerNode.RpcId(1, nameof(NetworkManager.RpcServerRelayChatMessage), text);
			} else chatLogNode?.AddMessage($"Player (Offline): {text}", Colors.LightGray);
		}
		if (chatMessageInput != null) chatMessageInput.Clear();
	}

	private void LogDiceResult(DiceRollResult result) { if (result.IsSuccess) { chatLogNode?.AddMessage($"Dice Roll ({result.Notation}): {result.Breakdown}", Colors.LightGoldenrod, true); _soundManager?.PlaySfx("dice_roll.wav.txt"); } else { chatLogNode?.AddMessage($"Dice Roll Error ({result.Notation}): {result.ErrorMessage}", Colors.OrangeRed, true);}}
	private void TestDiceRoller() { }
	public Token SpawnToken(Vector2 position) { if (_tokenScene == null) { GD.PrintErr("Token scene not loaded!"); return null; } Node n = _tokenScene.Instantiate(); if (n is Token t) { t.Position = position; t.Name = $"Token_{GD.Randi() % 10000}"; t.InitializeVision(pixelsPerUnit); t.InitializeMovementLimits(pixelsPerUnit, GameUnitsPerGridSquare); t.Sheet = new CharacterSheet { Name = $"Creature {GD.Randi() % 1000}" }; AddChild(t); t.InputEvent += (vp, ev, shapeIdx) => OnTokenInputEvent(vp, ev, shapeIdx, t); return t; } GD.PrintErr("Failed to instantiate Token."); n?.QueueFree(); return null; }
	public Token SpawnTokenAndApplyData(TokenData tokenData) { if (_tokenScene == null) { GD.PrintErr("Token scene not loaded!"); return null; } Node n = _tokenScene.Instantiate(); if (n is Token t) { t.Name = tokenData.NodeName ?? $"Token_{GD.Randi() % 10000}"; AddChild(t); t.InputEvent += (vp, ev, shapeIdx) => OnTokenInputEvent(vp, ev, shapeIdx, t); t.InitializeVision(pixelsPerUnit); t.InitializeMovementLimits(pixelsPerUnit, GameUnitsPerGridSquare); t.ApplyTokenData(tokenData); chatLogNode?.AddMessage($"Loaded/Spawned token '{t.Sheet?.Name ?? t.Name}' at {t.GlobalPosition}.", Colors.LightBlue); return t; } GD.PrintErr("Failed to instantiate Token from TokenData."); n?.QueueFree(); return null; }

	private void OnTokenInputEvent(Node viewport, InputEvent @event, int shapeIdx, Token tokenInstance)
	{
		if (tokenInstance == null) return;
		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseButtonEvent.Pressed)
			{
				_isTokenCurrentlyBeingDragged = true; _dragWasActuallyMovement = false;
				_locallyDraggedToken = tokenInstance; _locallyDraggedToken.StartDrag();
				if (_selectedToken != tokenInstance) { if (_selectedToken != null) _selectedToken.SetSelectionVisual(false); _selectedToken = tokenInstance; _selectedToken.SetSelectionVisual(true); chatLogNode?.AddMessage($"Selected: '{_selectedToken.Sheet?.Name ?? _selectedToken.Name}'.", Colors.Cyan); }
				GetViewport().SetInputAsHandled();
			} else {
				if (_isTokenCurrentlyBeingDragged && _locallyDraggedToken == tokenInstance)
				{
					Vector2 finalVisualPos = _locallyDraggedToken.GlobalPosition;
					if (networkManagerNode != null && networkManagerNode.IsNetworkActive())
					{
						if (networkManagerNode.IsServer()) { _locallyDraggedToken.UpdateDragPosition(finalVisualPos); bool isValid = _locallyDraggedToken.PerformCollisionCheckAndRevertIfFailed(); _locallyDraggedToken.EndDragCleanup(); networkManagerNode.Rpc(nameof(NetworkManager.RpcClientUpdateTokenPosition), _locallyDraggedToken.Name.ToString(), _locallyDraggedToken.GlobalPosition); if (!isValid) chatLogNode?.AddMessage($"Server: Token '{_locallyDraggedToken.Name}' move invalid, reverted.", Colors.Orange); }
						else { _locallyDraggedToken.GlobalPosition = _locallyDraggedToken.GetOriginalDragPosition(); _locallyDraggedToken.EndDragCleanup(); networkManagerNode.RpcId(1, nameof(NetworkManager.RpcServerRequestTokenDragMove), _locallyDraggedToken.Name.ToString(), finalVisualPos); }
					} else { _locallyDraggedToken.UpdateDragPosition(finalVisualPos); _locallyDraggedToken.PerformCollisionCheckAndRevertIfFailed(); _locallyDraggedToken.EndDragCleanup(); }
				}
				_isTokenCurrentlyBeingDragged = false; _dragWasActuallyMovement = false; _locallyDraggedToken = null; GetViewport().SetInputAsHandled();
			}
		}
	}

	public override void _Process(double delta) { if (_isTokenCurrentlyBeingDragged && _locallyDraggedToken != null) { Vector2 currentMousePos = GetGlobalMousePosition(); if (_locallyDraggedToken.GlobalPosition.DistanceSquaredTo(currentMousePos) > 16) _dragWasActuallyMovement = true; _locallyDraggedToken.UpdateDragPosition(currentMousePos); } }
	public override void _UnhandledInput(InputEvent @event)
	{
		if (GetViewport().GuiGetFocusOwner() != null) { if (_isMeasuring) { _isMeasuring = false; drawingOverlay?.ClearTemporaryMeasurement(); } if (_isTokenCurrentlyBeingDragged && _locallyDraggedToken != null) { _locallyDraggedToken.GlobalPosition = _locallyDraggedToken.GetOriginalDragPosition(); _locallyDraggedToken.EndDragCleanup(); _isTokenCurrentlyBeingDragged = false; _locallyDraggedToken = null; chatLogNode?.AddMessage("Drag cancelled due to UI focus.", Colors.Orange); } return; }
		if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right && rmb.Pressed)
		{
			if (_selectedToken != null)
			{
				Vector2 targetPos = GetGlobalMousePosition();
				if (networkManagerNode != null && networkManagerNode.IsNetworkActive())
				{
					if (networkManagerNode.IsServer()) { var obs = GetObstaclesForPathfinding(); Rect2 bounds = GetMapBoundsForPathfinding(); PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState; List<Vector2> path = Pathfinder.FindPath(_selectedToken.GlobalPosition, targetPos, obs, bounds, pixelsPerUnit, space); if (path != null && path.Count > 0) { _selectedToken.MoveAlongPath(path); var arr = new Godot.Collections.Array(path.Select(p=>(Variant)p).ToArray()); networkManagerNode.Rpc(nameof(NetworkManager.RpcClientExecuteTokenPath), _selectedToken.Name.ToString(), arr); chatLogNode?.AddMessage($"Server: Path for '{_selectedToken.Name}'. Broadcasting.", Colors.DarkGreen); } else chatLogNode?.AddMessage($"Server: No path for '{_selectedToken.Name}'.", Colors.Orange); }
					else networkManagerNode.RpcId(1, nameof(NetworkManager.RpcServerRequestTokenPathMove), _selectedToken.Name.ToString(), targetPos);
				} else { var obs = GetObstaclesForPathfinding(); Rect2 bounds = GetMapBoundsForPathfinding(); PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState; List<Vector2> path = Pathfinder.FindPath(_selectedToken.GlobalPosition, targetPos, obs, bounds, pixelsPerUnit, space); if (path != null && path.Count > 0) _selectedToken.MoveAlongPath(path); else chatLogNode?.AddMessage("Offline: No path found.", Colors.Orange); }
				GetViewport().SetInputAsHandled(); return;
			}
		}
		if (_isMeasureModeActive) { /* Measurement logic as before */ bool eh=false; if(@event is InputEventMouseButton mb && mb.ButtonIndex==MouseButton.Left){if(mb.Pressed){_isMeasuring=true;_measurementStartPoint=GetGlobalMousePosition();_measurementEndPoint=_measurementStartPoint;drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint,_measurementEndPoint,"0 "+GameUnitName);eh=true;}else if(_isMeasuring){_isMeasuring=false;_measurementEndPoint=GetGlobalMousePosition();float pd=_measurementStartPoint.DistanceTo(_measurementEndPoint);float gd=(pd/pixelsPerUnit)*GameUnitsPerGridSquare;string dt=$"{gd:F1} {GameUnitName}";drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint,_measurementEndPoint,dt);chatLogNode?.AddMessage($"Measured: {dt}",Colors.Cyan);eh=true;}}else if(@event is InputEventMouseMotion mm && _isMeasuring){_measurementEndPoint=GetGlobalMousePosition();float pd=_measurementStartPoint.DistanceTo(_measurementEndPoint);float gd=(pd/pixelsPerUnit)*GameUnitsPerGridSquare;string dt=$"{gd:F1} {GameUnitName}";drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint,_measurementEndPoint,dt);eh=true;}if(eh)GetViewport().SetInputAsHandled();}
	}
	private Godot.Collections.Array<StaticBody2D> GetObstaclesForPathfinding() { var o=new Godot.Collections.Array<StaticBody2D>(); Node w=GetNodeOrNull("Walls"); if(w!=null) foreach(Node c in w.GetChildren()) if(c is StaticBody2D sb) o.Add(sb); return o; }
	private Rect2 GetMapBoundsForPathfinding() { return mapBackgroundSprite!=null && mapBackgroundSprite.Texture!=null ? mapBackgroundSprite.GetGlobalRect() : GetViewportRect(); }

	private void OnNetworkTokenPositionUpdated_Client(string tokenNodeName, Vector2 newGlobalPosition) { Node n=GetNodeOrNull(tokenNodeName); if(n is Token t){t.GlobalPosition=newGlobalPosition; t.Velocity=Vector2.Zero; t.EndDragCleanup();} else GD.PrintErr($"Client: Token '{tokenNodeName}' not found for pos update.");}
	private void OnNetworkTokenPathExecutionRequested_Client(string tokenNodeName, Godot.Collections.Array pathPointsVariant) { Node n=GetNodeOrNull(tokenNodeName); if(n is Token t){List<Vector2> p=pathPointsVariant.Select(v=>v.AsVector2()).ToList(); if(p.Count > 0)t.MoveAlongPath(p); else chatLogNode?.AddMessage($"Token '{tokenNodeName}' received empty path.", Colors.Yellow);} else GD.PrintErr($"Client: Token '{tokenNodeName}' not found for path exec.");}
	private void OnServerDragRequestReceived_Server(string tokenNodeName, Vector2 requestedGlobalPosition, long senderId) { if(!networkManagerNode.IsServer())return; Node n=GetNodeOrNull(tokenNodeName); if(n is Token t){t.StartDrag();t.GlobalPosition=requestedGlobalPosition;bool valid=t.PerformCollisionCheckAndRevertIfFailed();t.EndDragCleanup(); networkManagerNode.Rpc(nameof(NetworkManager.RpcClientUpdateTokenPosition),tokenNodeName,t.GlobalPosition); if(!valid)chatLogNode?.AddMessage($"Server: Reverted invalid move for '{tokenNodeName}' (req by {senderId}).", Colors.Orange); else chatLogNode?.AddMessage($"Server: Processed drag for '{tokenNodeName}' (req by {senderId}) to {t.GlobalPosition}.", Colors.DarkTurquoise);} else GD.PrintErr($"Server: Token '{tokenNodeName}' not found for drag req from {senderId}.");}
	private void OnServerPathRequestReceived_Server(string tokenNodeName, Vector2 targetGlobalPosition, long senderId) { if(!networkManagerNode.IsServer())return; Node n=GetNodeOrNull(tokenNodeName); if(n is Token t){var obs=GetObstaclesForPathfinding(); Rect2 bounds=GetMapBoundsForPathfinding(); PhysicsDirectSpaceState2D space=GetWorld2D().DirectSpaceState; List<Vector2> p=Pathfinder.FindPath(t.GlobalPosition,targetGlobalPosition,obs,bounds,pixelsPerUnit,space); if(p!=null&&p.Count>0){t.MoveAlongPath(p);var arr=new Godot.Collections.Array(p.Select(v=>(Variant)v).ToArray()); networkManagerNode.Rpc(nameof(NetworkManager.RpcClientExecuteTokenPath),tokenNodeName,arr); chatLogNode?.AddMessage($"Server: Path for '{tokenNodeName}' (req by {senderId}). Broadcasting.", Colors.DarkGreen);} else chatLogNode?.AddMessage($"Server: No path for '{tokenNodeName}' (req by {senderId}).", Colors.Orange);} else GD.PrintErr($"Server: Token '{tokenNodeName}' not found for path req from {senderId}.");}
	private void OnServerDiceRollRequested_Server(string diceNotation, long senderId) { if(!networkManagerNode.IsServer())return; DiceRollResult res=DiceRoller.Roll(diceNotation); DiceRollResultData data=DiceRollResultData.FromDiceRollResult(res); string json=Json.Stringify(data.ToDictionary()); string name=networkManagerNode.GetPlayerList().FirstOrDefault(p=>p.Id==senderId)?.Name ?? $"Player {senderId}"; networkManagerNode.Rpc(nameof(NetworkManager.RpcClientDisplayDiceRollResult),senderId,name,json);}
	private void OnNetworkDiceRollResult_ClientServer(long rollerId, string rollerName, string resultDataJson) { if(chatLogNode==null)return; DiceRollResultData data=DiceRollResultData.FromDictionary(Json.ParseString(resultDataJson).AsGodotDictionary()); string msg; Color color; if(data.IsSuccess){msg=$"{rollerName} (ID:{rollerId}) rolls ({data.Notation}): {data.Breakdown}";color=Colors.Goldenrod;_soundManager?.PlaySfx("dice_roll.wav.txt");}else{msg=$"{rollerName} (ID:{rollerId}) roll error ({data.Notation}): {data.ErrorMessage}";color=Colors.OrangeRed;} chatLogNode.AddMessage(msg,color,isCombatLog:true);}
	private void OnNetworkCombatStateReceived_Client(string combatTrackerDataJson) { if(networkManagerNode.IsServer())return; if(combatTracker==null){GD.PrintErr("Client: CombatTracker null.");return;} var parsedJson=Json.ParseString(combatTrackerDataJson); if(parsedJson.VariantType==Variant.Type.Nil){GD.PrintErr("Client: Failed to parse CombatTrackerData JSON.");chatLogNode?.AddMessage("Error: Invalid combat state from server.",Colors.Red);return;} CombatTrackerData data=CombatTrackerData.FromDictionary(parsedJson.AsGodotDictionary()); if(data!=null)combatTracker.ApplyCombatTrackerData(data,GetTokens()); else{GD.PrintErr("Client: Parsed CombatTrackerData is null.");chatLogNode?.AddMessage("Error: Could not interpret combat state from server.",Colors.Red);}}
	public string GetCurrentMapPath() { return mapBackgroundSprite?.Texture?.ResourcePath; }
	private void OnNetworkMapLoadRequested_Client(string mapResourcePath) { GD.Print($"Client: Received request to load map: {mapResourcePath}"); if(string.IsNullOrEmpty(mapResourcePath)) chatLogNode?.AddMessage("Server cleared map.",Colors.MediumPurple); else chatLogNode?.AddMessage($"Loading map from server: {mapResourcePath.GetFile()}",Colors.MediumPurple); LoadMap(mapResourcePath); }
	private void OnNetworkHandoutDisplayRequested_Client(string handoutImageResourcePath)
	{
		GD.Print($"Client: Received request to display handout: {handoutImageResourcePath}");
		if (handoutDisplayScene == null) { GD.PrintErr("HandoutDisplay scene not set on client!"); return; }
		var imageTexture = ResourceLoader.Load<Texture2D>(handoutImageResourcePath);
		if (imageTexture == null) { chatLogNode?.AddMessage($"Error: Could not load handout image '{handoutImageResourcePath.GetFile()}' from server.", Colors.Red); return; }
		var handoutInstance = handoutDisplayScene.Instantiate<HandoutDisplay>();
		if (handoutInstance == null) { GD.PrintErr("Failed to instance HandoutDisplay on client."); return; }
		AddChild(handoutInstance);
		handoutInstance.DisplayHandout(imageTexture);
		chatLogNode?.AddMessage($"Received handout '{handoutImageResourcePath.GetFile()}' from GM.", Colors.Plum);
	}

	private void OnAddSelectedTokenPressed() { if (_selectedToken == null) { chatLogNode?.AddMessage("Error: No token selected.", Colors.OrangeRed); return; } string name = _selectedToken.Sheet?.Name ?? _selectedToken.Name ?? "Unnamed"; if (_initiativeDialog != null) _initiativeDialog.QueueFree(); _initiativeDialog = new AcceptDialog { Title = "Enter Initiative" }; VBoxContainer v = new VBoxContainer(); v.AddChild(new Label { Text = $"Initiative for {name}:" }); _initiativeLineEdit = new LineEdit { PlaceholderText = "15" }; v.AddChild(_initiativeLineEdit); _initiativeDialog.AddChild(v); _initiativeDialog.Confirmed += OnInitiativeDialogConfirmed; _initiativeDialog.Canceled += () => { if (_initiativeDialog != null) _initiativeDialog.QueueFree(); _initiativeDialog = null; }; _initiativeDialog.CloseRequested += () => { if (_initiativeDialog != null) _initiativeDialog.QueueFree(); _initiativeDialog = null; }; AddChild(_initiativeDialog); _initiativeDialog.PopupCentered(); _initiativeLineEdit.GrabFocus(); }
	private void OnInitiativeDialogConfirmed() { if (_selectedToken == null || _initiativeLineEdit == null || combatTracker == null) { GD.PrintErr("Dialog confirm error."); CleanUpInitiativeDialog(); return; } if (int.TryParse(_initiativeLineEdit.Text, out int init)) { combatTracker.AddCombatantEntry(new Combatant(_selectedToken.Sheet?.Name ?? _selectedToken.Name ?? "Unnamed", init, _selectedToken)); } else chatLogNode?.AddMessage($"Error: Invalid initiative '{_initiativeLineEdit.Text}'.", Colors.OrangeRed); CleanUpInitiativeDialog(); }
	private void CleanUpInitiativeDialog() { if (_initiativeDialog != null) { _initiativeDialog.QueueFree(); _initiativeDialog = null; } _initiativeLineEdit = null; }
	private void OnChangeTokenImageButtonPressed() { if (_selectedToken == null) { chatLogNode?.AddMessage("Error: Select token first.", Colors.OrangeRed); return; } if (tokenImageFileDialog == null) { GD.PrintErr("TokenImageFileDialog null."); return; } if (_selectedToken.TokenTexture != null && !string.IsNullOrEmpty(_selectedToken.TokenTexture.ResourcePath)) { string dir = _selectedToken.TokenTexture.ResourcePath.GetBaseDir(); tokenImageFileDialog.CurrentPath = (DirAccess.DirExistsAbsolute(dir) || ResourceLoader.Exists(dir)) ? dir : "res://assets/tokens/"; } else tokenImageFileDialog.CurrentPath = "res://assets/tokens/"; tokenImageFileDialog.PopupCentered(); }
	private void OnTokenImageFileSelected(string path) { if (_selectedToken == null) { chatLogNode?.AddMessage("Error: No token selected.", Colors.OrangeRed); return; } var tex = ResourceLoader.Load<Texture2D>(path); if (tex == null) { chatLogNode?.AddMessage($"Error: Failed to load image '{path}'.", Colors.OrangeRed); return; } _selectedToken.TokenTexture = tex; string tokenName = _selectedToken.Sheet?.DisplayText ?? _selectedToken.Name ?? "Unnamed"; chatLogNode?.AddMessage($"Token '{tokenName}' image changed to {path.GetFile()}.", Colors.LawnGreen);
		// If server, broadcast this change
		if (networkManagerNode != null && networkManagerNode.IsServer())
		{
			// This would require a new RPC like RpcClientUpdateTokenTexture(string tokenName, string texturePath)
			// For now, this change is local unless saved in campaign.
			// networkManagerNode.Rpc(nameof(NetworkManager.RpcClientUpdateTokenTexture), _selectedToken.Name.ToString(), path);
			chatLogNode?.AddMessage($"Note: Token texture change for '{tokenName}' is currently local. Save campaign to persist for all.", Colors.LightYellow);
		}
	}
	private void OnLoadMapButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (mapFileDialog == null) { GD.PrintErr("MapFileDialog null."); return; } mapFileDialog.CurrentPath = "res://assets/maps/"; mapFileDialog.PopupCentered(); }
	public bool LoadMap(string path) { if (mapBackgroundSprite == null) { GD.PrintErr("MapBackgroundSprite null."); return false; } if (string.IsNullOrEmpty(path)) { mapBackgroundSprite.Texture = null; chatLogNode?.AddMessage("Map cleared.", Colors.MediumPurple); return true; } if (!ResourceLoader.Exists(path)) { chatLogNode?.AddMessage($"Error: Map not found at '{path}'.", Colors.OrangeRed); return false; } var tex = ResourceLoader.Load<Texture2D>(path); if (tex == null) { chatLogNode?.AddMessage($"Error: Failed to load map '{path}'.", Colors.OrangeRed); return false; } mapBackgroundSprite.Texture = tex; chatLogNode?.AddMessage($"Map changed to {path.GetFile()}.", Colors.MediumPurple); return true; }
	public void ClearExistingCampaignState() { foreach (Token token in GetTokens()) token.QueueFree(); _selectedToken = null; drawingOverlay?.ClearDrawings(); drawingOverlay?.ClearTemporaryMeasurement(); combatTracker?.ResetCombat(); if (mapBackgroundSprite != null) mapBackgroundSprite.Texture = null; GD.Print("Campaign state cleared."); chatLogNode?.AddMessage("Campaign state cleared.", Colors.Gray); }
	public Godot.Collections.Array<Token> GetTokens() { var tokens = new Godot.Collections.Array<Token>(); foreach (Node child in GetChildren()) if (child is Token token && IsInstanceValid(token)) tokens.Add(token); return tokens; } // Added IsInstanceValid
	private void OnToggleDrawModeButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (drawingOverlay == null) return; drawingOverlay.IsDrawingEnabled = !drawingOverlay.IsDrawingEnabled; if (toggleDrawModeButton != null) toggleDrawModeButton.Text = drawingOverlay.IsDrawingEnabled ? "Draw: ON" : "Draw: OFF"; chatLogNode?.AddMessage($"Drawing: {(drawingOverlay.IsDrawingEnabled ? "ON" : "OFF")}.", drawingOverlay.IsDrawingEnabled ? Colors.LightSeaGreen : Colors.Orange); if (drawingOverlay.IsDrawingEnabled && _isMeasureModeActive) { _isMeasureModeActive = false; UpdateMeasureModeButtonText(); drawingOverlay?.ClearTemporaryMeasurement(); chatLogNode?.AddMessage("Measure mode OFF (drawing ON).", Colors.Orange); } }
	private void OnToggleMeasureModeButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); _isMeasureModeActive = !_isMeasureModeActive; UpdateMeasureModeButtonText(); if (_isMeasureModeActive) { chatLogNode?.AddMessage("Measure mode: ON.", Colors.LightSeaGreen); if (drawingOverlay != null && drawingOverlay.IsDrawingEnabled) { drawingOverlay.IsDrawingEnabled = false; if (toggleDrawModeButton != null) toggleDrawModeButton.Text = "Draw: OFF"; chatLogNode?.AddMessage("Draw mode OFF (measure ON).", Colors.Orange); } } else { chatLogNode?.AddMessage("Measure mode: OFF.", Colors.Orange); _isMeasuring = false; drawingOverlay?.ClearTemporaryMeasurement(); } }
	private void UpdateMeasureModeButtonText() { if (toggleMeasureModeButton != null) toggleMeasureModeButton.Text = _isMeasureModeActive ? "Measure: ON" : "Measure: OFF"; }
	private void OnShareHandoutButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (handoutDisplayScene == null) { chatLogNode?.AddMessage("Error: HandoutDisplay scene not set.", Colors.Red); return; } if (handoutImageFileDialog == null) return; handoutImageFileDialog.CurrentPath = "res://assets/handouts/"; handoutImageFileDialog.PopupCentered(); }
	private void OnHandoutImageFileSelected(string path) { if (handoutDisplayScene == null) return; var tex = ResourceLoader.Load<Texture2D>(path); if (tex == null) { chatLogNode?.AddMessage($"Error: Failed to load handout '{path}'.", Colors.OrangeRed); return; } Node hi = handoutDisplayScene.Instantiate(); if (hi is HandoutDisplay hdi) { AddChild(hdi); hdi.DisplayHandout(tex); chatLogNode?.AddMessage($"GM shared handout: {path.GetFile()}", Colors.MediumPurple); if (networkManagerNode != null && networkManagerNode.IsServer()) { networkManagerNode.Rpc(nameof(NetworkManager.RpcClientShowHandout), path); } } else { GD.PrintErr("Failed to instance HandoutDisplay."); hi?.QueueFree(); } }
	private void LoadNotes() { if (notesTextEdit == null) return; ConfigFile cfg = new ConfigFile(); Error err = cfg.Load(NotesFilePath); if (err == Error.Ok) notesTextEdit.Text = cfg.GetValue("Notes", "Content", "").ToString(); else if (err != Error.FileNotFound) { GD.PrintErr($"Error loading notes: {err}"); chatLogNode?.AddMessage($"Error loading notes: {err}", Colors.Red); } }
	private void OnToggleNotesButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (notesPanel == null) return; notesPanel.Visible = !notesPanel.Visible; if (notesPanel.Visible) { notesTextEdit?.GrabFocus(); chatLogNode?.AddMessage("Notes ON.", Colors.DarkGray); } else chatLogNode?.AddMessage("Notes OFF.", Colors.DarkGray); }
	private void OnNotesTextChanged() { if (notesTextEdit == null) return; ConfigFile cfg = new ConfigFile(); cfg.SetValue("Notes", "Content", notesTextEdit.Text); Error err = cfg.Save(NotesFilePath); if (err != Error.Ok) { GD.PrintErr($"Error saving notes: {err}"); chatLogNode?.AddMessage($"Error saving notes: {err}", Colors.Red); } }
	private void OnToggleDecksPanelButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (decksPanel == null) return; decksPanel.Visible = !decksPanel.Visible; chatLogNode?.AddMessage($"Decks panel {(decksPanel.Visible ? "ON" : "OFF")}.", Colors.DarkSlateBlue); }
	private void OnToggleMusicButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (_soundManager == null) return; _isMusicPlaying = !_isMusicPlaying; if (_isMusicPlaying) _soundManager.PlayMusic("ambient_music.ogg.txt"); else _soundManager.StopMusic(); UpdateMusicButtonText(); }
	private void UpdateMusicButtonText() { if (toggleMusicButton != null) toggleMusicButton.Text = _isMusicPlaying ? "Music: Stop" : "Music: Play"; }
	private void OnToggleMacroPanelButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (macroPanel == null) return; macroPanel.Visible = !macroPanel.Visible; if (macroPanel.Visible) { macroInputTextEdit?.GrabFocus(); chatLogNode?.AddMessage("Macro panel ON.", Colors.DarkGoldenrod); } else chatLogNode?.AddMessage("Macro panel OFF.", Colors.DarkGoldenrod); }
	private void OnRunMacroButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (macroInputTextEdit == null || chatLogNode == null) { GD.PrintErr("Macro input/ChatLog missing."); return; } string scr = macroInputTextEdit.Text; if (string.IsNullOrWhiteSpace(scr)) { chatLogNode.AddMessage("[MACRO] Empty script.", Colors.OrangeRed); return; } MacroContext ctx = new MacroContext { Chat = chatLogNode, SelectedToken = _selectedToken, Combat = combatTracker, MainSceneInstance = this }; chatLogNode.AddMessage("[MACRO] Executing...", Colors.DarkGoldenrod); MacroEngine.ExecuteMacro(scr, ctx); chatLogNode.AddMessage("[MACRO] Finished.", Colors.DarkGoldenrod); }
	private void OnSaveCampaignButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (campaignFileDialog == null) return; campaignFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile; campaignFileDialog.ClearFilters(); campaignFileDialog.AddFilter($"*{CampaignFileExtension} ; VTT Campaign"); campaignFileDialog.CurrentPath = $"user://campaign_save{CampaignFileExtension}"; campaignFileDialog.PopupCentered(); }
	private void OnLoadCampaignButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (campaignFileDialog == null) return; campaignFileDialog.FileMode = FileDialog.FileModeEnum.OpenFile; campaignFileDialog.ClearFilters(); campaignFileDialog.AddFilter($"*{CampaignFileExtension} ; VTT Campaign"); campaignFileDialog.CurrentPath = "user://"; campaignFileDialog.PopupCentered(); }
	private void OnCampaignFileSelected(string path) { if (campaignFileDialog == null) return; if (campaignFileDialog.FileMode == FileDialog.FileModeEnum.SaveFile) { if (!path.EndsWith(CampaignFileExtension)) path += CampaignFileExtension; CampaignManager.SaveCampaign(path, this); } else CampaignManager.LoadCampaign(path, this); }
	private void OnHostButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (portInput == null || !int.TryParse(portInput.Text, out int port) || port <= 0 || port > 65535) { networkStatusLabel.Text = "Status: Invalid port."; chatLogNode?.AddMessage("Invalid port for hosting.", Colors.Red); return; } networkManagerNode?.HostGame(port); }
	private void OnJoinButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (serverIpInput == null || portInput == null || networkStatusLabel == null) return; string ip = serverIpInput.Text.Trim(); if (string.IsNullOrEmpty(ip)) { networkStatusLabel.Text = "Status: IP empty."; chatLogNode?.AddMessage("IP for joining empty.", Colors.Red); return; } if (!int.TryParse(portInput.Text, out int port) || port <= 0 || port > 65535) { networkStatusLabel.Text = "Status: Invalid port."; chatLogNode?.AddMessage("Invalid port for joining.", Colors.Red); return; } networkStatusLabel.Text = $"Status: Joining {ip}:{port}..."; networkManagerNode?.JoinGame(ip, port); }
	private void OnDisconnectButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); networkManagerNode?.DisconnectNetwork(); networkStatusLabel.Text = "Status: Disconnected."; chatLogNode?.AddMessage("Disconnected.", Colors.Orange); hostButton.Disabled = false; joinButton.Disabled = false; disconnectButton.Disabled = true; }
	private void OnNetworkServerCreated() { networkStatusLabel.Text = $"Status: HOSTING on port {portInput.Text}."; chatLogNode?.AddMessage($"Server created on port {portInput.Text}.", Colors.Green); hostButton.Disabled = true; joinButton.Disabled = true; disconnectButton.Disabled = false; }
	private void OnNetworkServerCreationFailed(string reason) { networkStatusLabel.Text = $"Status: Server creation FAILED: {reason}"; chatLogNode?.AddMessage($"Server creation failed: {reason}", Colors.Red); hostButton.Disabled = false; joinButton.Disabled = false; disconnectButton.Disabled = true; }
	private void OnNetworkConnectionSucceeded() { networkStatusLabel.Text = "Status: Connected to server!"; chatLogNode?.AddMessage("Connected to server.", Colors.Green); hostButton.Disabled = true; joinButton.Disabled = true; disconnectButton.Disabled = false; }
	private void OnNetworkConnectionFailed() { networkStatusLabel.Text = "Status: Connection FAILED."; chatLogNode?.AddMessage("Failed to connect.", Colors.Red); hostButton.Disabled = false; joinButton.Disabled = false; disconnectButton.Disabled = true; }
	private void OnNetworkPeerConnected(long id) { networkStatusLabel.Text = $"Status: Player {id} connected."; chatLogNode?.AddMessage($"Player {id} connected.", Colors.LawnGreen); }
	private void OnNetworkPeerDisconnected(long id) { networkStatusLabel.Text = $"Status: Player {id} disconnected."; chatLogNode?.AddMessage($"Player {id} disconnected.", Colors.Orange); }
	private void OnNetworkServerDisconnected() { networkStatusLabel.Text = "Status: Disconnected from server."; chatLogNode?.AddMessage("Disconnected from server.", Colors.OrangeRed); hostButton.Disabled = false; joinButton.Disabled = false; disconnectButton.Disabled = true; }
	private void OnNetworkPlayerListUpdated() { if (playerListDisplay == null || networkManagerNode == null) return; playerListDisplay.Clear(); var players = networkManagerNode.GetPlayerList(); if (players.Count == 0) playerListDisplay.AddItem("No players connected."); else { for(int i = 0; i < players.Count; i++) { if (players[i].AsGodotObject() is NetworkPlayer player) playerListDisplay.AddItem($"ID: {player.Id} - Name: {player.Name}"); else GD.Print($"Error: Player data at index {i} not NetworkPlayer."); } } chatLogNode?.AddMessage($"Player list updated. Count: {players.Count}", Colors.LightSteelBlue); }
	private void OnSpawnTestNetworkTokenButtonPressed() { _soundManager?.PlaySfx("ui_click.wav.txt"); if (networkManagerNode == null || !networkManagerNode.IsServer()) { chatLogNode?.AddMessage("Only host can spawn test net tokens.", Colors.OrangeRed); return; } string uniqueName = $"NetToken_{GD.Randi() % 10000}"; Vector2 spawnPos = new Vector2( (float)GD.RandRange(150, 450), (float)GD.RandRange(150, 350) ); CharacterSheetData sheetData = new CharacterSheetData { Name = $"NetCreature {GD.Randi() % 100}", MaxHealthPoints = 20, CurrentHealthPoints = 20, ArmorClass = 12, Speed = 30 }; sheetData.CustomProperties.Add("SpawnedBy", "NetworkTestButton"); TokenData tokenDataForNetwork = new TokenData { NodeName = uniqueName, Position = new Vector2Data(spawnPos), TexturePath = "res://icon.svg", SheetData = sheetData, HasVision = true, VisionRangeGameUnits = 6.0f, Size = new Vector2Data(new Vector2(128,128)) }; Token spawnedToken = SpawnTokenAndApplyData(tokenDataForNetwork); if (spawnedToken == null) { GD.PrintErr("Failed to spawn test token locally on server."); chatLogNode?.AddMessage("Error: Failed to spawn test token locally.", Colors.Red); return; } string actualSheetJson = Json.Stringify(spawnedToken.Sheet.ToDictionary()); string actualTexturePath = spawnedToken.TokenTexture?.ResourcePath ?? ""; networkManagerNode.Rpc(nameof(NetworkManager.RpcClientDoSpawnToken), spawnedToken.Name.ToString(), spawnedToken.GlobalPosition, actualTexturePath, actualSheetJson, spawnedToken.HasVision, spawnedToken.VisionRangeGameUnits, spawnedToken.Size); chatLogNode?.AddMessage($"Host spawned test token: {spawnedToken.Name}", Colors.LightGreen); }
	private void OnNetworkSpawnTokenRequested(string tokenNodeName, Vector2 globalPosition, string texturePath, string sheetDataJson, bool hasVision, float visionRangeGameUnits, Vector2 sizeVec) { if (GetNodeOrNull(tokenNodeName) != null) { GD.Print($"Client: Token {tokenNodeName} already exists."); return; } GD.Print($"Client: Received request to spawn token: {tokenNodeName}"); CharacterSheetData sheetData = new CharacterSheetData(); if (!string.IsNullOrEmpty(sheetDataJson)) { var parseResult = Json.ParseString(sheetDataJson); if (parseResult.VariantType == Variant.Type.Dictionary) sheetData = CharacterSheetData.FromDictionary(parseResult.AsGodotDictionary()); else GD.PrintErr($"Client: Failed to parse sheetDataJson for {tokenNodeName}."); } TokenData tokenData = new TokenData { NodeName = tokenNodeName, Position = new Vector2Data(globalPosition), TexturePath = texturePath, SheetData = sheetData, HasVision = hasVision, VisionRangeGameUnits = visionRangeGameUnits, Size = new Vector2Data(sizeVec) }; Token spawnedToken = SpawnTokenAndApplyData(tokenData); if (spawnedToken != null) chatLogNode?.AddMessage($"Remote token spawned: {spawnedToken.Name}", Colors.LightSkyBlue); else chatLogNode?.AddMessage($"Error: Failed to spawn remote token {tokenNodeName}", Colors.Red); }
	private void OnNetworkChatMessageReceived(long senderId, string senderName, string messageContent) { if (chatLogNode == null) return; string formattedMessage = $"{senderName} (ID:{senderId}): {messageContent}"; Color messageColor = (senderId == Multiplayer.GetUniqueId()) ? Colors.LightYellow : Colors.WhiteSmoke; if (senderId == 1 && Multiplayer.GetUniqueId() != 1) messageColor = Colors.Aqua; chatLogNode.AddMessage(formattedMessage, messageColor); if ((messageContent.StartsWith("Dice Roll (") || messageContent.StartsWith("Dice Roll Error (")) && senderId != Multiplayer.GetUniqueId()) { if (messageContent.StartsWith("Dice Roll (")) _soundManager?.PlaySfx("dice_roll.wav.txt"); } }
}
