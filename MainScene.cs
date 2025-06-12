using Godot;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks; // Required for TaskCompletionSource

public partial class MainScene : Control
{
	// Signals
	[Signal] public delegate void DrawingModeChangedEventHandler(DrawingMode newMode);
	[Signal] public delegate void MapDirtyStateChangedEventHandler(bool isDirty);
	[Signal] public delegate void CurrentMapPathChangedEventHandler(string newPath);

	// Enums
	public enum FileMenuItemId { New, Open, Save, SaveAs, AppSettings, Quit }
	private enum EditMenuItemId { Undo, Redo }
	public enum ViewMenuItemId { ToggleGridSnap, ToggleObjectSnap } // Added for View Menu
	public enum DrawingMode { Tile, Room, ObjectPlacement, SelectObject }
	public enum UnsavedChangesDialogResult { Save, DontSave, Cancel }
	public enum AlignmentMode
	{
		AlignLeft, AlignRight, AlignHorizontalCenter,
		AlignTop, AlignBottom, AlignVerticalCenter
	}
	public enum DistributionMode
	{
		DistributeHorizontalCenters,
		DistributeVerticalCenters,
		DistributeHorizontalSpacing, // New
		DistributeVerticalSpacing    // New
	}
	public enum AlignContext
	{
		ToFirstSelected,
		ToSelectionBounds
	}

	// State
	private DrawingMode currentDrawingMode = DrawingMode.Tile;
	public bool IsMapDirty { get; private set; } = false;
	private string currentMapFilePath = null;
	private string applicationName = "Dungeon Architector";

	// Core Components
	private AssetManager assetManager;
	private TileDrawer tileDrawer;
	private UndoRedoManager undoRedoManagerInstance;
	private GlobalSettings globalSettings;
	private LayersPanelController layersPanelController;

	// UI Panels & Controls
	private Panel toolbarPanel;
	private AssetLibraryPanel assetLibraryPanelInstance;
	private PanelContainer objectPropertiesPanel;
	private AppSettingsPanelController appSettingsPanelInstance; // For App Settings Panel
	private Window unsavedChangesDialogNode; // Changed from ConfirmationDialog
    private Label unsavedDialogMessageLabel; // To set the message dynamically
    private TaskCompletionSource<UnsavedChangesDialogResult> dialogTcs; // Added
	private MenuBar topMenuBar;
    private PopupMenu fileMenuPopup;
	private PopupMenu editMenuPopup;
    private int undoMenuItemIndex = -1;
    private int redoMenuItemIndex = -1;
	private PopupMenu viewMenuPopup; // Added for View Menu
    private int toggleGridSnapMenuItemIndex = -1; // Added for View Menu
    private int toggleObjectSnapMenuItemIndex = -1; // Added for View Menu
	private PopupMenu toolsMenuPopup; // Added for Tools Menu
	private Dictionary<DrawingMode, int> toolMenuItemIndices = new Dictionary<DrawingMode, int>(); // Added for Tools Menu

	// Toolbar Buttons
	private Button manageAssetsButton;
	private CheckBox scatterModeCheckBox;
	private Button undoButton;
	private Button redoButton;
	private Button eraserButton;

	// Object Properties Panel Controls
	private Label assetNameLabelInProps;
	private LineEdit rotationLineEditProp; // Changed from SpinBox
	private LineEdit scaleXLineEditProp;   // Changed from SpinBox
	private LineEdit scaleYLineEditProp;   // Changed from SpinBox
	private Button deleteObjectButtonFromPanelProp;
	// Alignment UI Controls (in ObjectPropertiesPanel)
	private Label alignmentSectionLabel;
	private HBoxContainer alignmentButtonsHBox1;
	private HBoxContainer alignmentButtonsHBox2;
	private Button alignLeftButton, alignHorizontalCenterButton, alignRightButton;
	private Button alignTopButton, alignVerticalCenterButton, alignBottomButton;
	// Distribution UI Controls (in ObjectPropertiesPanel)
	private Label distributionSectionLabel;
	private HBoxContainer distributionButtonsHBox;
	private Button distributeHCentersButton;
	private Button distributeVCentersButton;
	private Button distributeHSpacingButton; // New
	private Button distributeVSpacingButton; // New
	// Alignment Context UI
	private HBoxContainer alignContextHBox;
	private OptionButton alignContextOptionButton;

	// File Dialogs
	private FileDialog saveFileDialog;
	private FileDialog loadFileDialog;
	private TaskCompletionSource<string> saveAsTcs; // For Save As dialog

	// Selection & Gizmo State
	private List<PlacedObject> currentlySelectedObjects = new List<PlacedObject>();
	private Dictionary<PlacedObject, Color> originalModulations = new Dictionary<PlacedObject, Color>();
	private bool isDraggingObject = false;
	private Vector2 dragStartMousePosition;
	private Vector2 dragStartObjectOriginalPosition;
	private bool isUpdatingPanelFromSelection = false;
	private RotationGizmo rotationGizmoInstance;
	private const string RotationGizmoScenePath = "res://ui/gizmos/RotationGizmo.tscn";
	private float gizmoDragStartRotationForUndo;
	private ScaleGizmo scaleGizmoInstance;
	private const string ScaleGizmoScenePath = "res://ui/gizmos/ScaleGizmo.tscn";
	private Vector2 gizmoDragStartScaleForUndo;

	private CombinedTransformGizmo combinedTransformGizmoInstance;
	private const string CombinedTransformGizmoScenePath = "res://ui/gizmos/CombinedTransformGizmo.tscn";
	// Using a dictionary in case we later support multi-object gizmo ops, though initially for single.
	private Dictionary<PlacedObject, Vector2> gizmoDragStartObjectPositionsForUndo_Translate = new Dictionary<PlacedObject, Vector2>();

	private SnapFeedbackDisplay snapFeedbackDisplayInstance;
	private const string SnapFeedbackDisplayScenePath = "res://ui/gizmos/SnapFeedbackDisplay.tscn";


	public override void _Ready()
	{
		assetManager = GetNode<AssetManager>("/root/AssetManager");
		undoRedoManagerInstance = GetNode<UndoRedoManager>("/root/UndoRedoManager");
		globalSettings = GetNode<GlobalSettings>("/root/GlobalSettings");

		if (globalSettings == null) GD.PrintErr("MainScene: GlobalSettings singleton not found!");
		if (assetManager == null) GD.PrintErr("MainScene: AssetManager not found!");

		if (undoRedoManagerInstance != null)
		{
			undoRedoManagerInstance.UndoStackChanged += _OnUndoStackChanged;
			undoRedoManagerInstance.RedoStackChanged += _OnRedoStackChanged;
			if (!undoRedoManagerInstance.IsConnected(UndoRedoManager.SignalName.HistoryChanged, Callable.From(_OnUndoRedoHistoryChanged)))
			{
				undoRedoManagerInstance.Connect(UndoRedoManager.SignalName.HistoryChanged, Callable.From(_OnUndoRedoHistoryChanged));
			}
		} else { GD.PrintErr("MainScene: UndoRedoManager singleton not found!");}

		// Connect to GlobalSettings signals for View menu updates
		if (globalSettings != null)
		{
			globalSettings.GridSettingsChanged += _OnGridSettingsChangedForViewMenu;
			globalSettings.ObjectSnapSettingsChanged += _OnObjectSnapSettingsChangedForViewMenu;
		}

		// Connect to own DrawingModeChanged signal for Tools menu checkmark updates
		if (!this.IsConnected(SignalName.DrawingModeChanged, Callable.From<DrawingMode>(_OnActualDrawingModeChanged))) // Assuming long is compatible with enum for delegate
		{
			this.DrawingModeChanged += _OnActualDrawingModeChanged;
		}


		var tileMapNodes = GetTree().GetNodesInGroup("TileMapGroup");
		if (tileMapNodes.Count > 0) tileDrawer = tileMapNodes[0] as TileDrawer;
		if (tileDrawer == null) tileDrawer = GetNode<TileDrawer>("TileMap");
		if (tileDrawer == null) { GD.PrintErr("MainScene: TileDrawer node not found! Critical error."); GetTree().Quit(); return; }

		toolbarPanel = GetNode<Panel>("ToolbarPanel");
		if (toolbarPanel == null) { GD.PrintErr("MainScene: ToolbarPanel not found! Critical error."); GetTree().Quit(); return; }

		assetLibraryPanelInstance = GetNode<AssetLibraryPanel>("AssetLibraryPanelInstance");
		if (assetLibraryPanelInstance != null) {
			assetLibraryPanelInstance.Visible = false;
			assetLibraryPanelInstance.AssetsChanged += _OnAssetsChanged;
		} else { GD.PrintErr("MainScene: AssetLibraryPanelInstance not found!"); }

		if (undoRedoManagerInstance != null && tileDrawer != null) undoRedoManagerInstance.Initialize(tileDrawer);

		// --- Custom Unsaved Changes Dialog Setup ---
		unsavedChangesDialogNode = new Window
		{
			Title = "Unsaved Changes",
			Exclusive = true, // Modal
			Visible = false,
			MinSize = new Vector2I(380, 130),
			MaxSize = new Vector2I(380, 130), // Fixed size
			KeepSize = true, // Prevent user resizing
			Transient = true, // Usually closes with Esc
			AlwaysOnTop = true, // Try to keep it above other game windows if any
			ContentScaleMode = Window.ContentScaleModeEnum.Disabled, // Use fixed size
			// Position will be set before showing
		};
		// Ensure it's not resizable by user
		unsavedChangesDialogNode.SetFlag(Window.Flags.ResizeDisabled, true);

		VBoxContainer dialogVBox = new VBoxContainer { Name = "DialogVBox" };
		dialogVBox.AddThemeConstantOverride("separation", 15); // Add some spacing
		unsavedChangesDialogNode.AddChild(dialogVBox);

		unsavedDialogMessageLabel = new Label {
			Name = "MessageLabel",
			Text = "You have unsaved changes. What would you like to do?", // Default text
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(340, 0) // Ensure text wraps
		};
		dialogVBox.AddChild(unsavedDialogMessageLabel);

		var m = new MarginContainer(); // Add margin around buttons
		m.AddThemeConstantOverride("margin_left", 20);
		m.AddThemeConstantOverride("margin_right", 20);
		m.AddThemeConstantOverride("margin_top", 10);
		dialogVBox.AddChild(m);

		HBoxContainer dialogButtonsHBox = new HBoxContainer { Name = "ButtonsHBox", Alignment = HBoxContainer.AlignmentMode.Center };
		dialogButtonsHBox.AddThemeConstantOverride("separation", 10); // Spacing between buttons
		m.AddChild(dialogButtonsHBox);

		Button btnSave = new Button { Text = "Save", Name = "SaveButton", CustomMinimumSize = new Vector2(80,0) };
		btnSave.Pressed += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.Save);
		dialogButtonsHBox.AddChild(btnSave);

		Button btnDontSave = new Button { Text = "Don't Save", Name = "DontSaveButton", CustomMinimumSize = new Vector2(100,0) };
		btnDontSave.Pressed += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.DontSave);
		dialogButtonsHBox.AddChild(btnDontSave);

		Button btnCancel = new Button { Text = "Cancel", Name = "CancelButton", CustomMinimumSize = new Vector2(80,0) };
		btnCancel.Pressed += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.Cancel);
		dialogButtonsHBox.AddChild(btnCancel);

		AddChild(unsavedChangesDialogNode);

		// Connect the window's close request to the cancel action
		unsavedChangesDialogNode.CloseRequested += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.Cancel);
		// --- End Custom Dialog Setup ---

		objectPropertiesPanel = GetNode<PanelContainer>("ObjectPropertiesPanelInstance");
		if (objectPropertiesPanel != null) {
			assetNameLabelInProps = objectPropertiesPanel.GetNodeOrNull<Label>("PropertiesVBox/AssetNameHBox/AssetNameLabel");
			// Updated to LineEdit and new names
			rotationLineEditProp = objectPropertiesPanel.GetNodeOrNull<LineEdit>("PropertiesVBox/RotationHBox/RotationLineEdit");
			scaleXLineEditProp = objectPropertiesPanel.GetNodeOrNull<LineEdit>("PropertiesVBox/ScaleXHBox/ScaleXLineEdit");
			scaleYLineEditProp = objectPropertiesPanel.GetNodeOrNull<LineEdit>("PropertiesVBox/ScaleYHBox/ScaleYLineEdit");
			deleteObjectButtonFromPanelProp = objectPropertiesPanel.GetNodeOrNull<Button>("PropertiesVBox/DeleteObjectButton");

			bool panelBasicControlsValid = assetNameLabelInProps != null && rotationLineEditProp != null &&
										 scaleXLineEditProp != null && scaleYLineEditProp != null &&
										 deleteObjectButtonFromPanelProp != null;

			if (panelBasicControlsValid) {
				// Connect new TextSubmitted signals for LineEdits
				if (!rotationLineEditProp.IsConnected(LineEdit.SignalName.TextSubmitted, Callable.From<string>(_OnRotationTextSubmitted))) {
					rotationLineEditProp.TextSubmitted += _OnRotationTextSubmitted;
				}
				if (!scaleXLineEditProp.IsConnected(LineEdit.SignalName.TextSubmitted, Callable.From<string>(_OnScaleXTextSubmitted))) {
					scaleXLineEditProp.TextSubmitted += _OnScaleXTextSubmitted;
				}
				if (!scaleYLineEditProp.IsConnected(LineEdit.SignalName.TextSubmitted, Callable.From<string>(_OnScaleYTextSubmitted))) {
					scaleYLineEditProp.TextSubmitted += _OnScaleYTextSubmitted;
				}
				deleteObjectButtonFromPanelProp.Pressed += _OnPropsDeleteObjectPressed;
			} else { GD.PrintErr("MainScene: Failed to get one or more basic control nodes (Label, LineEdits, Button) from ObjectPropertiesPanelInstance!"); }

			// Get Alignment UI references
			alignmentSectionLabel = objectPropertiesPanel.GetNode<Label>("PropertiesVBox/AlignLabel");
			alignmentButtonsHBox1 = objectPropertiesPanel.GetNode<HBoxContainer>("PropertiesVBox/AlignmentButtonsHBox1");
			alignmentButtonsHBox2 = objectPropertiesPanel.GetNode<HBoxContainer>("PropertiesVBox/AlignmentButtonsHBox2");

			if (alignmentSectionLabel == null || alignmentButtonsHBox1 == null || alignmentButtonsHBox2 == null) {
				GD.PrintErr("MainScene: Failed to get alignment group nodes from ObjectPropertiesPanelInstance!");
				alignmentSectionLabel = null; alignmentButtonsHBox1 = null; alignmentButtonsHBox2 = null; // Ensure all are null if one is
			} else {
				// Get individual buttons
				alignLeftButton = alignmentButtonsHBox1.GetNode<Button>("AlignLeftButton");
				alignHorizontalCenterButton = alignmentButtonsHBox1.GetNode<Button>("AlignHorizontalCenterButton");
				alignRightButton = alignmentButtonsHBox1.GetNode<Button>("AlignRightButton");

				alignTopButton = alignmentButtonsHBox2.GetNode<Button>("AlignTopButton");
				alignVerticalCenterButton = alignmentButtonsHBox2.GetNode<Button>("AlignVerticalCenterButton");
				alignBottomButton = alignmentButtonsHBox2.GetNode<Button>("AlignBottomButton");

				bool allButtonsFound = alignLeftButton != null && alignHorizontalCenterButton != null && alignRightButton != null &&
									 alignTopButton != null && alignVerticalCenterButton != null && alignBottomButton != null;

				if (!allButtonsFound) {
					GD.PrintErr("MainScene: Failed to get all individual alignment buttons from ObjectPropertiesPanelInstance!");
					// Null out group containers as well so panel logic doesn't try to show partial UI
					alignmentSectionLabel = null; alignmentButtonsHBox1 = null; alignmentButtonsHBox2 = null;
				} else {
					// Connect signals
					alignLeftButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignLeft);
					alignHorizontalCenterButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignHorizontalCenter);
					alignRightButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignRight);

					alignTopButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignTop);
					alignVerticalCenterButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignVerticalCenter);
					alignBottomButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignBottom);

					// Ensure initially hidden (also set in .tscn but good to confirm)
					alignmentSectionLabel.Visible = false;
					alignmentButtonsHBox1.Visible = false;
					alignmentButtonsHBox2.Visible = false;
				}
			}

			// Get Align Context UI references
			alignContextHBox = objectPropertiesPanel.GetNodeOrNull<HBoxContainer>("PropertiesVBox/AlignContextHBox");
			if (alignContextHBox != null) {
				alignContextOptionButton = alignContextHBox.GetNodeOrNull<OptionButton>("AlignContextOptionButton");
				if (alignContextOptionButton != null) {
					alignContextOptionButton.Clear();
					alignContextOptionButton.AddItem("First Selected", (int)AlignContext.ToFirstSelected);
					alignContextOptionButton.AddItem("Selection Bounds", (int)AlignContext.ToSelectionBounds);
					alignContextOptionButton.Selected = (int)AlignContext.ToFirstSelected; // Default selection
				} else {
					GD.PrintErr("MainScene: AlignContextOptionButton not found in AlignContextHBox!");
				}
				alignContextHBox.Visible = false; // Ensure initially hidden
			} else {
				GD.PrintErr("MainScene: AlignContextHBox not found in ObjectPropertiesPanelInstance!");
			}

			// Get Distribution UI references
			distributionSectionLabel = objectPropertiesPanel.GetNode<Label>("PropertiesVBox/DistributionSectionLabel");
			distributionButtonsHBox = objectPropertiesPanel.GetNode<HBoxContainer>("PropertiesVBox/DistributionButtonsHBox");

			if (distributionSectionLabel == null || distributionButtonsHBox == null) {
				GD.PrintErr("MainScene: Failed to get distribution group nodes from ObjectPropertiesPanelInstance!");
				distributionSectionLabel = null; distributionButtonsHBox = null;
			} else {
				distributeHCentersButton = distributionButtonsHBox.GetNode<Button>("DistributeHCentersButton");
				distributeVCentersButton = distributionButtonsHBox.GetNode<Button>("DistributeVCentersButton");

				if (distributeHCentersButton == null || distributeVCentersButton == null) {
					GD.PrintErr("MainScene: Failed to get all individual CENTER distribution buttons from ObjectPropertiesPanelInstance!");
					// Don't null out the entire section if only some buttons are missing,
					// but the feature relying on these specific buttons will be impaired.
					// The visibility of the HBox itself can still be managed.
				} else {
					if (!distributeHCentersButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalCenters)))) {
						distributeHCentersButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalCenters);
					}
					if (!distributeVCentersButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalCenters)))) {
						distributeVCentersButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalCenters);
					}
				}

				// Get new spacing distribution buttons
				distributeHSpacingButton = distributionButtonsHBox.GetNodeOrNull<Button>("DistributeHSpacingButton");
				distributeVSpacingButton = distributionButtonsHBox.GetNodeOrNull<Button>("DistributeVSpacingButton");

				if (distributeHSpacingButton == null || distributeVSpacingButton == null) {
					GD.PrintWarn("MainScene: Failed to get all new distribution SPACING buttons from ObjectPropertiesPanelInstance! Spacing distribution might not work.");
					// Again, don't null out sectionLabel/distributionButtonsHBox, as center buttons might still work.
				} else {
					// Connect signals for new buttons
					if (!distributeHSpacingButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalSpacing)))) {
						distributeHSpacingButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalSpacing);
					}
					if (!distributeVSpacingButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalSpacing)))) {
						distributeVSpacingButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalSpacing);
					}
				}

				// Initial visibility for the entire distribution section
				distributionSectionLabel.Visible = false;
				distributionButtonsHBox.Visible = false;
			}
			objectPropertiesPanel.Visible = false;
		} else { GD.PrintErr("MainScene: ObjectPropertiesPanelInstance not found!"); }

		PackedScene rotGizmoSceneLoader = ResourceLoader.Load<PackedScene>(RotationGizmoScenePath);
				alignmentButtonsHBox2.Visible = false;
			}
			objectPropertiesPanel.Visible = false;
		} else { GD.PrintErr("MainScene: ObjectPropertiesPanelInstance not found!"); }

		PackedScene rotGizmoSceneLoader = ResourceLoader.Load<PackedScene>(RotationGizmoScenePath);
		if (rotGizmoSceneLoader != null) {
			Node rotInstance = rotGizmoSceneLoader.Instantiate();
			if (rotInstance is RotationGizmo rgi) {
				rotationGizmoInstance = rgi; AddChild(rotationGizmoInstance); rotationGizmoInstance.Visible = false;
				rotationGizmoInstance.RotationStarted += _OnGizmoRotationStarted;
				rotationGizmoInstance.RotationUpdated += _OnGizmoRotationUpdated;
				rotationGizmoInstance.RotationFinished += _OnGizmoRotationFinished;
			} else { GD.PrintErr($"Failed to cast to RotationGizmo. Path: {RotationGizmoScenePath}"); rotInstance?.QueueFree(); }
		} else { GD.PrintErr($"Failed to load RotationGizmo scene from: {RotationGizmoScenePath}"); }

		PackedScene scaleGizmoPackedScene = ResourceLoader.Load<PackedScene>(ScaleGizmoScenePath);
		if (scaleGizmoPackedScene != null) {
			Node scaleInstance = scaleGizmoPackedScene.Instantiate();
			if (scaleInstance is ScaleGizmo sgi) {
				scaleGizmoInstance = sgi; AddChild(scaleGizmoInstance); scaleGizmoInstance.Hide();
				if (!scaleGizmoInstance.IsConnected(nameof(ScaleGizmo.ScaleStarted), Callable.From<Vector2, ScaleGizmo.HandleType>(_OnGizmoScaleStarted)))
					scaleGizmoInstance.Connect(nameof(ScaleGizmo.ScaleStarted), Callable.From<Vector2, ScaleGizmo.HandleType>(_OnGizmoScaleStarted));
				if (!scaleGizmoInstance.IsConnected(nameof(ScaleGizmo.ScaleUpdated), Callable.From<Vector2>(_OnGizmoScaleUpdated)))
					scaleGizmoInstance.Connect(nameof(ScaleGizmo.ScaleUpdated), Callable.From<Vector2>(_OnGizmoScaleUpdated));
				if (!scaleGizmoInstance.IsConnected(nameof(ScaleGizmo.ScaleFinished), Callable.From<Vector2, Vector2>(_OnGizmoScaleFinished)))
					scaleGizmoInstance.Connect(nameof(ScaleGizmo.ScaleFinished), Callable.From<Vector2, Vector2>(_OnGizmoScaleFinished));
			} else { GD.PrintErr($"Failed to cast to ScaleGizmo. Path: {ScaleGizmoScenePath}"); scaleInstance?.QueueFree(); }
		} else { GD.PrintErr($"Failed to load ScaleGizmo scene from: {ScaleGizmoScenePath}"); }

		// unsavedChangesDialog is now setup above with the other UI elements.

		PopulateToolbar();
		SetupFileDialogs();

		if (undoRedoManagerInstance != null) {
			_OnUndoStackChanged(undoRedoManagerInstance.CanUndo());
			_OnRedoStackChanged(undoRedoManagerInstance.CanRedo());
		}
		UpdateObjectPropertiesPanel();
		UpdateWindowTitle();

		GetTree().AutoAcceptQuit = false; // Crucial for custom close request handling

		layersPanelController = GetNode<LayersPanelController>("LayersPanel");
		if (layersPanelController == null) GD.PrintErr("MainScene: LayersPanelController node not found!");

		// Setup AppSettingsPanel
		var placeholder = GetNode<PanelContainer>("AppSettingsPanelInstance");
		if (placeholder != null)
		{
			PackedScene appSettingsScene = ResourceLoader.Load<PackedScene>("res://ui/AppSettingsPanel.tscn");
			if (appSettingsScene != null)
			{
				appSettingsPanelInstance = appSettingsScene.Instantiate<AppSettingsPanelController>();
				if (appSettingsPanelInstance != null)
				{
					Node parent = placeholder.GetParent();
					parent.RemoveChild(placeholder);
					placeholder.QueueFree(); // Free the placeholder
					parent.AddChild(appSettingsPanelInstance); // Add the actual instance
					appSettingsPanelInstance.Name = "AppSettingsPanelInstance"; // Keep the name if needed for future GetNode calls

					if (!appSettingsPanelInstance.IsConnected(AppSettingsPanelController.SignalName.GlobalAssetPathChangedAndRefreshed, Callable.From(_OnGlobalAssetPathChanged)))
					{
						appSettingsPanelInstance.Connect(AppSettingsPanelController.SignalName.GlobalAssetPathChangedAndRefreshed, Callable.From(_OnGlobalAssetPathChanged));
					}
					appSettingsPanelInstance.Hide(); // Ensure hidden initially
				}
				else GD.PrintErr("MainScene: Failed to instantiate AppSettingsPanelController from scene.");
			}
			else GD.PrintErr("MainScene: Failed to load res://ui/AppSettingsPanel.tscn");
		}
		else GD.PrintErr("MainScene: AppSettingsPanelInstance placeholder not found!");

		// Instance SnapFeedbackDisplay
		PackedScene snapFeedbackPackedScene = ResourceLoader.Load<PackedScene>(SnapFeedbackDisplayScenePath);
		if (snapFeedbackPackedScene != null)
		{
			snapFeedbackDisplayInstance = snapFeedbackPackedScene.Instantiate<SnapFeedbackDisplay>();
			if (snapFeedbackDisplayInstance != null)
			{
				AddChild(snapFeedbackDisplayInstance);
			}
			else { GD.PrintErr($"Failed to instance SnapFeedbackDisplay from scene: {SnapFeedbackDisplayScenePath}"); }
		}
		else { GD.PrintErr($"Failed to load SnapFeedbackDisplay scene from: {SnapFeedbackDisplayScenePath}"); }

		// Instance CombinedTransformGizmo
		PackedScene combinedGizmoPackedScene = ResourceLoader.Load<PackedScene>(CombinedTransformGizmoScenePath);
		if (combinedGizmoPackedScene != null)
		{
			combinedTransformGizmoInstance = combinedGizmoPackedScene.Instantiate<CombinedTransformGizmo>();
			if (combinedTransformGizmoInstance != null)
			{
				AddChild(combinedTransformGizmoInstance);
				combinedTransformGizmoInstance.HideGizmo(); // Start hidden
				// Connect signals
				combinedTransformGizmoInstance.TranslationHandleGrabbed += _OnGizmoTranslationStarted;
				combinedTransformGizmoInstance.TranslationUpdated += _OnGizmoTranslationUpdated;
				combinedTransformGizmoInstance.TranslationHandleReleased += _OnGizmoTranslationFinished;
				// TODO: Connect Rotation and Scale signals later if CombinedTransformGizmo handles all
			}
			else { GD.PrintErr($"Failed to instance CombinedTransformGizmo from scene: {CombinedTransformGizmoScenePath}"); }
		}
		else { GD.PrintErr($"Failed to load CombinedTransformGizmo scene from: {CombinedTransformGizmoScenePath}"); }

		// Initialize File Menu
		topMenuBar = GetNodeOrNull<MenuBar>("MainLayoutVBox/TopMenuBar"); // Assumes TopMenuBar is child of MainLayoutVBox
		if (topMenuBar == null) {
			topMenuBar = GetNodeOrNull<MenuBar>("TopMenuBar"); // Fallback if TopMenuBar is direct child of MainScene
		}

		if (topMenuBar != null)
		{
			fileMenuPopup = new PopupMenu();
			fileMenuPopup.AddItem("New", (int)FileMenuItemId.New);
			fileMenuPopup.SetItemShortcut((int)FileMenuItemId.New, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.N) } });

			fileMenuPopup.AddItem("Open...", (int)FileMenuItemId.Open);
			fileMenuPopup.SetItemShortcut((int)FileMenuItemId.Open, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.O) } });

			fileMenuPopup.AddItem("Save", (int)FileMenuItemId.Save);
			fileMenuPopup.SetItemShortcut((int)FileMenuItemId.Save, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.S) } });

			fileMenuPopup.AddItem("Save As...", (int)FileMenuItemId.SaveAs);
			var saveAsShortcut = new Shortcut();
			saveAsShortcut.Events.Add(InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl | KeyModifierMask.MaskShift, Key.S));
			fileMenuPopup.SetItemShortcut((int)FileMenuItemId.SaveAs, saveAsShortcut);

			fileMenuPopup.AddSeparator();
			fileMenuPopup.AddItem("App Settings...", (int)FileMenuItemId.AppSettings);
			fileMenuPopup.AddSeparator();
			fileMenuPopup.AddItem("Quit", (int)FileMenuItemId.Quit);

			fileMenuPopup.IdPressed += _OnFileMenuItemPressed;

			topMenuBar.AddChild(fileMenuPopup);
			topMenuBar.SetMenuTitle(0, "File");
			topMenuBar.SetSubmenuPopup(0, fileMenuPopup.GetPath());
		}
		else
		{
			GD.PrintErr("MainScene: TopMenuBar node not found! File menu cannot be created.");
		}

		// Initialize Edit Menu (after File menu setup)
		if (topMenuBar != null)
        {
            editMenuPopup = new PopupMenu();

            editMenuPopup.AddItem("Undo", (int)EditMenuItemId.Undo);
            undoMenuItemIndex = editMenuPopup.ItemCount - 1;
            editMenuPopup.SetItemShortcut(undoMenuItemIndex, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.Z) } });
            editMenuPopup.SetItemDisabled(undoMenuItemIndex, !(undoRedoManagerInstance?.CanUndo() ?? false));

            editMenuPopup.AddItem("Redo", (int)EditMenuItemId.Redo);
            redoMenuItemIndex = editMenuPopup.ItemCount - 1;
            var redoShortcut = new Shortcut();
            redoShortcut.Events.Add(InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl | KeyModifierMask.MaskShift, Key.Z));
            editMenuPopup.SetItemShortcut(redoMenuItemIndex, redoShortcut);
            editMenuPopup.SetItemDisabled(redoMenuItemIndex, !(undoRedoManagerInstance?.CanRedo() ?? false));

            editMenuPopup.IdPressed += _OnEditMenuItemPressed;

            int editMenuIndex = topMenuBar.GetMenuCount();
            topMenuBar.AddChild(editMenuPopup);
            topMenuBar.SetMenuTitle(editMenuIndex, "Edit");
            topMenuBar.SetSubmenuPopup(editMenuIndex, editMenuPopup.GetPath());
        }
        // No additional error print here if topMenuBar is null, already handled by File menu setup.

		// Initialize View Menu
		if (topMenuBar != null && globalSettings != null)
		{
			viewMenuPopup = new PopupMenu();
			viewMenuPopup.Name = "ViewMenuPopup"; // Good for debugging

			viewMenuPopup.AddItem("Toggle Grid Snap", (int)ViewMenuItemId.ToggleGridSnap);
			toggleGridSnapMenuItemIndex = viewMenuPopup.ItemCount - 1;
			viewMenuPopup.SetItemAsCheckable(toggleGridSnapMenuItemIndex, true);
			viewMenuPopup.SetItemChecked(toggleGridSnapMenuItemIndex, globalSettings.IsSnapToGridEnabled);
			// No common shortcut for this toggle directly in menu, often handled by specific UI or editor settings.

			viewMenuPopup.AddItem("Toggle Object Snap", (int)ViewMenuItemId.ToggleObjectSnap);
			toggleObjectSnapMenuItemIndex = viewMenuPopup.ItemCount - 1;
			viewMenuPopup.SetItemAsCheckable(toggleObjectSnapMenuItemIndex, true);
			viewMenuPopup.SetItemChecked(toggleObjectSnapMenuItemIndex, globalSettings.IsObjectSnapEnabled);

			viewMenuPopup.IdPressed += _OnViewMenuItemPressed;

			int viewMenuIndex = topMenuBar.GetMenuCount();
			topMenuBar.AddChild(viewMenuPopup);
			topMenuBar.SetMenuTitle(viewMenuIndex, "View");
			topMenuBar.SetSubmenuPopup(viewMenuIndex, viewMenuPopup.GetPath());
		}
		else
		{
			if (topMenuBar == null) GD.PrintErr("MainScene: TopMenuBar node not found! View menu cannot be created.");
			if (globalSettings == null) GD.PrintErr("MainScene: GlobalSettings not available! View menu cannot be initialized correctly for snap toggles.");
		}

		// Initialize Tools Menu
		if (topMenuBar != null)
        {
            toolsMenuPopup = new PopupMenu();
            toolsMenuPopup.Name = "ToolsMenuPopup";
            toolMenuItemIndices = new Dictionary<DrawingMode, int>(); // Ensure it's new/empty

            DrawingMode[] modesToShowInMenu = {
                DrawingMode.Tile,
                DrawingMode.Room,
                DrawingMode.ObjectPlacement,
                DrawingMode.SelectObject
            };

            foreach (DrawingMode mode in modesToShowInMenu)
            {
                string itemName = GetToolModeUserFriendlyName(mode);
                toolsMenuPopup.AddItem(itemName, (int)mode);
                int itemIndex = toolsMenuPopup.ItemCount - 1;

                toolMenuItemIndices[mode] = itemIndex;
                toolsMenuPopup.SetItemAsCheckable(itemIndex, true);
                toolsMenuPopup.SetItemChecked(itemIndex, mode == this.currentDrawingMode);
            }

            // toolsMenuPopup.IdPressed += _OnToolsMenuItemPressed; // To be implemented in next subtask
			// Ensure the connection is active:
			if (toolsMenuPopup != null) { // Check if toolsMenuPopup itself is not null
                if (toolsMenuPopup.IsConnected(PopupMenu.SignalName.IdPressed, Callable.From<long>(_OnToolsMenuItemPressed))) {
                    // Optional: toolsMenuPopup.Disconnect(PopupMenu.SignalName.IdPressed, Callable.From<long>(_OnToolsMenuItemPressed));
                }
                toolsMenuPopup.IdPressed += _OnToolsMenuItemPressed;
            }


            int toolsMenuIndex = topMenuBar.GetMenuCount();
            topMenuBar.AddChild(toolsMenuPopup);
            topMenuBar.SetMenuTitle(toolsMenuIndex, "Tools");
            topMenuBar.SetSubmenuPopup(toolsMenuIndex, toolsMenuPopup.GetPath());
        }
		// Error for topMenuBar null already handled by prior menu setups
	}

	public override async void _Notification(int what) // Make async
	{
		if (what == NotificationWMCloseRequest)
		{
			GD.Print("MainScene: WMCloseRequest received, calling _RequestQuitApplication.");
			_RequestQuitApplication();
			GetViewport().SetInputAsHandled(); // Important to prevent default close if dialog is up
		}
	}

	public void SetMapDirty(bool newDirtyState) {
		if (IsMapDirty == newDirtyState) return;
		IsMapDirty = newDirtyState;
		UpdateWindowTitle();
		EmitSignal(nameof(MapDirtyStateChanged), IsMapDirty);
	}

	public void SetCurrentMapFilePath(string path) {
		currentMapFilePath = path;
		UpdateWindowTitle();
		EmitSignal(nameof(CurrentMapPathChanged), currentMapFilePath ?? "Untitled");
		SetMapDirty(false);
	}
	public string GetCurrentMapFilePath() { return currentMapFilePath; }

	private bool _SaveMap(string path) // Return bool for success
	{
		if (tileDrawer == null) {
			GD.PrintErr("SaveMap: TileDrawer not found.");
			return false;
		}
		GD.Print($"Saving map to: {path}");
		MapSaverLoader.SaveMap(tileDrawer, path); // Assuming static method
		SetCurrentMapFilePath(path); // This sets path and calls SetMapDirty(false)
		GD.Print("Map saved successfully.");
		return true;
	}

	private Task<string> _SaveMapAsAsync()
	{
		saveAsTcs = new TaskCompletionSource<string>();
		saveFileDialog.PopupCentered(); // Show the Save As dialog
		return saveAsTcs.Task;
	}

	private void UpdateWindowTitle() {
		string title = applicationName;
		string fileName = string.IsNullOrEmpty(currentMapFilePath) ? "Untitled" : Path.GetFileName(currentMapFilePath);
		title += $" - {fileName}";
		if (IsMapDirty) title += "*";
		DisplayServer.WindowSetTitle(title);
	}

	private async void _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult result) // Make async
    {
        unsavedChangesDialogNode.Hide();
        UnsavedChangesDialogResult finalResult = result; // Store the button pressed

        if (result == UnsavedChangesDialogResult.Save)
        {
            bool saveSuccess;
            if (string.IsNullOrEmpty(currentMapFilePath))
            {
                string chosenPath = await _SaveMapAsAsync(); // Await path from FileDialog
                saveSuccess = (chosenPath != null);
                // _SaveMap is called by _OnSaveFileDialogFileSelected if path chosen
            }
            else
            {
                saveSuccess = _SaveMap(currentMapFilePath);
            }

            if (!saveSuccess)
            {
                // If save failed or was cancelled by user from FileDialog, treat as "Cancel" overall
                finalResult = UnsavedChangesDialogResult.Cancel;
            }
        }
        // For DontSave or Cancel, finalResult is already correct.

        dialogTcs?.TrySetResult(finalResult); // Resolve the original TCS from ShowUnsavedChangesDialog
    }

    public async Task<UnsavedChangesDialogResult> ShowUnsavedChangesDialog(string actionContextDescription = "proceeding")
    {
        if (!IsMapDirty)
        {
            return UnsavedChangesDialogResult.DontSave; // No unsaved changes, so proceed as if "Don't Save" was chosen
        }

        if (unsavedChangesDialogNode == null || unsavedDialogMessageLabel == null) {
            GD.PrintErr("Unsaved changes dialog not properly initialized.");
            return UnsavedChangesDialogResult.Cancel; // Fail safe
        }

        dialogTcs = new TaskCompletionSource<UnsavedChangesDialogResult>();

        string message = $"You have unsaved changes. Do you want to save before {actionContextDescription}?";
        if (string.IsNullOrEmpty(actionContextDescription)) {
            message = "You have unsaved changes. What would you like to do?";
        }
        unsavedDialogMessageLabel.Text = message;

        // Recenter dialog before showing
        unsavedChangesDialogNode.Position = DisplayServer.WindowGetSize() / 2 - unsavedChangesDialogNode.MinSize / 2;
        unsavedChangesDialogNode.Popup(); // Use Popup() for modal behavior with Window node

        return await dialogTcs.Task;
    }

	private void _OnAssetsChanged() { PopulateToolbar(); SetMapDirty(true); }
	private void _OnUndoRedoHistoryChanged() { UpdateObjectPropertiesPanel(); RefreshLayerList(); SetMapDirty(true); }

	public void DeselectAllObjects(PlacedObject excludeFromDeselection = null) {
		List<PlacedObject> objectsToActuallyDeselect = new List<PlacedObject>(currentlySelectedObjects);
		bool selectionTrulyChanged = false;
		foreach (PlacedObject obj in objectsToActuallyDeselect) {
			if (obj == excludeFromDeselection) continue;
			if (GodotObject.IsInstanceValid(obj)) {
				Sprite2D sprite = obj.GetNode<Sprite2D>("ObjectSprite");
				if (sprite != null) {
					if (originalModulations.TryGetValue(obj, out Color originalColor)) { sprite.Modulate = originalColor; }
					else { sprite.Modulate = Colors.White; }
				}
			}
			currentlySelectedObjects.Remove(obj);
			originalModulations.Remove(obj);
			selectionTrulyChanged = true;
		}
		if (selectionTrulyChanged || (excludeFromDeselection == null && currentlySelectedObjects.Count == 0) ||
			(currentlySelectedObjects.Count == 0 && excludeFromDeselection != null) ||
			(currentlySelectedObjects.Count == 1 && excludeFromDeselection != null && currentlySelectedObjects[0] == excludeFromDeselection)) {
			UpdateObjectPropertiesPanel();
		}
	}

	private void UpdateObjectPropertiesPanel() {
		// Check against new LineEdit properties
		if (objectPropertiesPanel == null || assetNameLabelInProps == null || rotationLineEditProp == null ||
			scaleXLineEditProp == null || scaleYLineEditProp == null || deleteObjectButtonFromPanelProp == null) {
			if(objectPropertiesPanel != null) objectPropertiesPanel.Visible = false;
			if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance)) rotationGizmoInstance.UpdateGizmoVisuals(null);
			if (scaleGizmoInstance != null && GodotObject.IsInstanceValid(scaleGizmoInstance)) scaleGizmoInstance.Hide();
			if (combinedTransformGizmoInstance != null && GodotObject.IsInstanceValid(combinedTransformGizmoInstance)) combinedTransformGizmoInstance.HideGizmo();
			return;
		}
		isUpdatingPanelFromSelection = true;
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			PlacedObject selected = currentlySelectedObjects[0];
			assetNameLabelInProps.Text = selected.AssetNameRef;

			// TODO: Add Position LineEdits and update them here
			// if (positionXLineEditProp != null) positionXLineEditProp.Text = selected.GlobalPosition.X.ToString("F2");
			// if (positionYLineEditProp != null) positionYLineEditProp.Text = selected.GlobalPosition.Y.ToString("F2");

			if (rotationLineEditProp != null) rotationLineEditProp.Text = selected.CurrentRotationDegrees.ToString("F2");
			if (scaleXLineEditProp != null) scaleXLineEditProp.Text = selected.CurrentScale.X.ToString("F2");
			if (scaleYLineEditProp != null) scaleYLineEditProp.Text = selected.CurrentScale.Y.ToString("F2");

			if (deleteObjectButtonFromPanelProp != null) deleteObjectButtonFromPanelProp.Disabled = false;
			// TODO: Add Position LineEdit Editable = true
			if (rotationLineEditProp != null) rotationLineEditProp.Editable = true;
			if (scaleXLineEditProp != null) scaleXLineEditProp.Editable = true;
			if (scaleYLineEditProp != null) scaleYLineEditProp.Editable = true;
			if (objectPropertiesPanel != null) objectPropertiesPanel.Visible = true;

			// For now, show all gizmos if one object is selected. Tool switching will refine this.
			if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance)) rotationGizmoInstance.UpdateGizmoVisuals(selected);
			if (scaleGizmoInstance != null && GodotObject.IsInstanceValid(scaleGizmoInstance)) scaleGizmoInstance.ShowForTarget(selected);
			if (combinedTransformGizmoInstance != null && GodotObject.IsInstanceValid(combinedTransformGizmoInstance)) combinedTransformGizmoInstance.ShowForTarget(selected);

		} else {
			if (currentlySelectedObjects.Count > 1) {
				assetNameLabelInProps.Text = $"[Multiple ({currentlySelectedObjects.Count}) Selected]";
				// TODO: Add Position LineEdits and set to "[Mixed]" or common value

				bool rotMixed = false, scaleXMixed = false, scaleYMixed = false;
				float firstRot = currentlySelectedObjects[0].CurrentRotationDegrees;
				float firstScaleX = currentlySelectedObjects[0].CurrentScale.X;
				float firstScaleY = currentlySelectedObjects[0].CurrentScale.Y;

				for (int i = 1; i < currentlySelectedObjects.Count; i++) {
					if (!Mathf.IsEqualApprox(currentlySelectedObjects[i].CurrentRotationDegrees, firstRot)) rotMixed = true;
					if (!Mathf.IsEqualApprox(currentlySelectedObjects[i].CurrentScale.X, firstScaleX)) scaleXMixed = true;
					if (!Mathf.IsEqualApprox(currentlySelectedObjects[i].CurrentScale.Y, firstScaleY)) scaleYMixed = true;
				}

				if (rotationLineEditProp != null) rotationLineEditProp.Text = rotMixed ? "[Mixed]" : firstRot.ToString("F2");
				if (scaleXLineEditProp != null) scaleXLineEditProp.Text = scaleXMixed ? "[Mixed]" : firstScaleX.ToString("F2");
				if (scaleYLineEditProp != null) scaleYLineEditProp.Text = scaleYMixed ? "[Mixed]" : firstScaleY.ToString("F2");

				if (deleteObjectButtonFromPanelProp != null) deleteObjectButtonFromPanelProp.Disabled = false;
				if (rotationLineEditProp != null) rotationLineEditProp.Editable = true;
				if (scaleXLineEditProp != null) scaleXLineEditProp.Editable = true;
				if (scaleYLineEditProp != null) scaleYLineEditProp.Editable = true;
				if (objectPropertiesPanel != null) objectPropertiesPanel.Visible = true;
				// TODO: Add Position LineEdit Editable = true (for batch translation if desired, or false)
			} else { // No objects selected
				assetNameLabelInProps.Text = "[No Object Selected]";
				// TODO: Add Position LineEdits and clear them
				if (rotationLineEditProp != null) rotationLineEditProp.Text = "";
				if (scaleXLineEditProp != null) scaleXLineEditProp.Text = "";
				if (scaleYLineEditProp != null) scaleYLineEditProp.Text = "";

				if (deleteObjectButtonFromPanelProp != null) deleteObjectButtonFromPanelProp.Disabled = true;
				// TODO: Add Position LineEdit Editable = false
				if (rotationLineEditProp != null) rotationLineEditProp.Editable = false;
				if (scaleXLineEditProp != null) scaleXLineEditProp.Editable = false;
				if (scaleYLineEditProp != null) scaleYLineEditProp.Editable = false;
				if (objectPropertiesPanel != null) objectPropertiesPanel.Visible = false;
			}
			if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance)) rotationGizmoInstance.UpdateGizmoVisuals(null);
			if (scaleGizmoInstance != null && GodotObject.IsInstanceValid(scaleGizmoInstance)) scaleGizmoInstance.Hide();
			if (combinedTransformGizmoInstance != null && GodotObject.IsInstanceValid(combinedTransformGizmoInstance)) combinedTransformGizmoInstance.HideGizmo();
		}

		// Alignment controls visibility
		if (alignmentSectionLabel != null) // Check if nodes are valid (were found in _Ready)
		{
			bool showAlignControls = (currentlySelectedObjects.Count > 1);
			alignmentSectionLabel.Visible = showAlignControls;
			if (alignContextHBox != null) alignContextHBox.Visible = showAlignControls; // Show/Hide AlignContextHBox
			alignmentButtonsHBox1.Visible = showAlignControls;
			alignmentButtonsHBox2.Visible = showAlignControls;

			if (!showAlignControls && alignContextOptionButton != null) {
                 alignContextOptionButton.Selected = (int)AlignContext.ToFirstSelected; // Reset to default if hiding
            }
		}

		// Distribution controls visibility
		if (distributionSectionLabel != null) // Check if nodes are valid
		{
			bool showDistributeControls = (currentlySelectedObjects.Count > 2); // Needs at least 3 objects
			distributionSectionLabel.Visible = showDistributeControls;
			distributionButtonsHBox.Visible = showDistributeControls;
		}
		isUpdatingPanelFromSelection = false;
	}

	private void _OnPropsRotationChanged(double newValue) { // This method is now effectively dead, will be replaced by TextSubmitted
		if (isUpdatingPanelFromSelection || undoRedoManagerInstance == null || tileDrawer == null || currentlySelectedObjects.Count == 0) return;
		float newRotationDeg = (float)newValue; List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects) {
			if (GodotObject.IsInstanceValid(obj)) {
				float oldRotationDeg = obj.CurrentRotationDegrees;
				batchActions.Add(new RotateObjectAction(obj, oldRotationDeg, newRotationDeg));
			}
		}
		if (batchActions.Count > 0) {
			CompositeEditorAction ca = new CompositeEditorAction(batchActions, $"Batch Rotate {batchActions.Count} Objs");
			ca.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(ca); SetMapDirty(true);
		}
	}

	private void _OnPropsScaleXChanged(double newValue) { // This method is now effectively dead
		if (isUpdatingPanelFromSelection || undoRedoManagerInstance == null || tileDrawer == null || currentlySelectedObjects.Count == 0) return;
		float newScaleX = (float)newValue; if (newScaleX < 0.01f) newScaleX = 0.01f;
		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects) {
			if (GodotObject.IsInstanceValid(obj)) {
				Vector2 oldScale = obj.CurrentScale; Vector2 newScale = new Vector2(newScaleX, oldScale.Y);
                if (!Mathf.IsEqualApprox(oldScale.X, newScale.X) || true) {
				    batchActions.Add(new ScaleObjectAction(obj, oldScale, newScale));
                }
			}
		}
		if (batchActions.Count > 0) {
			CompositeEditorAction ca = new CompositeEditorAction(batchActions, $"Batch ScaleX {batchActions.Count} Objs");
			ca.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(ca); SetMapDirty(true);
		}
	}

	private void _OnPropsScaleYChanged(double newValue) { // This method is now effectively dead
		if (isUpdatingPanelFromSelection || undoRedoManagerInstance == null || tileDrawer == null || currentlySelectedObjects.Count == 0) return;
		float newScaleY = (float)newValue; if (newScaleY < 0.01f) newScaleY = 0.01f;
		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects) {
			if (GodotObject.IsInstanceValid(obj)) {
				Vector2 oldScale = obj.CurrentScale; Vector2 newScale = new Vector2(oldScale.X, newScaleY);
                if (!Mathf.IsEqualApprox(oldScale.Y, newScale.Y) || true) {
				    batchActions.Add(new ScaleObjectAction(obj, oldScale, newScale));
                }
			}
		}
		if (batchActions.Count > 0) {
			CompositeEditorAction ca = new CompositeEditorAction(batchActions, $"Batch ScaleY {batchActions.Count} Objs");
			ca.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(ca); SetMapDirty(true);
		}
	}

	private void _OnPropsDeleteObjectPressed() { if (!isUpdatingPanelFromSelection) _HandleDeleteSelectedObjects(); }
	private void _HandleDeleteSelectedObjects() {
		if (currentlySelectedObjects.Count == 0) return;
		if (assetManager == null || undoRedoManagerInstance == null || tileDrawer == null) { GD.PrintErr("Delete deps null."); return; }
		Node placedObjectsRootNode = GetNode("PlacedObjectsRoot");
		if (!(placedObjectsRootNode is Node2D objectsRootParent)) { GD.PrintErr("PlacedObjectsRoot not found/Node2D."); return; }
		List<PlacedObject> toDeleteCopy = new List<PlacedObject>(currentlySelectedObjects);
		DeselectAllObjects();
		DeleteMultipleObjectsAction action = new DeleteMultipleObjectsAction(
			toDeleteCopy, objectsRootParent.GetPath(), assetManager, TileDrawer.PlacedObjectScenePath );
		action.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true);
	}

	private void _OnGizmoRotationStarted(float initialRotationDegrees) {
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			gizmoDragStartRotationForUndo = initialRotationDegrees;
		}
	}
	private void _OnGizmoRotationUpdated(float newRotationDegrees) {
		if (isUpdatingPanelFromSelection) return;
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]) && rotationLineEditProp != null) { // Check LineEdit
			isUpdatingPanelFromSelection = true;
			rotationLineEditProp.Text = newRotationDegrees.ToString("F2"); // Update LineEdit
			isUpdatingPanelFromSelection = false;
		}
	}
	private void _OnGizmoRotationFinished(float finalRotationDegrees, float originalRotationDegreesAtDragStart) { // Remains same
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			PlacedObject activeObject = currentlySelectedObjects[0];
			activeObject.CurrentRotationDegrees = finalRotationDegrees;
			if (!Mathf.IsEqualApprox(originalRotationDegreesAtDragStart, finalRotationDegrees)) {
				if (undoRedoManagerInstance != null && tileDrawer != null) {
					RotateObjectAction action = new RotateObjectAction(activeObject, originalRotationDegreesAtDragStart, finalRotationDegrees);
					undoRedoManagerInstance.RecordAction(action); SetMapDirty(true);
				}
			}
			UpdateObjectPropertiesPanel();
		}
	}

	private void _OnGizmoScaleStarted(Vector2 initialScale, ScaleGizmo.HandleType handleType) {
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			gizmoDragStartScaleForUndo = initialScale;
		}
	}
	private void _OnGizmoScaleUpdated(Vector2 newScale) {
		if (isUpdatingPanelFromSelection) return;
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			if (objectPropertiesPanel != null && objectPropertiesPanel.Visible && scaleXLineEditProp != null && scaleYLineEditProp != null) { // Check LineEdits
				isUpdatingPanelFromSelection = true;
				scaleXLineEditProp.Text = newScale.X.ToString("F2"); // Update LineEdits
				scaleYLineEditProp.Text = newScale.Y.ToString("F2");
				isUpdatingPanelFromSelection = false;
			}
		}
	}
	private void _OnGizmoScaleFinished(Vector2 finalScale, Vector2 originalScaleOnDragStart) { // Remains same
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			PlacedObject activeObject = currentlySelectedObjects[0];
			activeObject.CurrentScale = finalScale;
			if (!gizmoDragStartScaleForUndo.IsEqualApprox(finalScale)) {
				if (undoRedoManagerInstance != null && tileDrawer != null) {
					ScaleObjectAction action = new ScaleObjectAction(activeObject, gizmoDragStartScaleForUndo, finalScale);
					undoRedoManagerInstance.RecordAction(action); SetMapDirty(true);
				}
			}
			UpdateObjectPropertiesPanel();
		}
	}

	public override void _UnhandledInput(InputEvent @event) {
        // Prioritize gizmo input if any gizmo is active and wants input
        if (combinedTransformGizmoInstance != null && combinedTransformGizmoInstance.Visible && combinedTransformGizmoInstance.IsInputCapturedByGizmo()) { GetViewport().SetInputAsHandled(); return; }
        if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsDragging()) { GetViewport().SetInputAsHandled(); return; } // Assuming IsDragging implies input capture
        if (scaleGizmoInstance != null && scaleGizmoInstance.Visible && scaleGizmoInstance.IsDragging()) { GetViewport().SetInputAsHandled(); return; } // Assuming IsDragging implies input capture

		if (GetViewport().GuiGetFocusOwner() != null && !(GetViewport().GuiGetFocusOwner() is Button)) return;

		if (currentDrawingMode == DrawingMode.SelectObject) {
            // These specific checks might be redundant if the above IsInputCapturedByGizmo() is comprehensive
            // if (scaleGizmoInstance != null && scaleGizmoInstance.Visible && scaleGizmoInstance.IsDragging() && @event is InputEventMouseMotion) {}
            // else if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsDragging() && @event is InputEventMouseMotion) {}
			if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left) {
                if (GetViewport().IsInputHandled()) return;
				if (mouseButtonEvent.Pressed) {
                    // Let gizmos handle their own input first if mouse is over them
					if (combinedTransformGizmoInstance != null && combinedTransformGizmoInstance.Visible && combinedTransformGizmoInstance.IsMouseOverAnyHandle()) { return; } // Expecting CombinedGizmo to have IsMouseOverAnyHandle
					if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsMouseOverHandle()) { return; }
                    if (scaleGizmoInstance != null && scaleGizmoInstance.Visible && scaleGizmoInstance.IsMouseOverAnyHandle()) { return; }

                    Vector2 mousePos = GetGlobalMousePosition();
					PhysicsDirectSpaceState2D spaceState = GetTree().Root.World2D.DirectSpaceState;
					var queryParams = new PhysicsPointQueryParameters2D { Position = mousePos, CollideWithAreas = true, CollideWithBodies = false, CollisionMask = 1 };
					var results = spaceState.IntersectPoint(queryParams, 1);
					PlacedObject newlyClickedObject = null;
					if (results.Count > 0) { if (results[0]["collider"].Obj is Area2D area && area.GetParent() is PlacedObject po) newlyClickedObject = po; }
					bool isShiftPressed = Input.IsKeyPressed(Key.Shift) || Input.IsKeyPressed(Key.CmdOrCtrl);
					if (newlyClickedObject != null) {
						if (isShiftPressed) {
							if (currentlySelectedObjects.Contains(newlyClickedObject)) {
								if (GodotObject.IsInstanceValid(newlyClickedObject)) {
									Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite");
									if (sprite != null && originalModulations.TryGetValue(newlyClickedObject, out Color originalColor)) sprite.Modulate = originalColor;
									else if (sprite != null) sprite.Modulate = Colors.White;
								}
								currentlySelectedObjects.Remove(newlyClickedObject); originalModulations.Remove(newlyClickedObject);
							} else {
								currentlySelectedObjects.Add(newlyClickedObject);
								Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite");
								if (sprite != null) {
									if (!originalModulations.ContainsKey(newlyClickedObject)) originalModulations[newlyClickedObject] = sprite.Modulate;
									sprite.Modulate = new Color(0.7f, 0.7f, 1.0f, 0.8f);
								}
							}
						} else {
							if (!currentlySelectedObjects.Contains(newlyClickedObject) || currentlySelectedObjects.Count > 1) {
								DeselectAllObjects(excludeFromDeselection: newlyClickedObject);
								if (!currentlySelectedObjects.Contains(newlyClickedObject)) currentlySelectedObjects.Add(newlyClickedObject);
								Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite");
								if (sprite != null) {
									originalModulations[newlyClickedObject] = sprite.Modulate;
									sprite.Modulate = new Color(0.7f, 0.7f, 1.0f, 0.8f);
								}
							} else if (currentlySelectedObjects.Count == 1 && currentlySelectedObjects[0] != newlyClickedObject) {
                                DeselectAllObjects(); currentlySelectedObjects.Add(newlyClickedObject);
                                Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite");
                                if (sprite != null) {
                                    originalModulations[newlyClickedObject] = sprite.Modulate;
                                    sprite.Modulate = new Color(0.7f, 0.7f, 1.0f, 0.8f);
                                }
                            }
						}
						if (currentlySelectedObjects.Count == 1) {
							isDraggingObject = true; dragStartMousePosition = mousePos;
							dragStartObjectOriginalPosition = currentlySelectedObjects[0].GlobalPosition;
						} else { isDraggingObject = false; }
					} else {
						if (!isShiftPressed) DeselectAllObjects();
						isDraggingObject = false;
					}
					UpdateObjectPropertiesPanel(); GetViewport().SetInputAsHandled();
				} else { // MouseButton.Left Released
					if (isDraggingObject && currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
						PlacedObject draggedObject = currentlySelectedObjects[0];
						Vector2 finalPos = draggedObject.GlobalPosition;
						if (!dragStartObjectOriginalPosition.IsEqualApprox(finalPos)) {
							draggedObject.GlobalPosition = dragStartObjectOriginalPosition;
							MoveObjectAction action = new MoveObjectAction(draggedObject, dragStartObjectOriginalPosition, finalPos);
							action.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true);
							UpdateObjectPropertiesPanel();
						}
					}
					isDraggingObject = false;
                    if (isDraggingObject && mouseButtonEvent.Pressed == false && currentlySelectedObjects.Count ==1 ) { GetViewport().SetInputAsHandled(); }
				}
			} else if (@event is InputEventMouseMotion mouseMotionEvent && isDraggingObject) { // Object dragging
				if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
					PlacedObject draggedObject = currentlySelectedObjects[0];
					Vector2 mouseDelta = GetGlobalMousePosition() - dragStartMousePosition;
					Vector2 newPotentialPos = dragStartObjectOriginalPosition + mouseDelta; // Raw desired position based on drag

					Vector2 finalVisualPos = newPotentialPos; // Start with the raw drag position

					// Ensure globalSettings is available
					if (globalSettings == null) {
						GD.PrintErr("MainScene Drag: GlobalSettings not available! Snapping will be skipped.");
						finalVisualPos = newPotentialPos; // No snapping if settings are missing
					} else {
						// Prepare arguments for the new ApplySnappingRules signature
						Sprite2D sprite = draggedObject.GetNodeOrNull<Sprite2D>("ObjectSprite");
						PlacedObject.GlobalSnapPoints hypotheticalSnapPoints;
						if (sprite != null && sprite.Texture != null) {
							Vector2 textureSize = sprite.Texture.GetSize();
							float scaledHalfWidth = (textureSize.X * draggedObject.CurrentScale.X) / 2.0f;
							float scaledHalfHeight = (textureSize.Y * draggedObject.CurrentScale.Y) / 2.0f;
							hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints {
								Center = newPotentialPos,
								LeftX = newPotentialPos.X - scaledHalfWidth, RightX = newPotentialPos.X + scaledHalfWidth,
								TopY = newPotentialPos.Y - scaledHalfHeight, BottomY = newPotentialPos.Y + scaledHalfHeight
							};
						} else {
							hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints {
								Center = newPotentialPos, LeftX = newPotentialPos.X, RightX = newPotentialPos.X,
								TopY = newPotentialPos.Y, BottomY = newPotentialPos.Y
							};
						}

						List<PlacedObject> allObjectsInScene = new List<PlacedObject>();
						Node placedObjectsRootNode = GetNodeOrNull("PlacedObjectsRoot");
						if (placedObjectsRootNode != null) {
							foreach (Node child in placedObjectsRootNode.GetChildren()) {
								if (child is PlacedObject po) {
									allObjectsInScene.Add(po);
								}
							}
						}

						// Call the new ApplySnappingRules with all out parameters
						finalVisualPos = ApplySnappingRules(
							newPotentialPos,
							hypotheticalSnapPoints,
							draggedObject, // objectBeingMovedOrNull
							allObjectsInScene,
							out bool xObjSnapped, out float xObjSnapLine,
							out bool yObjSnapped, out float yObjSnapLine,
							out bool xGridSn,   out float xGridLine,  // Corrected from xGridSnapped, xGridSnapLineCoord
							out bool yGridSn,   out float yGridLine   // Corrected from yGridSnapped, yGridSnapLineCoord
						);

						// Re-integrate snap line display
						if (snapFeedbackDisplayInstance != null) {
							snapFeedbackDisplayInstance.ClearLines(); // Clear previous lines
							Rect2 vpRect = GetViewportRect(); // Assuming ViewportRect gives global coordinates

							if (xObjSnapped) {
								snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xObjSnapLine, vpRect.Position.Y), new Vector2(xObjSnapLine, vpRect.End.Y), Colors.Aqua);
							}
							if (yObjSnapped) {
								snapFeedbackDisplayInstance.AddSnapLine(new Vector2(vpRect.Position.X, yObjSnapLine), new Vector2(vpRect.End.X, yObjSnapLine), Colors.Aqua);
							}
							// Show grid snap lines if they occurred AND object snap did not occur on that axis,
							// or if we want to show both (grid lines could be different color/style).
							// For simplicity, show grid lines if they snapped, potentially overlaying/replacing object snap lines if at same coord.
							if (xGridSn) {
								snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xGridLine, vpRect.Position.Y), new Vector2(xGridLine, vpRect.End.Y), Colors.LightGreen); // Different color for grid
							}
							if (yGridSn) {
								snapFeedbackDisplayInstance.AddSnapLine(new Vector2(vpRect.Position.X, yGridLine), new Vector2(vpRect.End.X, yGridLine), Colors.LightGreen);
							}
						}
					}
					// --- End Snapping Logic ---

					draggedObject.GlobalPosition = finalVisualPos; // Update live visual position

					// Update properties panel if it shows live position
					// isUpdatingPanelFromSelectionCounter is not used here, using existing isUpdatingPanelFromSelection flag
					if (!isUpdatingPanelFromSelection) {
						 UpdateObjectPropertiesPanel();
					}
					GetViewport().SetInputAsHandled();
				}
			} else if ((@event.IsActionPressed("delete_object_action") ||
					 (@event is InputEventKey keyDel && keyDel.Keycode == Key.Delete && keyDel.Pressed && !keyDel.IsEcho()))
					 && currentlySelectedObjects.Count > 0 ) {
				if (GetViewport().GuiGetFocusOwner() != null && !(GetViewport().GuiGetFocusOwner() is Button)) return;
				_HandleDeleteSelectedObjects(); GetViewport().SetInputAsHandled();
			}
		}
	}

	private void SetupFileDialogs() {
		saveFileDialog = new FileDialog(); saveFileDialog.Title = "Save Map"; saveFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
		saveFileDialog.AddFilter("*.map ; Map Files"); // Ensure your extension is correct, e.g. "*.map" or "*.json"
		saveFileDialog.FileSelected += _OnSaveFileDialogFileSelected;
		saveFileDialog.Canceled += _OnSaveFileDialogCancelled;
		AddChild(saveFileDialog);

		loadFileDialog = new FileDialog(); loadFileDialog.Title = "Load Map"; loadFileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
		loadFileDialog.AddFilter("*.map ; Map Files");  // Ensure your extension is correct
		loadFileDialog.FileSelected += OnLoadFileDialogFileSelected; AddChild(loadFileDialog);
	}

	private void _OnSaveFileDialogCancelled()
	{
		saveAsTcs?.TrySetResult(null); // Complete with null if cancelled
	}

	private void PopulateToolbar() {
		foreach (Node child in toolbarPanel.GetChildren()) { toolbarPanel.RemoveChild(child); child.QueueFree(); }
		manageAssetsButton = new Button(); manageAssetsButton.Name = "ManageAssetsButton"; manageAssetsButton.Text = "Manage Assets";
		manageAssetsButton.Pressed += _OnManageAssetsButtonPressed; toolbarPanel.AddChild(manageAssetsButton);
		scatterModeCheckBox = new CheckBox(); scatterModeCheckBox.Name = "ScatterModeCheckBox"; scatterModeCheckBox.Text = "Scatter (Rand Rot)";
		scatterModeCheckBox.ButtonPressed = false; toolbarPanel.AddChild(scatterModeCheckBox);
		Button selectObjectToolButton = new Button(); selectObjectToolButton.Name = "SelectObjectToolButton"; selectObjectToolButton.Text = "Select Obj";
		selectObjectToolButton.Pressed += _OnSelectObjectToolButtonPressed; toolbarPanel.AddChild(selectObjectToolButton);
		eraserButton = new Button(); eraserButton.Name = "EraserButton"; eraserButton.Text = "Eraser";
		if (assetManager != null) {
			AssetData eraserAssetForIcon = assetManager.GetAsset("Eraser");
			if (eraserAssetForIcon != null && eraserAssetForIcon.PreviewTexture != null) eraserButton.Icon = eraserAssetForIcon.PreviewTexture;
		}
		eraserButton.Pressed += _OnEraserButtonPressed; toolbarPanel.AddChild(eraserButton);
		undoButton = new Button(); undoButton.Name = "UndoButton"; undoButton.Text = "Undo"; undoButton.Disabled = true;
		undoButton.Pressed += _OnUndoButtonPressed; toolbarPanel.AddChild(undoButton);
		redoButton = new Button(); redoButton.Name = "RedoButton"; redoButton.Text = "Redo"; redoButton.Disabled = true;
		redoButton.Pressed += _OnRedoButtonPressed; toolbarPanel.AddChild(redoButton);

		Button newMapButton = new Button(); newMapButton.Text = "New Map"; newMapButton.Pressed += _OnNewMapButtonPressed; toolbarPanel.AddChild(newMapButton);
		Button saveButton = new Button(); saveButton.Text = "Save Map"; saveButton.Pressed += _OnSaveMapButtonPressed; toolbarPanel.AddChild(saveButton);
		Button loadButton = new Button(); loadButton.Text = "Load Map"; loadButton.Pressed += _OnLoadMapButtonPressed; toolbarPanel.AddChild(loadButton);

		Button appSettingsButton = new Button();
		appSettingsButton.Name = "AppSettingsButton";
		appSettingsButton.Text = "App Settings";
		appSettingsButton.Pressed += _OnAppSettingsButtonPressed;
		toolbarPanel.AddChild(appSettingsButton);

		Button startRoomModeButton = new Button(); startRoomModeButton.Text = "Start Room"; startRoomModeButton.Pressed += OnStartRoomModePressed; toolbarPanel.AddChild(startRoomModeButton);
		Button tileModeButton = new Button(); tileModeButton.Text = "Back to Tile Mode"; tileModeButton.Pressed += OnTileModePressed; toolbarPanel.AddChild(tileModeButton);
		if (assetManager != null) {
			List<AssetData> assets = assetManager.GetAllAssets();
			if (assets != null && assets.Count > 0) {
				foreach (var asset in assets) {
					Button button = new Button(); button.Name = "AssetButton_" + asset.Name; button.Text = asset.Name;
					if (asset.PreviewTexture != null) button.Icon = asset.PreviewTexture;
					button.Pressed += () => OnToolbarButtonPressed(asset.Name); toolbarPanel.AddChild(button);
				}
			}
		} else { GD.PrintErr("AssetManager null in PopulateToolbar."); }
	}
	private void _OnManageAssetsButtonPressed() { if (assetLibraryPanelInstance != null) assetLibraryPanelInstance.Visible = !assetLibraryPanelInstance.Visible; else GD.PrintErr("AssetLibraryPanelInstance null."); }
	private void _OnUndoButtonPressed() { if (undoRedoManagerInstance != null) undoRedoManagerInstance.Undo(); SetMapDirty(true); }
	private void _OnRedoButtonPressed() { if (undoRedoManagerInstance != null) undoRedoManagerInstance.Redo(); SetMapDirty(true); }

	private void _OnUndoStackChanged(bool canUndo)
	{
		if (undoButton != null) undoButton.Disabled = !canUndo;
		if (editMenuPopup != null && undoMenuItemIndex != -1)
        {
            editMenuPopup.SetItemDisabled(undoMenuItemIndex, !canUndo);
        }
	}
	private void _OnRedoStackChanged(bool canRedo)
	{
		if (redoButton != null) redoButton.Disabled = !canRedo;
		if (editMenuPopup != null && redoMenuItemIndex != -1)
        {
            editMenuPopup.SetItemDisabled(redoMenuItemIndex, !canRedo);
        }
	}
	private void _OnSelectObjectToolButtonPressed() { ChangeDrawingMode(DrawingMode.SelectObject); }
	private void _OnEraserButtonPressed() {
		if (assetManager == null || tileDrawer == null) { GD.PrintErr("Eraser dependencies null."); return; }
		AssetData eraserAsset = assetManager.GetAsset("Eraser");
		if (eraserAsset != null) {
			tileDrawer.SetCurrentAsset(eraserAsset);
			if (currentDrawingMode != DrawingMode.Tile) ChangeDrawingMode(DrawingMode.Tile);
		} else { GD.PrintErr("Eraser asset not found!"); }
	}
	private void OnStartRoomModePressed() { ChangeDrawingMode(DrawingMode.Room); }
	private void OnTileModePressed() { ChangeDrawingMode(DrawingMode.Tile); }
	private void OnToolbarButtonPressed(string assetName) {
		AssetData asset = assetManager.GetAsset(assetName);
		if (asset != null && tileDrawer != null) tileDrawer.SetCurrentAsset(asset);
		else { if (asset == null) GD.PrintErr($"Asset '{assetName}' not found."); if (tileDrawer == null) GD.PrintErr("TileDrawer null."); }
	}

	private async void _OnSaveMapButtonPressed() // Make async if it might call Save As
	{
		if (string.IsNullOrEmpty(currentMapFilePath) || Input.IsKeyPressed(Key.Shift)) // Force Save As if no path or Shift held
		{
			string chosenPath = await _SaveMapAsAsync();
			if (chosenPath == null) {
				GD.Print("Save As was cancelled.");
			}
			// _SaveMap is called by _OnSaveFileDialogFileSelected if path chosen
		}
		else
		{
			_SaveMap(currentMapFilePath);
		}
	}

	private async void _OnNewMapButtonPressed() // Make async
	{
		UnsavedChangesDialogResult result = await ShowUnsavedChangesDialog("creating a new map");
		if (result == UnsavedChangesDialogResult.Cancel)
		{
			return; // User cancelled
		}

		GD.Print("Creating new map...");
		if(tileDrawer != null) tileDrawer.Clear(); // Clear all layers and tiles in the TileMap

		Node placedObjectsRoot = GetNodeOrNull("PlacedObjectsRoot");
		if (placedObjectsRoot != null) {
			foreach (Node child in placedObjectsRoot.GetChildren()) {
				child.QueueFree();
			}
		}

		if (undoRedoManagerInstance != null) {
			undoRedoManagerInstance.ClearHistory(); // Clear undo/redo stacks
		}

		SetCurrentMapFilePath(null); // No file path for new map
		SetMapDirty(false);          // New map is not dirty initially

		if (layersPanelController != null) layersPanelController.RefreshLayerList();
		UpdateObjectPropertiesPanel(); // Hide/reset object panel (already calls gizmo hide)

		GD.Print("New map created.");
	}

	private async void _OnLoadMapButtonPressed() // Make async. Renamed from OnLoadMapPressed
	{
		UnsavedChangesDialogResult result = await ShowUnsavedChangesDialog("loading another map");
		if (result == UnsavedChangesDialogResult.Cancel)
		{
			return; // User cancelled
		}
		// If Save succeeded or Don't Save was chosen, proceed.
		if (loadFileDialog != null) loadFileDialog.PopupCentered(); else GD.PrintErr("LoadFileDialog is null.");
	}

	private void _OnAppSettingsButtonPressed()
	{
		if (appSettingsPanelInstance != null)
		{
			if (appSettingsPanelInstance.Visible) {
				appSettingsPanelInstance.Hide();
			} else {
				appSettingsPanelInstance.Show(); // Controller's _OnVisibilityChanged handles loading current settings
			}
		}
	}

	private void _OnGlobalAssetPathChanged()
	{
		GD.Print("MainScene: Detected GlobalAssetPathChangedAndRefreshed. Refreshing relevant UI.");

		PopulateToolbar(); // Rebuilds asset buttons
		UpdateObjectPropertiesPanel(); // Refresh based on current selection and new assets
		if (layersPanelController != null) layersPanelController.RefreshLayerList();
	}

	private void _OnAlignmentButtonPressed(AlignmentMode mode)
	{
		if (currentlySelectedObjects.Count < 2)
		{
			GD.Print("Alignment requires at least two objects to be selected.");
			return;
		}

		if (undoRedoManagerInstance == null || tileDrawer == null)
		{
			GD.PrintErr("MainScene: UndoRedoManager or TileDrawer not available for AlignObjectsAction.");
			return;
		}

		List<PlacedObject> selectionCopy = new List<PlacedObject>(currentlySelectedObjects);

		AlignContext currentAlignContext = AlignContext.ToFirstSelected; // Default
		if (alignContextOptionButton != null) {
			currentAlignContext = (AlignContext)alignContextOptionButton.Selected;
		}

		AlignObjectsAction action = new AlignObjectsAction(selectionCopy, mode, currentAlignContext);

		if (action.IsEmpty())
		{
			GD.Print("Objects are already aligned in the selected mode. No action taken.");
			return;
		}

		action.Execute(tileDrawer);
		undoRedoManagerInstance.RecordAction(action);

		GD.Print($"Alignment action '{mode}' performed and recorded.");
		// UI updates (panel, gizmos) will be handled by HistoryChanged signal logic
	}

	private void _OnDistributeObjectsButtonPressed(DistributionMode mode)
	{
		if (currentlySelectedObjects.Count < 3)
		{
			GD.Print("Distribution requires at least three objects to be selected.");
			return;
		}

		if (undoRedoManagerInstance == null || tileDrawer == null)
		{
			GD.PrintErr("MainScene: UndoRedoManager or TileDrawer not available for DistributeObjectsAction.");
			return;
		}

		List<PlacedObject> selectionCopy = new List<PlacedObject>(currentlySelectedObjects);

		AlignContext currentAlignContext = AlignContext.ToFirstSelected; // Default
		if (alignContextOptionButton != null) { // Check if the OptionButton was successfully fetched in _Ready
			currentAlignContext = (AlignContext)alignContextOptionButton.Selected;
		} else {
			GD.PrintWarn("MainScene: alignContextOptionButton is null. Defaulting to AlignContext.ToFirstSelected.");
		}

		AlignObjectsAction action = new AlignObjectsAction(selectionCopy, mode, currentAlignContext);

		if (action.IsEmpty())
		{
			GD.Print("Objects are already distributed in the selected mode or not enough objects to distribute between extremes. No action taken.");
			return;
		}

		action.Execute(tileDrawer);
		undoRedoManagerInstance.RecordAction(action);

		GD.Print($"Distribution action '{mode}' performed and recorded.");
	}

	private void _OnSaveFileDialogFileSelected(string filePath) // This is connected to saveFileDialog.FileSelected
	{
		_SaveMap(filePath); // This sets currentMapFilePath and dirty = false
		saveAsTcs?.TrySetResult(filePath); // Complete the task for _SaveMapAsAsync
	}

	// Corrected line:
	private void OnLoadFileDialogFileSelected(string filePath) { MapSaverLoader.LoadMap(tileDrawer, filePath); RefreshLayerList(); UpdateObjectPropertiesPanel(); SetCurrentMapFilePath(filePath); SetMapDirty(false); if (undoRedoManagerInstance != null) undoRedoManagerInstance.ClearHistory(); DeselectAllObjects(); }
	public DrawingMode GetCurrentDrawingMode() { return currentDrawingMode; }
	public void ChangeDrawingMode(DrawingMode newMode) {
		if (currentDrawingMode == newMode) return;
		string oldModeName = currentDrawingMode.ToString();
		if (currentDrawingMode == DrawingMode.SelectObject && newMode != DrawingMode.SelectObject) DeselectAllObjects();
		currentDrawingMode = newMode;
		GD.Print($"MainScene: DrawingMode changed from {oldModeName} to {newMode}");
		EmitSignal(nameof(DrawingModeChanged), (long)currentDrawingMode);
	}
	private void RefreshLayerList() {
		var layersPanelCtrl = GetNode<LayersPanelController>("LayersPanel");
		layersPanelCtrl?.RefreshLayerList();
	}

	private Vector2 TrySnapPosition(
		Vector2 currentPotentialPivotPosition,
		PlacedObject.GlobalSnapPoints activeObjectSnapPointsAtPotentialPosition,
		List<PlacedObject> allOtherPlacedObjects,
		out bool xWasSnapped, out float xSnapLineGuideCoord,
		out bool yWasSnapped, out float ySnapLineGuideCoord)
	{
		xWasSnapped = false; xSnapLineGuideCoord = 0f;
		yWasSnapped = false; ySnapLineGuideCoord = 0f;

		if (globalSettings == null) {
			GD.PrintErr("TrySnapPosition: GlobalSettings not initialized!");
			return currentPotentialPivotPosition;
		}

		if (!globalSettings.IsObjectSnapEnabled || allOtherPlacedObjects == null || allOtherPlacedObjects.Count == 0)
		{
			return currentPotentialPivotPosition;
		}

		Vector2 finalSnappedPosition = currentPotentialPivotPosition;
		float bestSnapDeltaX = float.MaxValue;
		float bestSnapDeltaY = float.MaxValue;
		float snapDistanceThreshold = globalSettings.ObjectSnapDistanceWorld;

		// Store the coordinate of the line we snapped to, not just the delta
		float currentBestXTargetCoord = 0f;
		float currentBestYTargetCoord = 0f;

		foreach (PlacedObject otherObject in allOtherPlacedObjects)
		{
			if (otherObject == null || !GodotObject.IsInstanceValid(otherObject)) continue;

			PlacedObject.GlobalSnapPoints otherSnapPoints = otherObject.GetCurrentGlobalSnapPoints();
			float currentDelta;

			// --- Check X-axis Snaps ---
			if (globalSettings.ObjectSnapToPivots)
			{
				currentDelta = otherSnapPoints.VerticalCenterX - activeObjectSnapPointsAtPotentialPosition.VerticalCenterX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.VerticalCenterX;}
			}
			if (globalSettings.ObjectSnapToEdges)
			{
				currentDelta = otherSnapPoints.LeftX - activeObjectSnapPointsAtPotentialPosition.LeftX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.LeftX;}
				currentDelta = otherSnapPoints.RightX - activeObjectSnapPointsAtPotentialPosition.LeftX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.RightX;}
				currentDelta = otherSnapPoints.LeftX - activeObjectSnapPointsAtPotentialPosition.RightX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.LeftX;}
				currentDelta = otherSnapPoints.RightX - activeObjectSnapPointsAtPotentialPosition.RightX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.RightX;}
				currentDelta = otherSnapPoints.LeftX - activeObjectSnapPointsAtPotentialPosition.VerticalCenterX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.LeftX;}
				currentDelta = otherSnapPoints.RightX - activeObjectSnapPointsAtPotentialPosition.VerticalCenterX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.RightX;}
				currentDelta = otherSnapPoints.VerticalCenterX - activeObjectSnapPointsAtPotentialPosition.LeftX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.VerticalCenterX;}
				currentDelta = otherSnapPoints.VerticalCenterX - activeObjectSnapPointsAtPotentialPosition.RightX;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.VerticalCenterX;}
			}

			// --- Check Y-axis Snaps ---
			if (globalSettings.ObjectSnapToPivots)
			{
				currentDelta = otherSnapPoints.HorizontalCenterY - activeObjectSnapPointsAtPotentialPosition.HorizontalCenterY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.HorizontalCenterY;}
			}
			if (globalSettings.ObjectSnapToEdges)
			{
				currentDelta = otherSnapPoints.TopY - activeObjectSnapPointsAtPotentialPosition.TopY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.TopY;}
				currentDelta = otherSnapPoints.BottomY - activeObjectSnapPointsAtPotentialPosition.TopY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.BottomY;}
				currentDelta = otherSnapPoints.TopY - activeObjectSnapPointsAtPotentialPosition.BottomY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.TopY;}
				currentDelta = otherSnapPoints.BottomY - activeObjectSnapPointsAtPotentialPosition.BottomY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.BottomY;}
				currentDelta = otherSnapPoints.TopY - activeObjectSnapPointsAtPotentialPosition.HorizontalCenterY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.TopY;}
				currentDelta = otherSnapPoints.BottomY - activeObjectSnapPointsAtPotentialPosition.HorizontalCenterY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.BottomY;}
				currentDelta = otherSnapPoints.HorizontalCenterY - activeObjectSnapPointsAtPotentialPosition.TopY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.HorizontalCenterY;}
				currentDelta = otherSnapPoints.HorizontalCenterY - activeObjectSnapPointsAtPotentialPosition.BottomY;
				if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.HorizontalCenterY;}
			}
		}

		if (Mathf.Abs(bestSnapDeltaX) < snapDistanceThreshold)
		{
			finalSnappedPosition.X += bestSnapDeltaX;
			xWasSnapped = true;
			xSnapLineGuideCoord = currentBestXTargetCoord; // Assign the stored target coordinate
		}
		if (Mathf.Abs(bestSnapDeltaY) < snapDistanceThreshold)
		{
			finalSnappedPosition.Y += bestSnapDeltaY;
			yWasSnapped = true;
			ySnapLineGuideCoord = currentBestYTargetCoord; // Assign the stored target coordinate
		}
		return finalSnappedPosition;
	}

	// Placeholder handlers for LineEdit TextSubmitted signals
	private void _OnRotationTextSubmitted(string newText) {
		if (isUpdatingPanelFromSelection) return;
		if (currentlySelectedObjects.Count == 0 || undoRedoManagerInstance == null || tileDrawer == null || rotationLineEditProp == null)
		{
			rotationLineEditProp?.ReleaseFocus();
			UpdateObjectPropertiesPanel(); // Revert text if invalid state for action
			return;
		}

		string inputText = newText.Trim();
		if (string.IsNullOrEmpty(inputText))
		{
			rotationLineEditProp.ReleaseFocus();
			UpdateObjectPropertiesPanel();
			return;
		}

		float absoluteValue = 0f;
		float deltaValue = 0f; // For relative changes
		bool isRelative = false;
		bool parseSuccess = false;

		if (inputText.StartsWith("+") || (inputText.StartsWith("-") && inputText.Length > 1) ) // Explicit sign for relative, e.g. +15, -10
		{
			isRelative = true;
			parseSuccess = float.TryParse(inputText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out deltaValue);
		}
		else // Absolute value
		{
			isRelative = false;
			parseSuccess = float.TryParse(inputText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out absoluteValue);
		}

		Label statusMessageLabel = objectPropertiesPanel?.GetNode<Label>("PropertiesVBox/StatusMessageLabel"); // Assuming this exists

		if (!parseSuccess)
		{
			if(statusMessageLabel != null) statusMessageLabel.Text = "Error: Invalid rotation input.";
			rotationLineEditProp.ReleaseFocus();
			CallDeferred(nameof(UpdateObjectPropertiesPanel));
			return;
		}

		if(statusMessageLabel != null) statusMessageLabel.Text = ""; // Clear error

		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects)
		{
			if (GodotObject.IsInstanceValid(obj))
			{
				float oldRot = obj.CurrentRotationDegrees;
				float newRot = oldRot;

				if (isRelative)
				{
					newRot = oldRot + deltaValue;
				}
				else
				{
					newRot = absoluteValue;
				}
				// Optional: Normalize newRot, e.g., newRot = Mathf.Wrap(newRot, -180f, 180f);

				if (!Mathf.IsEqualApprox(oldRot, newRot))
				{
					batchActions.Add(new RotateObjectAction(obj, oldRot, newRot));
				}
			}
		}

		if (batchActions.Count > 0)
		{
			CompositeEditorAction compositeAction = new CompositeEditorAction(batchActions, "Batch Change Rotation from Panel");
			compositeAction.Execute(tileDrawer);
			undoRedoManagerInstance.RecordAction(compositeAction);
		}

		rotationLineEditProp.ReleaseFocus();
		// UpdateObjectPropertiesPanel(); // Will be called by HistoryChanged if action recorded.
									 // If no action, text remains as user typed (if valid but no-op) or reverted by CallDeferred.
	}

	private void _OnScaleXTextSubmitted(string newText) {
		if (isUpdatingPanelFromSelection) return;
		if (currentlySelectedObjects.Count == 0 || undoRedoManagerInstance == null || tileDrawer == null || scaleXLineEditProp == null)
		{
			scaleXLineEditProp?.ReleaseFocus();
			UpdateObjectPropertiesPanel(); // Revert
			return;
		}

		string inputText = newText.Trim();
		if (string.IsNullOrEmpty(inputText))
		{
			scaleXLineEditProp.ReleaseFocus();
			UpdateObjectPropertiesPanel();
			return;
		}

		float value = 0f;
		bool isRelativeMultiply = false;
		bool isRelativeDivide = false;
		bool isAbsolute = false;
		bool parseSuccess = false;
		Label statusMessageLabel = objectPropertiesPanel?.GetNode<Label>("PropertiesVBox/StatusMessageLabel");


		if (inputText.StartsWith("*"))
		{
			isRelativeMultiply = true;
			parseSuccess = float.TryParse(inputText.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
		}
		else if (inputText.StartsWith("/"))
		{
			isRelativeDivide = true;
			parseSuccess = float.TryParse(inputText.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
			if (parseSuccess && Mathf.IsZeroApprox(value))
			{
				if(statusMessageLabel != null) statusMessageLabel.Text = "Error: Cannot divide scale by zero.";
				parseSuccess = false;
			}
		}
		else
		{
			isAbsolute = true;
			parseSuccess = float.TryParse(inputText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
		}

		if (!parseSuccess)
		{
			if(statusMessageLabel != null) statusMessageLabel.Text = "Error: Invalid scale input.";
			scaleXLineEditProp.ReleaseFocus();
			CallDeferred(nameof(UpdateObjectPropertiesPanel));
			return;
		}

		if (value < 0.01f && (isAbsolute || isRelativeMultiply)) value = 0.01f;
		if(statusMessageLabel != null) statusMessageLabel.Text = "";

		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects)
		{
			if (GodotObject.IsInstanceValid(obj))
			{
				Vector2 oldScale = obj.CurrentScale;
				Vector2 newScale = oldScale;

				if (isRelativeMultiply) newScale.X = oldScale.X * value;
				else if (isRelativeDivide) newScale.X = oldScale.X / value;
				else newScale.X = value;

				if (newScale.X < 0.01f) newScale.X = 0.01f;

				if (!oldScale.IsEqualApprox(newScale))
				{
					batchActions.Add(new ScaleObjectAction(obj, oldScale, newScale));
				}
			}
		}

		if (batchActions.Count > 0)
		{
			CompositeEditorAction compositeAction = new CompositeEditorAction(batchActions, "Batch Change Scale X from Panel");
			compositeAction.Execute(tileDrawer);
			undoRedoManagerInstance.RecordAction(compositeAction);
		}
		scaleXLineEditProp.ReleaseFocus();
	}

	private void _OnScaleYTextSubmitted(string newText) {
		if (isUpdatingPanelFromSelection) return;
		if (currentlySelectedObjects.Count == 0 || undoRedoManagerInstance == null || tileDrawer == null || scaleYLineEditProp == null)
		{
			scaleYLineEditProp?.ReleaseFocus();
			UpdateObjectPropertiesPanel();
			return;
		}

		string inputText = newText.Trim();
		if (string.IsNullOrEmpty(inputText))
		{
			scaleYLineEditProp.ReleaseFocus();
			UpdateObjectPropertiesPanel();
			return;
		}

		float value = 0f;
		bool isRelativeMultiply = false;
		bool isRelativeDivide = false;
		bool isAbsolute = false;
		bool parseSuccess = false;
		Label statusMessageLabel = objectPropertiesPanel?.GetNode<Label>("PropertiesVBox/StatusMessageLabel");

		if (inputText.StartsWith("*"))
		{
			isRelativeMultiply = true;
			parseSuccess = float.TryParse(inputText.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
		}
		else if (inputText.StartsWith("/"))
		{
			isRelativeDivide = true;
			parseSuccess = float.TryParse(inputText.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
			if (parseSuccess && Mathf.IsZeroApprox(value))
			{
				if(statusMessageLabel != null) statusMessageLabel.Text = "Error: Cannot divide scale by zero.";
				parseSuccess = false;
			}
		}
		else
		{
			isAbsolute = true;
			parseSuccess = float.TryParse(inputText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
		}

		if (!parseSuccess)
		{
			if(statusMessageLabel != null) statusMessageLabel.Text = "Error: Invalid scale input.";
			scaleYLineEditProp.ReleaseFocus();
			CallDeferred(nameof(UpdateObjectPropertiesPanel));
			return;
		}

		if (value < 0.01f && (isAbsolute || isRelativeMultiply)) value = 0.01f;
		if(statusMessageLabel != null) statusMessageLabel.Text = "";

		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects)
		{
			if (GodotObject.IsInstanceValid(obj))
			{
				Vector2 oldScale = obj.CurrentScale;
				Vector2 newScale = oldScale;

				if (isRelativeMultiply) newScale.Y = oldScale.Y * value;
				else if (isRelativeDivide) newScale.Y = oldScale.Y / value;
				else newScale.Y = value;

				if (newScale.Y < 0.01f) newScale.Y = 0.01f;

				if (!oldScale.IsEqualApprox(newScale))
				{
					batchActions.Add(new ScaleObjectAction(obj, oldScale, newScale));
        }
    }

	private void _OnActualDrawingModeChanged(DrawingMode newModeEnumValue) // Changed param type to DrawingMode for direct use
    {
        if (toolsMenuPopup == null || toolMenuItemIndices == null) return;

        // DrawingMode newActiveMode = (DrawingMode)newModeEnumValue; // No longer needed if param is DrawingMode
        GD.Print($"MainScene: Actual drawing mode changed to {newModeEnumValue}. Updating Tools menu checkmarks.");

        foreach (KeyValuePair<DrawingMode, int> entry in toolMenuItemIndices)
        {
            DrawingMode modeForThisItem = entry.Key;
            int itemIndexInMenu = entry.Value;

            if (itemIndexInMenu >= 0 && itemIndexInMenu < toolsMenuPopup.ItemCount)
            {
                toolsMenuPopup.SetItemChecked(itemIndexInMenu, modeForThisItem == newModeEnumValue);
            }
				}
			}
		}

		if (batchActions.Count > 0)
		{
			CompositeEditorAction compositeAction = new CompositeEditorAction(batchActions, "Batch Change Scale Y from Panel");
			compositeAction.Execute(tileDrawer);
			undoRedoManagerInstance.RecordAction(compositeAction);
		}
		scaleYLineEditProp.ReleaseFocus();
	}

	public SnapFeedbackDisplay GetSnapFeedbackDisplay()
	{
		return snapFeedbackDisplayInstance;
	}

	// --- File Menu Action Handlers ---
	private void _OnFileMenuItemPressed(long id)
	{
		FileMenuItemId itemId = (FileMenuItemId)id;
		// GD.Print($"File menu item pressed: {itemId}"); // Optional: for debugging

		switch (itemId)
		{
			case FileMenuItemId.New:
				_OnNewMapButtonPressed();
				break;
			case FileMenuItemId.Open:
				_OnLoadMapButtonPressed();
				break;
			case FileMenuItemId.Save:
				_OnSaveMapButtonPressed();
				break;
			case FileMenuItemId.SaveAs:
				_OnSaveMapAsButtonPressed();
				break;
			case FileMenuItemId.AppSettings:
				_OnAppSettingsButtonPressed();
				break;
			case FileMenuItemId.Quit:
				_RequestQuitApplication();
				break;
		}
	}

	private async void _OnSaveMapAsButtonPressed()
	{
		string chosenPath = await _SaveMapAsAsync();
		if (chosenPath == null) {
			GD.Print("Save As (from menu) was cancelled.");
			// if (statusMessageLabel != null) statusMessageLabel.Text = "Save As cancelled."; // Status label might not be visible/relevant
		} else {
			// if (statusMessageLabel != null) statusMessageLabel.Text = $"Map saved to {System.IO.Path.GetFileName(chosenPath)}";
			GD.Print($"Map saved via 'Save As...' to {chosenPath}");
		}
	}

	private async void _RequestQuitApplication()
	{
		UnsavedChangesDialogResult result = await ShowUnsavedChangesDialog("quitting the application");
		if (result != UnsavedChangesDialogResult.Cancel)
		{
			GD.Print("Proceeding with application quit.");
			GetTree().Quit();
		}
		else
		{
			GD.Print("Application close cancelled by user.");
		}
	}

	// --- Edit Menu Action Handlers ---
	private void _OnEditMenuItemPressed(long id)
    {
        EditMenuItemId itemId = (EditMenuItemId)id;
        // GD.Print($"Edit menu item pressed: {itemId}"); // Optional: for debugging

        if (undoRedoManagerInstance == null) return;

        switch (itemId)
        {
            case EditMenuItemId.Undo:
                undoRedoManagerInstance.Undo();
                break;
            case EditMenuItemId.Redo:
                undoRedoManagerInstance.Redo();
                break;
        }
    }

	// --- View Menu Action Handlers ---
	private void _OnViewMenuItemPressed(long id)
	{
		ViewMenuItemId itemId = (ViewMenuItemId)id;
		// GD.Print($"View menu item pressed: {itemId}");

		if (globalSettings == null) return;

		switch (itemId)
		{
			case ViewMenuItemId.ToggleGridSnap:
				globalSettings.IsSnapToGridEnabled = !globalSettings.IsSnapToGridEnabled;
				// The new state will be reflected via _OnGridSettingsChangedForViewMenu
				break;
			case ViewMenuItemId.ToggleObjectSnap:
				globalSettings.IsObjectSnapEnabled = !globalSettings.IsObjectSnapEnabled;
				// The new state will be reflected via _OnObjectSnapSettingsChangedForViewMenu
				break;
		}
	}

	private void _OnGridSettingsChangedForViewMenu() // Connected to globalSettings.GridSettingsChanged
	{
		if (viewMenuPopup != null && toggleGridSnapMenuItemIndex != -1 && globalSettings != null)
		{
			viewMenuPopup.SetItemChecked(toggleGridSnapMenuItemIndex, globalSettings.IsSnapToGridEnabled);
		}
	}

	private void _OnObjectSnapSettingsChangedForViewMenu() // Connected to globalSettings.ObjectSnapSettingsChanged
	{
		if (viewMenuPopup != null && toggleObjectSnapMenuItemIndex != -1 && globalSettings != null)
		{
			viewMenuPopup.SetItemChecked(toggleObjectSnapMenuItemIndex, globalSettings.IsObjectSnapEnabled);
		}
	}

	// --- Tools Menu Setup and Handlers ---
	private string GetToolModeUserFriendlyName(DrawingMode mode)
    {
        switch (mode)
        {
            case DrawingMode.Tile: return "Tile Drawing";
            case DrawingMode.Room: return "Room Drawing";
            case DrawingMode.ObjectPlacement: return "Place Object";
            case DrawingMode.SelectObject: return "Select/Edit Object";
            default: return mode.ToString();
        }
    }

	// --- Tools Menu Action Handlers ---
	private void _OnToolsMenuItemPressed(long id)
    {
        DrawingMode selectedMode = (DrawingMode)id;
        // GD.Print($"Tools menu item pressed: {selectedMode}"); // Optional for debugging
        ChangeDrawingMode(selectedMode);
    }

    private Vector2 ApplySnappingRules(
        Vector2 potentialPivotPosition,                         // Current desired pivot position of the active object
        PlacedObject.GlobalSnapPoints activeObjHypotheticalSnapPoints, // Snap points of active object calculated AT potentialPivotPosition
        PlacedObject objectBeingMovedOrNull,                    // The actual PlacedObject node if one is being moved (to exclude from others)
        List<PlacedObject> allPlacedObjectsInScene,             // A list of ALL PlacedObjects currently in the scene
        out bool xObjectSnapped, out float xObjectSnapLineCoord,
        out bool yObjectSnapped, out float yObjectSnapLineCoord,
        out bool xGridSnapped,   out float xGridSnapLineCoord,
        out bool yGridSnapped,   out float yGridSnapLineCoord
    )
    {
        xObjectSnapped = false; xObjectSnapLineCoord = 0f;
        yObjectSnapped = false; yObjectSnapLineCoord = 0f;
        xGridSnapped = false;   xGridSnapLineCoord = 0f;
        yGridSnapped = false;   yGridSnapLineCoord = 0f;

        Vector2 positionAfterObjectSnap = potentialPivotPosition;

        // Ensure globalSettings is available
        if (globalSettings == null) {
            GD.PrintErr("ApplySnappingRules: GlobalSettings not available!");
            return potentialPivotPosition;
        }

        // --- 1. Object-to-Object Snapping ---
        if (globalSettings.IsObjectSnapEnabled)
        {
            List<PlacedObject> otherObjects = new List<PlacedObject>();
            if (allPlacedObjectsInScene != null) {
                foreach (PlacedObject po in allPlacedObjectsInScene) {
                    if (po != objectBeingMovedOrNull) { // Exclude the object being moved itself
                        otherObjects.Add(po);
                    }
                }
            }

            if (otherObjects.Count > 0)
            {
                // TrySnapPosition is assumed to have: out bool xSnapped, out float xSnapLineGuide, etc.
                positionAfterObjectSnap = TrySnapPosition(
                    potentialPivotPosition,
                    activeObjHypotheticalSnapPoints,
                    otherObjects,
                    out xObjectSnapped, out xObjectSnapLineCoord,
                    out yObjectSnapped, out yObjectSnapLineCoord
                );
            }
        }

        // --- 2. Grid Snapping (applied *after* object snapping) ---
        Vector2 finalSnappedPosition = positionAfterObjectSnap; // Start with (potentially) object-snapped position
        if (globalSettings.IsSnapToGridEnabled)
        {
            Vector2 originalPosForGridSnap = finalSnappedPosition;
            finalSnappedPosition = GlobalSettings.SnapPositionToGrid(originalPosForGridSnap, globalSettings.GridSize);

            if (!Mathf.IsEqualApprox(originalPosForGridSnap.X, finalSnappedPosition.X)) {
                xGridSnapped = true;
                xGridSnapLineCoord = finalSnappedPosition.X; // Grid snap aligns pivot, so line is at pivot X
            }
            if (!Mathf.IsEqualApprox(originalPosForGridSnap.Y, finalSnappedPosition.Y)) {
                yGridSnapped = true;
                yGridSnapLineCoord = finalSnappedPosition.Y; // Grid snap aligns pivot, so line is at pivot Y
            }
        }
        return finalSnappedPosition;
    }

	// --- CombinedTransformGizmo Signal Handlers ---
	private void _OnGizmoTranslationStarted(string axisOrType, PlacedObject targetObject)
	{
		if (targetObject != null && GodotObject.IsInstanceValid(targetObject))
		{
			gizmoDragStartObjectPositionsForUndo_Translate.Clear();
			gizmoDragStartObjectPositionsForUndo_Translate[targetObject] = targetObject.GlobalPosition;
		}
		else
		{
			gizmoDragStartObjectPositionsForUndo_Translate.Clear();
		}
	}

	private void _OnGizmoTranslationUpdated(Vector2 rawNewDesiredPosition, PlacedObject targetObject)
	{
		if (targetObject == null || !GodotObject.IsInstanceValid(targetObject) ||
			globalSettings == null || snapFeedbackDisplayInstance == null )
		{
			snapFeedbackDisplayInstance?.ClearAllLines(); // Clear lines if we can't proceed
			return;
		}
		// Ensure it's still the selected one if selection matters strictly here (usually does for gizmos)
		if (currentlySelectedObjects.Count != 1 || currentlySelectedObjects[0] != targetObject) {
			snapFeedbackDisplayInstance.ClearAllLines();
			return;
		}


		Sprite2D sprite = targetObject.GetNodeOrNull<Sprite2D>("ObjectSprite");
		PlacedObject.GlobalSnapPoints activeObjHypotheticalSnapPoints;
		if (sprite != null && sprite.Texture != null) {
			Vector2 textureSize = sprite.Texture.GetSize();
			float scaledHalfWidth = (textureSize.X * targetObject.CurrentScale.X) / 2.0f;
			float scaledHalfHeight = (textureSize.Y * targetObject.CurrentScale.Y) / 2.0f;
			activeObjHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints {
				Center = rawNewDesiredPosition,
				LeftX = rawNewDesiredPosition.X - scaledHalfWidth, RightX = rawNewDesiredPosition.X + scaledHalfWidth,
				TopY = rawNewDesiredPosition.Y - scaledHalfHeight, BottomY = rawNewDesiredPosition.Y + scaledHalfHeight
			};
		} else {
			activeObjHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints {
				Center = rawNewDesiredPosition, LeftX = rawNewDesiredPosition.X, RightX = rawNewDesiredPosition.X,
				TopY = rawNewDesiredPosition.Y, BottomY = rawNewDesiredPosition.Y
			};
		}

		List<PlacedObject> allObjectsInScene = new List<PlacedObject>();
		Node placedObjectsRootNode = GetNodeOrNull("PlacedObjectsRoot");
		if (placedObjectsRootNode != null) {
			foreach (Node child in placedObjectsRootNode.GetChildren()) {
				if (child is PlacedObject po) { allObjectsInScene.Add(po); } // No need to exclude targetObject here, ApplySnappingRules handles it
			}
		}

		Vector2 finalSnappedPosition = ApplySnappingRules(
			rawNewDesiredPosition,
			activeObjHypotheticalSnapPoints,
			targetObject,
			allObjectsInScene, // Pass all objects
			out bool xObjSnapped, out float xObjSnapLine,
			out bool yObjSnapped, out float yObjSnapLine,
			out bool xGridSnapped, out float xGridSnapLine,
			out bool yGridSnapped, out float yGridSnapLine
		);

		targetObject.GlobalPosition = finalSnappedPosition;
		if (combinedTransformGizmoInstance != null) combinedTransformGizmoInstance.GlobalPosition = finalSnappedPosition;

		snapFeedbackDisplayInstance.ClearAllLines();
		Rect2 viewRect = GetViewportRectForSnapLines();
		if (xObjSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xObjSnapLine, viewRect.Position.Y), new Vector2(xObjSnapLine, viewRect.End.Y), Colors.Aqua);
		if (yObjSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(viewRect.Position.X, yObjSnapLine), new Vector2(viewRect.End.X, yObjSnapLine), Colors.Aqua);
		if (xGridSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xGridSnapLine, viewRect.Position.Y), new Vector2(xGridSnapLine, viewRect.End.Y), Colors.LightGreen);
		if (yGridSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(viewRect.Position.X, yGridSnapLine), new Vector2(viewRect.End.X, yGridSnapLine), Colors.LightGreen);

		if (!isUpdatingPanelFromSelection && objectPropertiesPanel != null && objectPropertiesPanel.Visible) {
			LineEdit posXEdit = objectPropertiesPanel.GetNodeOrNull<LineEdit>("PropertiesVBox/HBoxContainer_PositionX/PositionXLineEdit");
			LineEdit posYEdit = objectPropertiesPanel.GetNodeOrNull<LineEdit>("PropertiesVBox/HBoxContainer_PositionY/PositionYLineEdit");
			isUpdatingPanelFromSelection = true;
			if (posXEdit != null) posXEdit.Text = finalSnappedPosition.X.ToString("F2");
			if (posYEdit != null) posYEdit.Text = finalSnappedPosition.Y.ToString("F2");
			isUpdatingPanelFromSelection = false;
		}
	}

	private void _OnGizmoTranslationFinished(Vector2 finalAppliedPositionByGizmo, PlacedObject targetObject)
	{
		if (targetObject == null || !GodotObject.IsInstanceValid(targetObject) || globalSettings == null) return;

		if (!gizmoDragStartObjectPositionsForUndo_Translate.TryGetValue(targetObject, out Vector2 originalPositionAtDragStart))
        {
            GD.PrintErr("MainScene: Could not find original drag start position for undo. GizmoTranslationFinished aborted.");
            snapFeedbackDisplayInstance?.ClearAllLines();
            UpdateObjectPropertiesPanel(); // Refresh to actual state
            return;
        }

		Sprite2D sprite = targetObject.GetNodeOrNull<Sprite2D>("ObjectSprite");
		PlacedObject.GlobalSnapPoints finalHypotheticalSnapPoints;
		if (sprite != null && sprite.Texture != null) {
            Vector2 textureSize = sprite.Texture.GetSize();
            float scaledHalfWidth = (textureSize.X * targetObject.CurrentScale.X) / 2.0f;
            float scaledHalfHeight = (textureSize.Y * targetObject.CurrentScale.Y) / 2.0f;
            finalHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints {
                Center = finalAppliedPositionByGizmo,
                LeftX = finalAppliedPositionByGizmo.X - scaledHalfWidth, RightX = finalAppliedPositionByGizmo.X + scaledHalfWidth,
                TopY = finalAppliedPositionByGizmo.Y - scaledHalfHeight, BottomY = finalAppliedPositionByGizmo.Y + scaledHalfHeight
            };
        } else {
            finalHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints {
				Center = finalAppliedPositionByGizmo, LeftX = finalAppliedPositionByGizmo.X, RightX = finalAppliedPositionByGizmo.X,
				TopY = finalAppliedPositionByGizmo.Y, BottomY = finalAppliedPositionByGizmo.Y
			};
        }

		List<PlacedObject> allObjectsInScene = new List<PlacedObject>();
		Node placedObjectsRootNode = GetNodeOrNull("PlacedObjectsRoot");
		if (placedObjectsRootNode != null) {
			foreach (Node child in placedObjectsRootNode.GetChildren()) {
				if (child is PlacedObject po) { allObjectsInScene.Add(po); }
			}
		}

		Vector2 trueFinalSnappedPosition = ApplySnappingRules(
			finalAppliedPositionByGizmo,
			finalHypotheticalSnapPoints,
			targetObject,
			allObjectsInScene, // Pass all objects
			out bool _, out float _, out bool _, out float _,
			out bool _, out float _, out bool _, out float _
		);

		targetObject.GlobalPosition = trueFinalSnappedPosition;
		if (combinedTransformGizmoInstance != null) combinedTransformGizmoInstance.GlobalPosition = trueFinalSnappedPosition;

		if (!originalPositionAtDragStart.IsEqualApprox(trueFinalSnappedPosition))
		{
			if (undoRedoManagerInstance != null && tileDrawer != null)
			{
				MoveObjectAction action = new MoveObjectAction(targetObject, originalPositionAtDragStart, trueFinalSnappedPosition);
				// No Execute needed, state is final due to live updates. We just record the change.
				undoRedoManagerInstance.RecordAction(action);
				SetMapDirty(true);
			}
		}
		gizmoDragStartObjectPositionsForUndo_Translate.Clear();
		UpdateObjectPropertiesPanel();
		snapFeedbackDisplayInstance?.ClearAllLines();
	}

    // Helper for snap line viewport rect
    private Rect2 GetViewportRectForSnapLines() {
        Camera2D mapCam = GetNodeOrNull<Camera2D>("MapCamera");
        if (mapCam != null) return mapCam.GetVisibleRect();
        return GetViewportRect(); // Fallback
    }
}
