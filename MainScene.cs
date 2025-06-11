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

	private Token _selectedToken = null;
	private PackedScene _tokenScene;
	private bool _isMeasureModeActive = false;
	private Vector2 _measurementStartPoint = Vector2.Zero;
	private Vector2 _measurementEndPoint = Vector2.Zero;
	private bool _isMeasuring = false; // True when mouse button is down during measurement

	// Constants for measurement conversion
	private const float PixelsPerGridUnit = 50.0f; // e.g., 50 pixels per grid square
	private const float GameUnitsPerGridSquare = 5.0f; // e.g., 5 feet per grid square
	private const string GameUnitName = "ft";
	private AcceptDialog _initiativeDialog;
	private LineEdit _initiativeLineEdit;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// NodePath assignments from .tscn
		if (chatLogNode == null) GD.PrintErr("ChatLog node not found.");
		if (chatMessageInput == null) GD.PrintErr("ChatMessageInput node not found.");
		if (combatTracker == null) GD.PrintErr("CombatTracker node not found.");
		else combatTracker.Initialize(chatLogNode); // Pass ChatLog to CombatTracker

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

		if (chatMessageInput != null)
		{
			chatMessageInput.TextSubmitted += OnChatMessageSubmitted;
		}

		chatLogNode?.AddMessage("System: Welcome to the VTT!", Colors.Aqua);
		chatLogNode?.AddMessage("System: Place custom images in 'assets/tokens/' and 'assets/maps/'.", Colors.CornflowerBlue);

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

	private bool _isTokenCurrentlyBeingDragged = false; // Helps distinguish click from drag release for selection

	private void OnTokenInputEvent(Node viewport, InputEvent @event, int shapeIdx, Token tokenInstance)
	{
		if (tokenInstance == null) return;

		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseButtonEvent.Pressed)
			{
				// This flag helps to determine on mouse release if the action was a click or a drag.
				// Token's internal _isDragging handles the actual movement.
				_isTokenCurrentlyBeingDragged = true;
			}
			else // Mouse button released
			{
				// Check if the token itself considers this a drag completion or a pure click.
				// If tokenInstance.IsDragging() is true here, it means the token moved.
				// If false, it means it was likely a click without significant mouse movement.
				if (_isTokenCurrentlyBeingDragged && !tokenInstance.IsDragging())
				{
					if (_selectedToken != null && _selectedToken != tokenInstance)
					{
						_selectedToken.SetSelectionVisual(false);
					}
					_selectedToken = tokenInstance;
					_selectedToken.SetSelectionVisual(true); // Visually select the token
					chatLogNode?.AddMessage($"Token '{_selectedToken.Sheet?.Name ?? _selectedToken.Name}' selected.", Colors.Cyan);
				}
				_isTokenCurrentlyBeingDragged = false; // Reset flag for next input sequence
			}
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

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
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

	private void OnMapFileSelected(string path)
	{
		if (mapBackgroundSprite == null)
		{
			GD.PrintErr("MapBackgroundSprite is null.");
			return;
		}
		var newTexture = ResourceLoader.Load<Texture2D>(path);
		if (newTexture == null)
		{
			chatLogNode?.AddMessage($"Error: Failed to load map image from '{path}'.", Colors.OrangeRed);
			return;
		}
		mapBackgroundSprite.Texture = newTexture;
		chatLogNode?.AddMessage($"Map changed to {path.GetFile()}.", Colors.MediumPurple);
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
		chatLogNode?.AddMessage($"Drawing mode: {(drawingOverlay.IsDrawingEnabled ? "Enabled" : "Disabled")}.",
			drawingOverlay.IsDrawingEnabled ? Colors.LightSeaGreen : Colors.Orange);

		// If drawing is being enabled, ensure measure mode is off
		if (drawingOverlay.IsDrawingEnabled && _isMeasureModeActive)
		{
			_isMeasureModeActive = false;
			UpdateMeasureModeButtonText();
			drawingOverlay?.ClearTemporaryMeasurement(); // Clear any measurement visuals
			chatLogNode?.AddMessage("Measurement mode disabled (drawing enabled).", Colors.Orange);
		}
	}

	private void OnToggleMeasureModeButtonPressed()
	{
		_isMeasureModeActive = !_isMeasureModeActive;
		UpdateMeasureModeButtonText();

		if (_isMeasureModeActive)
		{
			chatLogNode?.AddMessage("Measurement mode: Enabled. Click and drag to measure.", Colors.LightSeaGreen);
			// If measure mode is being enabled, ensure drawing mode is off
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
			_isMeasuring = false; // Stop any active measurement
			drawingOverlay?.ClearTemporaryMeasurement(); // Clear visuals
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
		// If a UI element has focus, don't process global input for measurement.
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

		if (!_isMeasureModeActive)
		{
			// If not in measure mode, ensure no input is consumed by this logic.
			// However, if _UnhandledInput is used, we might not need to explicitly call base.
			return;
		}

		bool eventHandled = false;
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_isMeasuring = true;
				_measurementStartPoint = GetGlobalMousePosition();
				_measurementEndPoint = _measurementStartPoint;
				// Initial draw or clear previous one if any
				drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint, _measurementEndPoint, "0 " + GameUnitName);
				eventHandled = true;
			}
			else // Released
			{
				if (_isMeasuring)
				{
					_isMeasuring = false;
					_measurementEndPoint = GetGlobalMousePosition(); // Capture final point
					float pixelDistance = _measurementStartPoint.DistanceTo(_measurementEndPoint);
					float gameDistance = (pixelDistance / PixelsPerGridUnit) * GameUnitsPerGridSquare;
					string distanceText = $"{gameDistance:F1} {GameUnitName}";

					drawingOverlay?.DrawTemporaryMeasurement(_measurementStartPoint, _measurementEndPoint, distanceText); // Update with final distance
					chatLogNode?.AddMessage($"Measured: {distanceText}", Colors.Cyan);
					// Optional: Keep the line by not calling ClearTemporaryMeasurement, or clear after a delay/next click.
					// For now, it stays until mode is toggled off or a new measurement starts.
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
				float gameDistance = (pixelDistance / PixelsPerGridUnit) * GameUnitsPerGridSquare;
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
}
