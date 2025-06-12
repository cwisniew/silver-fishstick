using Godot;
using System;

public partial class MainScene : Node2D
{
	[Export] private ChatLog chatLogNode;
	[Export] private LineEdit chatMessageInput;
	[Export] private CombatTracker combatTracker; // Panel itself is the tracker
	[Export] private Button addTokenButton;
	[Export] private Button nextTurnButton;
	[Export] private Button startCombatButton;
	[Export] private Button resetCombatButton;
	[Export] private Button changeTokenImageButton;
	[Export] private FileDialog tokenImageFileDialog;
	[Export] private Sprite2D mapBackgroundSprite;
	[Export] private Button loadMapButton;
	[Export] private FileDialog mapFileDialog;
	[Export] private DrawingOverlay drawingOverlay; // Changed type to DrawingOverlay
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

	private Token _selectedToken = null;
	private PackedScene _tokenScene;
	private bool _isMusicPlaying = false;
	private SoundManager _soundManager; // Instance for easy access
	private const string NotesFilePath = "user://user_notes.cfg";
	private bool _isMeasureModeActive = false;
	private Vector2 _measurementStartPoint = Vector2.Zero;
	private Vector2 _measurementEndPoint = Vector2.Zero;
	private bool _isMeasuring = false; // True when mouse button is down during measurement

	// Conversion factors
	[Export] private float pixelsPerUnit = 50.0f; // Renamed from PixelsPerGridUnit, used for measurement and vision
	private const float GameUnitsPerGridSquare = 5.0f; // e.g., 5 feet per grid square, used for measurement display
	private const string GameUnitName = "ft"; // Used for measurement display

	private AcceptDialog _initiativeDialog;
	private LineEdit _initiativeLineEdit;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// NodePath assignments from .tscn
		if (chatLogNode == null) GD.PrintErr("ChatLog node not found.");
		if (chatMessageInput == null) GD.PrintErr("ChatMessageInput node not found.");

		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager"); // Ensure SoundManager is fetched before initializing others
		if (_soundManager == null) GD.PrintErr("SoundManager Autoload not found!");

		if (combatTracker == null) GD.PrintErr("CombatTracker node not found.");
		else combatTracker.Initialize(chatLogNode, _soundManager, GameUnitName); // Pass ChatLog, SoundManager & GameUnitName

		if (addTokenButton == null) GD.PrintErr("AddTokenButton not found.");
		else addTokenButton.Pressed += OnAddSelectedTokenPressed;

		if (nextTurnButton == null) GD.PrintErr("NextTurnButton not found.");
		else nextTurnButton.Pressed += () => combatTracker?.NextTurn();

		if (startCombatButton == null) GD.PrintErr("StartCombatButton not found.");
		else startCombatButton.Pressed += () => combatTracker?.StartCombat();

		if (resetCombatButton == null) GD.PrintErr("ResetCombatButton not found.");
		else resetCombatButton.Pressed += () => combatTracker?.ResetCombat();

		if (changeTokenImageButton == null) GD.PrintErr("ChangeTokenImageButton not found.");
		else changeTokenImageButton.Pressed += OnChangeTokenImageButtonPressed;

		if (tokenImageFileDialog == null) GD.PrintErr("TokenImageFileDialog not found.");
		else tokenImageFileDialog.FileSelected += OnTokenImageFileSelected;

		if (mapBackgroundSprite == null) GD.PrintErr("MapBackgroundSprite not found.");
		if (loadMapButton == null) GD.PrintErr("LoadMapButton not found.");
		else loadMapButton.Pressed += OnLoadMapButtonPressed;

		if (mapFileDialog == null) GD.PrintErr("MapFileDialog not found.");
		else mapFileDialog.FileSelected += OnMapFileSelected;

		if (drawingOverlay == null)
		{
			GD.PrintErr("DrawingOverlay node not found.");
		}
		// else drawingOverlay.Initialize(); // If it had an Initialize method

		if (toggleDrawModeButton == null) GD.PrintErr("ToggleDrawModeButton not found.");
		else toggleDrawModeButton.Pressed += OnToggleDrawModeButtonPressed;

		if (clearDrawingsButton == null) GD.PrintErr("ClearDrawingsButton not found.");
		else clearDrawingsButton.Pressed += () => drawingOverlay?.ClearDrawings();

		if (toggleMeasureModeButton == null) GD.PrintErr("ToggleMeasureModeButton not found.");
		else toggleMeasureModeButton.Pressed += OnToggleMeasureModeButtonPressed;

		if (shareHandoutButton == null) GD.PrintErr("ShareHandoutButton not found.");
		else shareHandoutButton.Pressed += OnShareHandoutButtonPressed;

		if (handoutImageFileDialog == null) GD.PrintErr("HandoutImageFileDialog not found.");
		else handoutImageFileDialog.FileSelected += OnHandoutImageFileSelected;

		if (notesPanel == null) GD.PrintErr("NotesPanel not found.");
		if (toggleNotesButton == null) GD.PrintErr("ToggleNotesButton not found.");
		else toggleNotesButton.Pressed += OnToggleNotesButtonPressed;

		if (notesTextEdit == null) GD.PrintErr("NotesTextEdit not found.");
		else notesTextEdit.TextChanged += OnNotesTextChanged;

		if (decksPanel == null) GD.PrintErr("DecksPanel not found.");
		else decksPanel.Initialize(chatLogNode);

		if (toggleDecksPanelButton == null) GD.PrintErr("ToggleDecksPanelButton not found.");
		else toggleDecksPanelButton.Pressed += OnToggleDecksPanelButtonPressed;

		if (toggleMusicButton == null) GD.PrintErr("ToggleMusicButton not found.");
		else toggleMusicButton.Pressed += OnToggleMusicButtonPressed;

		if (macroPanel == null) GD.PrintErr("MacroPanel not found.");
		if (toggleMacroPanelButton == null) GD.PrintErr("ToggleMacroPanelButton not found.");
		else toggleMacroPanelButton.Pressed += OnToggleMacroPanelButtonPressed;

		if (macroInputTextEdit == null) GD.PrintErr("MacroInputTextEdit not found.");
		if (runMacroButton == null) GD.PrintErr("RunMacroButton not found.");
		else runMacroButton.Pressed += OnRunMacroButtonPressed;

		if (campaignFileDialog == null) GD.PrintErr("CampaignFileDialog not found!");
		else campaignFileDialog.FileSelected += OnCampaignFileSelected;

		if (saveCampaignButton == null) GD.PrintErr("SaveCampaignButton not found!");
		else saveCampaignButton.Pressed += OnSaveCampaignButtonPressed;

		if (loadCampaignButton == null) GD.PrintErr("LoadCampaignButton not found!");
		else loadCampaignButton.Pressed += OnLoadCampaignButtonPressed;

		if (chatMessageInput != null)
		{
			chatMessageInput.TextSubmitted += OnChatMessageSubmitted;
		}

		chatLogNode?.AddMessage("System: Welcome to the VTT!", Colors.Aqua);
		chatLogNode?.AddMessage("System: Place custom images in 'assets/tokens/' and 'assets/maps/'.", Colors.CornflowerBlue);
		LoadNotes();

		// _soundManager already fetched above
		if (_soundManager != null)
		{
			_soundManager.PlayMusic("ambient_music.ogg.txt");
			_isMusicPlaying = true;
			UpdateMusicButtonText();
		}

		_tokenScene = GD.Load<PackedScene>("res://Token.tscn");
		if (_tokenScene == null)
		{
			GD.PrintErr("Failed to load Token.tscn");
			return;
		}

		SpawnToken(new Vector2(100, 100)); // These will also log to chat now via TestDiceRoller modifications
		SpawnToken(new Vector2(300, 100));
		SpawnToken(new Vector2(500, 100));

		TestDiceRoller(); // This will now log to the chatLogNode
	}

	private void OnChatMessageSubmitted(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		// Simple command handling for dice rolls
		if (text.Trim().StartsWith("/roll ") || text.Trim().StartsWith("/r "))
		{
			string notation = text.Trim().Substring(text.IndexOf(" ", StringComparison.Ordinal) + 1);
			DiceRollResult result = DiceRoller.Roll(notation);
			LogDiceResult(result);
		}
		else
		{
			chatLogNode?.AddMessage($"Player: {text}", Colors.LightGray);
		}

		if (chatMessageInput != null)
		{
			chatMessageInput.Clear();
		}
	}

	private void LogDiceResult(DiceRollResult result)
	{
		if (result.IsSuccess)
		{
			string rollMessage = $"Dice Roll ({result.Notation}): {result.Breakdown}";
			chatLogNode?.AddMessage(rollMessage, Colors.LightGoldenrod, isCombatLog: true);
			_soundManager?.PlaySfx("dice_roll.wav.txt");
		}
		else
		{
			string errorMessage = $"Dice Roll Error ({result.Notation}): {result.ErrorMessage}";
			chatLogNode?.AddMessage(errorMessage, Colors.OrangeRed, isCombatLog: true);
		}
	}

	private void TestDiceRoller()
	{
		// GD.Print("\n--- Testing DiceRoller ---"); // Console output can be removed or kept for debugging
		chatLogNode?.AddMessage("--- Running DiceRoller Tests ---", Colors.MediumPurple);
		string[] testNotations = {
			"3d6", "1d20+5", "d8-1", "d6", "100", // Valid
			"invalid_string", "2d6+", "d", "3d0", "0d6", // Invalid
			"1d6+0", "2d4-0", "1d1000", "1001d6" // Edge cases
		};

		foreach (string notation in testNotations)
		{
			DiceRollResult result = DiceRoller.Roll(notation);
			LogDiceResult(result); // Log to chat UI

			// Keep console print for debugging if desired, or remove
			// GD.Print($"Input: \"{notation}\"");
			// GD.Print($"  Success: {result.IsSuccess}");
			// if (result.IsSuccess)
			// {
			// 	GD.Print($"  Total: {result.Total}");
			// 	GD.Print($"  Rolls: [{string.Join(", ", result.IndividualRolls)}]");
			// 	GD.Print($"  Breakdown: {result.Breakdown}");
			// }
			// else
			// {
			// 	GD.Print($"  Error: {result.ErrorMessage}");
			// }
			// GD.Print("-----");
		}
		// GD.Print("--- End DiceRoller Test ---\n");
		chatLogNode?.AddMessage("--- DiceRoller Tests Complete ---", Colors.MediumPurple);
	}

	private void SpawnToken(Vector2 position)
	{
		if (_tokenScene == null) return;

		Node tokenInstanceNode = _tokenScene.Instantiate();
		if (tokenInstanceNode is Token tokenInstance)
		{
			tokenInstance.Position = position;
			tokenInstance.Name = $"Token_{GD.Randi() % 1000}"; // Give a unique name for selection differentiation if sheet name is same
			// Load the default icon texture
			tokenInstance.TokenTexture = GD.Load<Texture2D>("res://icon.svg");

			// Connect Token's InputEvent to MainScene's handler
			// Note: Token's own _InputEvent still fires for its internal logic (like dragging)
			tokenInstance.InputEvent += (viewport, @event, shapeIdx) => OnTokenInputEvent(viewport, @event, shapeIdx, tokenInstance);
			tokenInstance.InitializeVision(pixelsPerUnit);
			tokenInstance.InitializeMovementLimits(pixelsPerUnit, GameUnitsPerGridSquare); // Use const GameUnitsPerGridSquare

			// Create and assign CharacterSheet
			CharacterSheet sheet = new CharacterSheet
			{
				Name = $"Token {GD.Randi() % 1000}", // Example: "Token 123"
				MaxHealthPoints = (int)(GD.Randi() % 10) + 5, // Random HP between 5 and 14
				CurrentHealthPoints = (int)(GD.Randi() % 10) + 5, // Random HP between 5 and 14
				ArmorClass = (int)(GD.Randi() % 6) + 10, // Random AC between 10 and 15
				Speed = 30
			};
			sheet.CurrentHealthPoints = sheet.MaxHealthPoints; // Start with full health
			sheet.CustomProperties.Add("Faction", "Generic Monster");
			sheet.CustomProperties.Add("Challenge Rating", (GD.Randi() % 3 + 1).ToString());


			tokenInstance.Sheet = sheet;

			// The Token script's _Ready method will apply this texture
			AddChild(tokenInstance);
			// GD.Print($"Spawned token '{sheet.Name}' at {position}. HP: {sheet.CurrentHealthPoints}/{sheet.MaxHealthPoints}, AC: {sheet.ArmorClass}");
			// GD.Print($"    Custom Properties: Faction='{sheet.CustomProperties["Faction"]}', CR='{sheet.CustomProperties["Challenge Rating"]}'");
			chatLogNode?.AddMessage($"Spawned token '{sheet.Name}' (HP: {sheet.CurrentHealthPoints}/{sheet.MaxHealthPoints}, AC: {sheet.ArmorClass}) at {position}.", Colors.DarkTurquoise);

		}
		else
		{
			GD.PrintErr("Failed to instantiate Token scene or instance is not of type Token.");
			if (tokenInstanceNode != null)
			{
				tokenInstanceNode.QueueFree(); // Clean up if instantiation happened but type was wrong
			}
		}
	}

	private bool _isTokenCurrentlyBeingDragged = false; // MainScene's flag for active drag operation
	private bool _dragWasActuallyMovement = false; // Flag to check if mouse moved significantly during drag

	private void OnTokenInputEvent(Node viewport, InputEvent @event, int shapeIdx, Token tokenInstance)
	{
		if (tokenInstance == null) return;

		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseButtonEvent.Pressed)
			{
				_isTokenCurrentlyBeingDragged = true;
				_dragWasActuallyMovement = false; // Reset this on new press
				tokenInstance.StartDrag(); // Notify token it's being dragged

				// Selection logic: select on press, unless it's already selected (then it's a drag)
				if (_selectedToken != tokenInstance)
				{
					if (_selectedToken != null) _selectedToken.SetSelectionVisual(false);
					_selectedToken = tokenInstance;
					_selectedToken.SetSelectionVisual(true);
					chatLogNode?.AddMessage($"Token '{_selectedToken.Sheet?.Name ?? _selectedToken.Name}' selected.", Colors.Cyan);
				}
				// Consume event so _UnhandledInput doesn't immediately try to pathfind if it was a right-click (though this is left click)
				GetViewport().SetInputAsHandled();
			}
			else // Mouse button released
			{
				if (_isTokenCurrentlyBeingDragged && _selectedToken == tokenInstance) // Ensure we're releasing the token we think we're dragging
				{
					bool dragValid = _selectedToken.EndDrag(); // Token handles collision check and revert if needed
					if (!dragValid)
					{
						chatLogNode?.AddMessage($"Token '{_selectedToken.Sheet?.Name ?? _selectedToken.Name}' placement failed, reverted.", Colors.Orange);
					}

					// If it was just a click (no significant mouse movement during drag), and it's the selected token.
					// The selection already happened on press.
					// If _dragWasActuallyMovement is false, it was a click on an already selected token.
					// No specific action needed here for selection on "click on already selected".
				}
				_isTokenCurrentlyBeingDragged = false;
				_dragWasActuallyMovement = false; // Reset
				GetViewport().SetInputAsHandled();
			}
		}

	private void OnShareHandoutButtonPressed()
	{
		if (handoutDisplayScene == null)
		{
			chatLogNode?.AddMessage("Error: HandoutDisplay scene not set in MainScene.", Colors.Red);
			GD.PrintErr("HandoutDisplay scene not linked in MainScene inspector.");
			return;
		}
		if (handoutImageFileDialog == null)
		{
			GD.PrintErr("HandoutImageFileDialog is null.");
			return;
		}
		handoutImageFileDialog.CurrentPath = "res://assets/handouts/";
		handoutImageFileDialog.PopupCentered();
	}

	private void OnHandoutImageFileSelected(string path)
	{
		if (handoutDisplayScene == null)
		{
			GD.PrintErr("HandoutDisplay scene not linked, cannot show handout.");
			return;
		}

		var imageTexture = ResourceLoader.Load<Texture2D>(path);
		if (imageTexture == null)
		{
			chatLogNode?.AddMessage($"Error: Failed to load handout image from '{path}'.", Colors.OrangeRed);
			return;
		}

		Node handoutNode = handoutDisplayScene.Instantiate();
		if (handoutNode is HandoutDisplay handoutInstance)
		{
			AddChild(handoutInstance); // Add to scene so it can be seen and interact
			handoutInstance.DisplayHandout(imageTexture);
			chatLogNode?.AddMessage($"GM shared handout: {path.GetFile()}", Colors.MediumPurple);
		}
		else
		{
			GD.PrintErr("Failed to instance HandoutDisplay or instanced node is not of HandoutDisplay type.");
			handoutNode?.QueueFree(); // Clean up if wrong type
		}
	}

	private void LoadNotes()
	{
		if (notesTextEdit == null) return;
		ConfigFile cfg = new ConfigFile();
		Error err = cfg.Load(NotesFilePath);
		if (err == Error.Ok)
		{
			notesTextEdit.Text = cfg.GetValue("Notes", "Content", "").ToString();
		}
		else if (err != Error.FileNotFound) // Don't log error if file simply doesn't exist yet
		{
			GD.PrintErr($"Error loading notes: {err}");
			chatLogNode?.AddMessage($"Error loading notes: {err}", Colors.Red);
		}
	}

	private void OnToggleNotesButtonPressed()
	{
		if (notesPanel == null) return;
		_soundManager?.PlaySfx("ui_click.wav.txt");
		notesPanel.Visible = !notesPanel.Visible;
		if (notesPanel.Visible)
		{
			notesTextEdit?.GrabFocus();
			chatLogNode?.AddMessage("Notes panel shown.", Colors.DarkGray);
		}
		else
		{
			chatLogNode?.AddMessage("Notes panel hidden.", Colors.DarkGray);
		}
	}

	private void OnNotesTextChanged()
	{
		if (notesTextEdit == null) return;
		ConfigFile cfg = new ConfigFile();
		cfg.SetValue("Notes", "Content", notesTextEdit.Text);
		Error err = cfg.Save(NotesFilePath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"Error saving notes: {err}");
			chatLogNode?.AddMessage($"Error saving notes: {err}", Colors.Red);
		}
	}

	private void OnToggleDecksPanelButtonPressed()
	{
		if (decksPanel == null) return;
		_soundManager?.PlaySfx("ui_click.wav.txt");
		decksPanel.Visible = !decksPanel.Visible;
		if (decksPanel.Visible)
		{
			chatLogNode?.AddMessage("Decks panel shown.", Colors.DarkSlateBlue);
		}
		else
		{
			chatLogNode?.AddMessage("Decks panel hidden.", Colors.DarkSlateBlue);
		}
	}

	private void OnToggleMusicButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_soundManager == null) return;
		_isMusicPlaying = !_isMusicPlaying;
		if (_isMusicPlaying) _soundManager.PlayMusic("ambient_music.ogg.txt");
		else _soundManager.StopMusic();
		UpdateMusicButtonText();
	}

	private void UpdateMusicButtonText()
	{
		if (toggleMusicButton != null)
			toggleMusicButton.Text = _isMusicPlaying ? "Music: Stop" : "Music: Play";
	}

	private void OnToggleMacroPanelButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (macroPanel == null) return;
		macroPanel.Visible = !macroPanel.Visible;
		if (macroPanel.Visible)
		{
			macroInputTextEdit?.GrabFocus();
			chatLogNode?.AddMessage("Macro panel shown.", Colors.DarkGoldenrod);
		}
		else
		{
			chatLogNode?.AddMessage("Macro panel hidden.", Colors.DarkGoldenrod);
		}
	}

	private void OnRunMacroButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (macroInputTextEdit == null || chatLogNode == null)
		{
			GD.PrintErr("Macro input or ChatLog node is missing.");
			return;
		}

		string script = macroInputTextEdit.Text;
		if (string.IsNullOrWhiteSpace(script))
		{
			chatLogNode.AddMessage("[MACRO] Script is empty.", Colors.OrangeRed);
			return;
		}

		MacroContext context = new MacroContext {
			Chat = this.chatLogNode,
			SelectedToken = this._selectedToken, // Can be null
			Combat = this.combatTracker,       // Can be null if not used in a context where CombatTracker is ready
			MainSceneInstance = this
			// DiceRoller is static, so no instance in context needed
		};

		chatLogNode.AddMessage($"[MACRO] Executing script...", Colors.DarkGoldenrod);
		MacroEngine.ExecuteMacro(script, context);
		chatLogNode.AddMessage($"[MACRO] Execution finished.", Colors.DarkGoldenrod);
	}

	private void OnSaveCampaignButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (campaignFileDialog == null) { GD.PrintErr("CampaignFileDialog is null!"); return; }
		campaignFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
		campaignFileDialog.ClearFilters();
		campaignFileDialog.AddFilter($"*{CampaignFileExtension} ; VTT Campaign File");
		campaignFileDialog.CurrentPath = $"user://campaign_save{CampaignFileExtension}";
		campaignFileDialog.PopupCentered();
	}

	private void OnLoadCampaignButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (campaignFileDialog == null) { GD.PrintErr("CampaignFileDialog is null!"); return; }
		campaignFileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
		campaignFileDialog.ClearFilters();
		campaignFileDialog.AddFilter($"*{CampaignFileExtension} ; VTT Campaign File");
		campaignFileDialog.CurrentPath = "user://";
		campaignFileDialog.PopupCentered();
	}

	private void OnCampaignFileSelected(string path)
	{
		if (campaignFileDialog == null) return;

		if (campaignFileDialog.FileMode == FileDialog.FileModeEnum.SaveFile)
		{
			if (!path.EndsWith(CampaignFileExtension))
			{
				path += CampaignFileExtension;
			}
			CampaignManager.SaveCampaign(path, this);
		}
		else
		{
			CampaignManager.LoadCampaign(path, this);
		}
	}

	public override void _Process(double delta)
	{
		if (_isTokenCurrentlyBeingDragged && _selectedToken != null)
		{
			Vector2 currentMousePos = GetGlobalMousePosition();
			if (_selectedToken.GlobalPosition.DistanceSquaredTo(currentMousePos) > 25)
			{
				_dragWasActuallyMovement = true;
			}
			_selectedToken.UpdateDragPosition(currentMousePos);
		}
	}

	private void OnAddSelectedTokenPressed()
	{
		if (_selectedToken == null)
		{
			chatLogNode?.AddMessage("Error: No token selected to add to combat.", Colors.OrangeRed);
			return;
		}

		string tokenDisplayName = _selectedToken.Sheet?.Name ?? _selectedToken.Name ?? "Unnamed Token";

		// Recreate dialog each time to ensure it's fresh and correct token name is displayed
		if (_initiativeDialog != null)
		{
			_initiativeDialog.QueueFree();
		}

		_initiativeDialog = new AcceptDialog();
		_initiativeDialog.Title = "Enter Initiative";

		VBoxContainer vbox = new VBoxContainer();
		Label promptLabel = new Label { Text = $"Enter initiative for {tokenDisplayName}:" };
		vbox.AddChild(promptLabel);

		_initiativeLineEdit = new LineEdit { PlaceholderText = "e.g., 15" };
		vbox.AddChild(_initiativeLineEdit);

		_initiativeDialog.AddChild(vbox);
		_initiativeDialog.Confirmed += OnInitiativeDialogConfirmed;
		// Lambda to clean up dialog if it's closed via escape key or close button
		_initiativeDialog.Canceled += () => {
			if (_initiativeDialog != null) _initiativeDialog.QueueFree();
			_initiativeDialog = null;
		};
		_initiativeDialog.CloseRequested += () => { // Also handles 'X' button
			if (_initiativeDialog != null) _initiativeDialog.QueueFree();
			_initiativeDialog = null;
		};


		AddChild(_initiativeDialog); // Add to scene tree to make it visible
		_initiativeDialog.PopupCentered();
		_initiativeLineEdit.GrabFocus(); // Focus the LineEdit for immediate input
	}

	private void OnInitiativeDialogConfirmed()
	{
		if (_selectedToken == null || _initiativeLineEdit == null || combatTracker == null)
		{
			GD.PrintErr("Dialog confirmation error: Null references during confirmation.");
			CleanUpInitiativeDialog();
			return;
		}

		string text = _initiativeLineEdit.Text;
		if (int.TryParse(text, out int initiative))
		{
			string combatantName = _selectedToken.Sheet?.Name ?? _selectedToken.Name ?? "Unnamed Combatant";
			Combatant newCombatant = new Combatant(combatantName, initiative, _selectedToken);
			combatTracker.AddCombatantEntry(newCombatant);
			// Specific log for adding combatant is handled by CombatTracker.AddCombatantEntry
		}
		else
		{
			chatLogNode?.AddMessage($"Error: Invalid initiative value '{text}'. Please enter a number.", Colors.OrangeRed);
		}
		CleanUpInitiativeDialog();
	}

	private void CleanUpInitiativeDialog()
	{
		if (_initiativeDialog != null)
		{
			_initiativeDialog.QueueFree();
			_initiativeDialog = null;
		}
		_initiativeLineEdit = null; // LineEdit is a child of dialog, so it's freed with it
	}

	private void OnChangeTokenImageButtonPressed()
	{
		if (_selectedToken == null)
		{
			chatLogNode?.AddMessage("Error: Select a token first before changing its image.", Colors.OrangeRed);
			return;
		}

		if (tokenImageFileDialog == null)
		{
			GD.PrintErr("TokenImageFileDialog is null in OnChangeTokenImageButtonPressed.");
			return;
		}

		// Try to set current path based on existing texture
		if (_selectedToken.TokenTexture != null && !string.IsNullOrEmpty(_selectedToken.TokenTexture.ResourcePath))
		{
			string dir = _selectedToken.TokenTexture.ResourcePath.GetBaseDir();
			if (DirAccess.DirExistsAbsolute(dir) || ResourceLoader.Exists(dir)) // Check if dir is valid
			{
				tokenImageFileDialog.CurrentPath = dir;
			}
			else
			{
				tokenImageFileDialog.CurrentPath = "res://assets/tokens/";
			}
		}
		else
		{
			tokenImageFileDialog.CurrentPath = "res://assets/tokens/";
		}

		tokenImageFileDialog.PopupCentered();
	}

	private void OnTokenImageFileSelected(string path)
	{
		if (_selectedToken == null)
		{
			chatLogNode?.AddMessage("Error: No token selected to apply image to.", Colors.OrangeRed);
			return;
		}

		var newTexture = ResourceLoader.Load<Texture2D>(path);
		if (newTexture == null)
		{
			chatLogNode?.AddMessage($"Error: Failed to load image from '{path}'.", Colors.OrangeRed);
			return;
		}

		_selectedToken.TokenTexture = newTexture; // This should update the visual via the setter in Token.cs
		string tokenName = _selectedToken.Sheet?.DisplayText ?? _selectedToken.Name ?? "Unnamed Token";
		chatLogNode?.AddMessage($"Token '{tokenName}' image changed to {path.GetFile()}.", Colors.LawnGreen);
	}

	private void OnLoadMapButtonPressed()
	{
		if (mapFileDialog == null)
		{
			GD.PrintErr("MapFileDialog is null.");
			return;
		}
		mapFileDialog.CurrentPath = "res://assets/maps/";
		mapFileDialog.PopupCentered();
	}

	private void OnMapFileSelected(string path) // This now just calls the refactored LoadMap
	{
		LoadMap(path);
	}

	public bool LoadMap(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			mapBackgroundSprite.Texture = null; // Clear map if path is empty
			chatLogNode?.AddMessage("Map cleared.", Colors.MediumPurple);
			return true;
		}

		if (mapBackgroundSprite == null)
		{
			GD.PrintErr("MapBackgroundSprite is null. Cannot load map.");
			return false;
		}

		if (!ResourceLoader.Exists(path)) // More robust check than FileAccess for res:// paths
		{
			chatLogNode?.AddMessage($"Error: Map image not found at '{path}'.", Colors.OrangeRed);
			GD.PrintErr($"Map image not found at '{path}'.");
			return false;
		}

		var newTexture = ResourceLoader.Load<Texture2D>(path);
		if (newTexture == null)
		{
			chatLogNode?.AddMessage($"Error: Failed to load map image from '{path}'.", Colors.OrangeRed);
			return false;
		}
		mapBackgroundSprite.Texture = newTexture;
		chatLogNode?.AddMessage($"Map changed to {path.GetFile()}.", Colors.MediumPurple);
		return true;
	}


	public void ClearExistingCampaignState()
	{
		// Clear tokens
		foreach (Token token in GetTokens()) // Using new GetTokens() helper
		{
			token.QueueFree();
		}
		_selectedToken = null; // Clear selection

		// Clear drawings
		drawingOverlay?.ClearDrawings();
		drawingOverlay?.ClearTemporaryMeasurement(); // Also clear any temp measurement lines

		// Reset combat tracker
		combatTracker?.ResetCombat(); // Assumes CombatTracker.ResetCombat() clears its state

		// Clear map
		if (mapBackgroundSprite != null)
		{
			mapBackgroundSprite.Texture = null;
		}

		// Clear notes (optional, could be persistent across campaign loads or part of campaign save)
		// notesTextEdit?.Clear();
		// For now, notes are independent.

		// Clear decks (optional, similar to notes)
		// DeckManager.AvailableDecks.Clear(); DeckManager.OnDecksChanged?.Invoke();
		// For now, decks are independent.

		// Clear chat log (optional)
		// chatLogNode?.Clear(); // Assuming ChatLog has a Clear method
		// chatLogNode?.AddMessage("Campaign state cleared.", Colors.Gray);

		GD.Print("Cleared existing campaign state.");
	}

	public Godot.Collections.Array<Token> GetTokens()
	{
		var tokens = new Godot.Collections.Array<Token>();
		foreach (Node child in GetChildren()) // Assuming tokens are direct children of MainScene
		{
			if (child is Token token)
			{
				tokens.Add(token);
			}
		}
		return tokens;
	}

	// Ensure SpawnToken returns the Token instance
	private Token SpawnToken(Vector2 position) // Changed to public and return Token
	{
		if (_tokenScene == null)
		{
			GD.PrintErr("Token scene not loaded in SpawnToken!");
			return null;
		}

		Node tokenInstanceNode = _tokenScene.Instantiate();
		if (tokenInstanceNode is Token tokenInstance)
		{
			tokenInstance.Position = position; // Initial position, might be overridden by TokenData
			tokenInstance.Name = $"Token_{GD.Randi() % 10000}";

			tokenInstance.InitializeVision(pixelsPerUnit);
			tokenInstance.InitializeMovementLimits(pixelsPerUnit, GameUnitsPerGridSquare);

			// Default sheet if not loaded from campaign data later
			CharacterSheet sheet = new CharacterSheet
			{
				Name = $"Creature {GD.Randi() % 1000}",
				MaxHealthPoints = (int)(GD.Randi() % 20) + 5,
				ArmorClass = (int)(GD.Randi() % 8) + 10,
				Speed = 30
			};
			sheet.CurrentHealthPoints = sheet.MaxHealthPoints;
			tokenInstance.Sheet = sheet;

			AddChild(tokenInstance);
			chatLogNode?.AddMessage($"Spawned new token '{sheet.Name}' at {position}.", Colors.DarkTurquoise);
			return tokenInstance; // Return the created token
		}
		else
		{
			GD.PrintErr("Failed to instantiate Token scene or instance is not of type Token.");
			tokenInstanceNode?.QueueFree();
			return null;
		}
	}

	// New method for loading tokens from save data
	public Token SpawnTokenAndApplyData(TokenData tokenData)
	{
		if (_tokenScene == null)
		{
			GD.PrintErr("Token scene not loaded in SpawnTokenAndApplyData!");
			return null;
		}

		Node tokenInstanceNode = _tokenScene.Instantiate();
		if (tokenInstanceNode is Token tokenInstance)
		{
			// It's important to set Name before adding to scene if other systems rely on Name during _Ready or for finding.
			// However, ApplyTokenData might set some properties that also affect _Ready.
			// For now, set name, add child, then apply data.
			tokenInstance.Name = tokenData.NodeName ?? $"Token_{GD.Randi() % 10000}";
			AddChild(tokenInstance); // Add to scene BEFORE applying data that might affect visuals or require node readiness

			tokenInstance.InitializeVision(pixelsPerUnit);
			tokenInstance.InitializeMovementLimits(pixelsPerUnit, GameUnitsPerGridSquare);
			tokenInstance.ApplyTokenData(tokenData); // Apply all other data (pos, texture, sheet, vision states, size)

			// Log specific to loading a token
			chatLogNode?.AddMessage($"Loaded token '{tokenInstance.Sheet?.Name ?? tokenInstance.Name}' at {tokenInstance.GlobalPosition}.", Colors.LightBlue);
			return tokenInstance;
		}
		else
		{
			GD.PrintErr("Failed to instantiate Token scene or instance is not of type Token for SpawnTokenAndApplyData.");
			tokenInstanceNode?.QueueFree();
			return null;
		}
	}


	private void OnToggleDrawModeButtonPressed()
	{
		if (drawingOverlay == null)
		{
			GD.PrintErr("DrawingOverlay is null for Draw Mode button.");
			return;
		}
		drawingOverlay.IsDrawingEnabled = !drawingOverlay.IsDrawingEnabled;
		if (toggleDrawModeButton != null)
		{
			toggleDrawModeButton.Text = drawingOverlay.IsDrawingEnabled ? "Draw: ON" : "Draw: OFF";
		}
		_soundManager?.PlaySfx("ui_click.wav.txt"); // Added SFX
		chatLogNode?.AddMessage($"Drawing mode: {(drawingOverlay.IsDrawingEnabled ? "Enabled" : "Disabled")}.",
			drawingOverlay.IsDrawingEnabled ? Colors.LightSeaGreen : Colors.Orange);

		// If drawing is being enabled, ensure measure mode is off
		if (drawingOverlay.IsDrawingEnabled && _isMeasureModeActive)
		{
			_isMeasureModeActive = false;
			UpdateMeasureModeButtonText(); // This will call PlaySfx again if it's also a toggle
			drawingOverlay?.ClearTemporaryMeasurement();
			chatLogNode?.AddMessage("Measurement mode disabled (drawing enabled).", Colors.Orange);
		}
	}

	private void OnToggleMeasureModeButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt"); // Added SFX
		_isMeasureModeActive = !_isMeasureModeActive;
		UpdateMeasureModeButtonText();

		if (_isMeasureModeActive)
		{
			chatLogNode?.AddMessage("Measurement mode: Enabled. Click and drag to measure.", Colors.LightSeaGreen);
			if (drawingOverlay != null && drawingOverlay.IsDrawingEnabled)
			{
				drawingOverlay.IsDrawingEnabled = false;
				if (toggleDrawModeButton != null) toggleDrawModeButton.Text = "Draw: OFF";
				chatLogNode?.AddMessage("Drawing mode disabled (measurement enabled).", Colors.Orange);
			}
		}
		else
		{
			chatLogNode?.AddMessage("Measurement mode: Disabled.", Colors.Orange);
			_isMeasuring = false;
			drawingOverlay?.ClearTemporaryMeasurement();
		}
	}

	private void UpdateMeasureModeButtonText()
	{
		if (toggleMeasureModeButton != null)
		{
			toggleMeasureModeButton.Text = _isMeasureModeActive ? "Measure: ON" : "Measure: OFF";
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (GetViewport().GuiGetFocusOwner() != null)
		{
			// If currently measuring and user clicks on UI, cancel measurement.
			if (_isMeasuring)
			{
				_isMeasuring = false;
				drawingOverlay?.ClearTemporaryMeasurement();
			}
			return;
		}

		{
			// If currently measuring and user clicks on UI, cancel measurement.
			if (_isMeasuring)
			{
				_isMeasuring = false;
				drawingOverlay?.ClearTemporaryMeasurement();
			}
			return; // UI has focus, do not process further for game actions
		}

		// Pathfinding on Right Click
		if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right && rmb.Pressed)
		{
			if (_selectedToken != null)
			{
				Vector2 targetPosition = GetGlobalMousePosition();

				// Gather obstacles (StaticBody2D nodes under "Walls")
				var obstacles = new Godot.Collections.Array<StaticBody2D>();
				Node wallsNode = GetNodeOrNull("Walls");
				if (wallsNode != null)
				{
					foreach (Node child in wallsNode.GetChildren())
					{
						if (child is StaticBody2D staticBody)
						{
							obstacles.Add(staticBody);
						}
					}
				}

				Rect2 mapBounds = mapBackgroundSprite != null ? mapBackgroundSprite.GetRect() : GetViewportRect();
				if (mapBackgroundSprite.Texture == null) mapBounds = GetViewportRect(); // Fallback if no map texture

				PhysicsDirectSpaceState2D spaceState = GetWorld2D().DirectSpaceState;

				List<Vector2> path = Pathfinder.FindPath(
					_selectedToken.GlobalPosition,
					targetPosition,
					obstacles,
					mapBounds,
					pixelsPerUnit, // Use the class field for gridCellSize
					spaceState
				);

				if (path != null && path.Count > 0)
				{
					_selectedToken.MoveAlongPath(path);
					chatLogNode?.AddMessage($"Path found for '{_selectedToken.Sheet?.Name ?? _selectedToken.Name}'. Moving...", Colors.GreenYellow);
				}
				else
				{
					chatLogNode?.AddMessage($"No path found for '{_selectedToken.Sheet?.Name ?? _selectedToken.Name}'.", Colors.OrangeRed);
				}
				GetViewport().SetInputAsHandled();
				return; // Pathfinding attempt handled, consume event
			}
		}


		// Measurement Logic (only if not pathfinding right click)
		if (_isMeasureModeActive)
		{
			bool eventHandled = false;
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_isMeasuring = true;
					_measurementStartPoint = GetGlobalMousePosition();
					_measurementEndPoint = _measurementStartPoint;
					drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint, _measurementEndPoint, "0 " + GameUnitName);
					eventHandled = true;
				}
				else // Released
				{
					if (_isMeasuring)
					{
						_isMeasuring = false;
						_measurementEndPoint = GetGlobalMousePosition();
						float pixelDistance = _measurementStartPoint.DistanceTo(_measurementEndPoint);
						float gameDistance = (pixelDistance / pixelsPerUnit) * GameUnitsPerGridSquare;
						string distanceText = $"{gameDistance:F1} {GameUnitName}";
						drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint, _measurementEndPoint, distanceText);
						chatLogNode?.AddMessage($"Measured: {distanceText}", Colors.Cyan);
						eventHandled = true;
					}
				}
			}
			else if (@event is InputEventMouseMotion mm)
			{
				if (_isMeasuring)
				{
					_measurementEndPoint = GetGlobalMousePosition();
					float pixelDistance = _measurementStartPoint.DistanceTo(_measurementEndPoint);
					float gameDistance = (pixelDistance / pixelsPerUnit) * GameUnitsPerGridSquare;
					string distanceText = $"{gameDistance:F1} {GameUnitName}";
					drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint, _measurementEndPoint, distanceText);
					eventHandled = true;
				}
			}

			if (eventHandled)
			{
				GetViewport().SetInputAsHandled();
			}
		}
		// If no specific mode handled input, do not call SetInputAsHandled generally for _UnhandledInput
		// unless a specific interaction within a mode (like measurement) consumed it.
	}
}
