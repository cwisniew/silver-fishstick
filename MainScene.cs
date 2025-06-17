using Godot;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks; // Required for TaskCompletionSource
using System.Globalization; // Required for NumberStyles and CultureInfo

public partial class MainScene : Control
{
	// Signals
	[Signal] public delegate void DrawingModeChangedEventHandler(DrawingMode newMode);
	[Signal] public delegate void MapDirtyStateChangedEventHandler(bool isDirty);
	[Signal] public delegate void CurrentMapPathChangedEventHandler(string newPath);

	// Enums
	public enum FileMenuItemId { New, Open, Save, SaveAs, AppSettings, Quit }
	private enum EditMenuItemId { Undo, Redo }
	public enum ViewMenuItemId { ToggleGridSnap, ToggleObjectSnap }
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
		DistributeHorizontalSpacing,
		DistributeVerticalSpacing
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
	private ObjectPropertiesPanel objectPropertiesPanelInstance;


	// UI Panels & Controls
	private Panel toolbarPanel;
	private AssetLibraryPanel assetLibraryPanelInstance;
	private PanelContainer objectPropertiesPanel;
	private AppSettingsPanelController appSettingsPanelInstance;
	private Window unsavedChangesDialogNode;
    private Label unsavedDialogMessageLabel;
    private TaskCompletionSource<UnsavedChangesDialogResult> dialogTcs;
	private MenuBar topMenuBar;
    private PopupMenu fileMenuPopup;
	private PopupMenu editMenuPopup;
    private int undoMenuItemIndex = -1;
    private int redoMenuItemIndex = -1;
	private PopupMenu viewMenuPopup;
    private int toggleGridSnapMenuItemIndex = -1;
    private int toggleObjectSnapMenuItemIndex = -1;
	private PopupMenu toolsMenuPopup;
	private Dictionary<DrawingMode, int> toolMenuItemIndices = new Dictionary<DrawingMode, int>();

	// Toolbar Buttons
	private Button manageAssetsButton;
	private CheckBox scatterModeCheckBox;
	private Button undoButton;
	private Button redoButton;
	private Button eraserButton;

	// Object Properties Panel Controls (Directly managed by MainScene - for now, only a few)
	private Label assetNameLabelInProps;
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
	private Button distributeHSpacingButton;
	private Button distributeVSpacingButton;
	// Alignment Context UI
	private HBoxContainer alignContextHBox;
	private OptionButton alignContextOptionButton;

	// File Dialogs
	private FileDialog saveFileDialog;
	private FileDialog loadFileDialog;
	private TaskCompletionSource<string> saveAsTcs;

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

		if (globalSettings != null)
		{
			globalSettings.GridSettingsChanged += _OnGridSettingsChangedForViewMenu;
			globalSettings.ObjectSnapSettingsChanged += _OnObjectSnapSettingsChangedForViewMenu;
		}

		if (!this.IsConnected(SignalName.DrawingModeChanged, Callable.From<DrawingMode>(_OnActualDrawingModeChanged)))
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

		unsavedChangesDialogNode = new Window { Title = "Unsaved Changes", Exclusive = true, Visible = false, MinSize = new Vector2I(380, 130), MaxSize = new Vector2I(380, 130), KeepSize = true, Transient = true, AlwaysOnTop = true, ContentScaleMode = Window.ContentScaleModeEnum.Disabled, };
		unsavedChangesDialogNode.SetFlag(Window.Flags.ResizeDisabled, true);
		VBoxContainer dialogVBox = new VBoxContainer { Name = "DialogVBox" }; dialogVBox.AddThemeConstantOverride("separation", 15); unsavedChangesDialogNode.AddChild(dialogVBox);
		unsavedDialogMessageLabel = new Label { Name = "MessageLabel", Text = "You have unsaved changes. What would you like to do?", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(340, 0) }; dialogVBox.AddChild(unsavedDialogMessageLabel);
		var m = new MarginContainer(); m.AddThemeConstantOverride("margin_left", 20); m.AddThemeConstantOverride("margin_right", 20); m.AddThemeConstantOverride("margin_top", 10); dialogVBox.AddChild(m);
		HBoxContainer dialogButtonsHBox = new HBoxContainer { Name = "ButtonsHBox", Alignment = HBoxContainer.AlignmentMode.Center }; dialogButtonsHBox.AddThemeConstantOverride("separation", 10); m.AddChild(dialogButtonsHBox);
		Button btnSave = new Button { Text = "Save", Name = "SaveButton", CustomMinimumSize = new Vector2(80,0) }; btnSave.Pressed += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.Save); dialogButtonsHBox.AddChild(btnSave);
		Button btnDontSave = new Button { Text = "Don't Save", Name = "DontSaveButton", CustomMinimumSize = new Vector2(100,0) }; btnDontSave.Pressed += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.DontSave); dialogButtonsHBox.AddChild(btnDontSave);
		Button btnCancel = new Button { Text = "Cancel", Name = "CancelButton", CustomMinimumSize = new Vector2(80,0) }; btnCancel.Pressed += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.Cancel); dialogButtonsHBox.AddChild(btnCancel);
		AddChild(unsavedChangesDialogNode);
		unsavedChangesDialogNode.CloseRequested += () => _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult.Cancel);

		objectPropertiesPanel = GetNode<PanelContainer>("ObjectPropertiesPanelInstance");
		objectPropertiesPanelInstance = objectPropertiesPanel as ObjectPropertiesPanel;
		if (objectPropertiesPanelInstance == null && objectPropertiesPanel != null) {
			GD.PrintErr("MainScene: Failed to cast ObjectPropertiesPanelInstance to ObjectPropertiesPanel script type. Ensure script is attached.");
		}

		if (objectPropertiesPanel != null) {
			assetNameLabelInProps = objectPropertiesPanel.GetNodeOrNull<Label>("PropertiesVBox/AssetNameHBox/AssetNameLabel");
			deleteObjectButtonFromPanelProp = objectPropertiesPanel.GetNodeOrNull<Button>("PropertiesVBox/DeleteObjectButton");

			if (objectPropertiesPanelInstance != null)
			{
				if (!objectPropertiesPanelInstance.IsConnected(ObjectPropertiesPanel.SignalName.PositionChangeRequested, Callable.From<NodePath, Vector2>(OnPanelPositionChangeRequested)))
				{
					objectPropertiesPanelInstance.PositionChangeRequested += OnPanelPositionChangeRequested;
				}
				if (!objectPropertiesPanelInstance.IsConnected(ObjectPropertiesPanel.SignalName.RotationChangeRequested, Callable.From<NodePath, float>(OnPanelRotationChangeRequested)))
				{
					objectPropertiesPanelInstance.RotationChangeRequested += OnPanelRotationChangeRequested;
				}
				if (!objectPropertiesPanelInstance.IsConnected(ObjectPropertiesPanel.SignalName.ScaleChangeRequested, Callable.From<NodePath, Vector2>(OnPanelScaleChangeRequested)))
				{
					objectPropertiesPanelInstance.ScaleChangeRequested += OnPanelScaleChangeRequested;
				}
			}

			bool mainSceneDirectControlsValid = assetNameLabelInProps != null && deleteObjectButtonFromPanelProp != null;
			if (mainSceneDirectControlsValid) {
				if(deleteObjectButtonFromPanelProp != null && !deleteObjectButtonFromPanelProp.IsConnected(Button.SignalName.Pressed, Callable.From(_OnPropsDeleteObjectPressed)))
				{
					deleteObjectButtonFromPanelProp.Pressed += _OnPropsDeleteObjectPressed;
				}
			} else { GD.PrintErr("MainScene: Failed to get AssetNameLabel or DeleteObjectButton from ObjectPropertiesPanelInstance!"); }

			alignmentSectionLabel = objectPropertiesPanel.GetNodeOrNull<Label>("PropertiesVBox/AlignLabel");
			alignmentButtonsHBox1 = objectPropertiesPanel.GetNodeOrNull<HBoxContainer>("PropertiesVBox/AlignmentButtonsHBox1");
			alignmentButtonsHBox2 = objectPropertiesPanel.GetNodeOrNull<HBoxContainer>("PropertiesVBox/AlignmentButtonsHBox2");
			if (alignmentSectionLabel == null || alignmentButtonsHBox1 == null || alignmentButtonsHBox2 == null) { GD.PrintErr("MainScene: Failed to get alignment group nodes from ObjectPropertiesPanelInstance!"); alignmentSectionLabel = null; alignmentButtonsHBox1 = null; alignmentButtonsHBox2 = null; } else {
				alignLeftButton = alignmentButtonsHBox1.GetNodeOrNull<Button>("AlignLeftButton"); alignHorizontalCenterButton = alignmentButtonsHBox1.GetNodeOrNull<Button>("AlignHorizontalCenterButton"); alignRightButton = alignmentButtonsHBox1.GetNodeOrNull<Button>("AlignRightButton");
				alignTopButton = alignmentButtonsHBox2.GetNodeOrNull<Button>("AlignTopButton"); alignVerticalCenterButton = alignmentButtonsHBox2.GetNodeOrNull<Button>("AlignVerticalCenterButton"); alignBottomButton = alignmentButtonsHBox2.GetNodeOrNull<Button>("AlignBottomButton");
				bool allButtonsFound = alignLeftButton != null && alignHorizontalCenterButton != null && alignRightButton != null && alignTopButton != null && alignVerticalCenterButton != null && alignBottomButton != null;
				if (!allButtonsFound) { GD.PrintErr("MainScene: Failed to get all individual alignment buttons from ObjectPropertiesPanelInstance!"); alignmentSectionLabel = null; alignmentButtonsHBox1 = null; alignmentButtonsHBox2 = null; } else {
					if(!alignLeftButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnAlignmentButtonPressed(AlignmentMode.AlignLeft))))  alignLeftButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignLeft);
					if(!alignHorizontalCenterButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnAlignmentButtonPressed(AlignmentMode.AlignHorizontalCenter)))) alignHorizontalCenterButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignHorizontalCenter);
					if(!alignRightButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnAlignmentButtonPressed(AlignmentMode.AlignRight)))) alignRightButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignRight);
					if(!alignTopButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnAlignmentButtonPressed(AlignmentMode.AlignTop)))) alignTopButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignTop);
					if(!alignVerticalCenterButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnAlignmentButtonPressed(AlignmentMode.AlignVerticalCenter)))) alignVerticalCenterButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignVerticalCenter);
					if(!alignBottomButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnAlignmentButtonPressed(AlignmentMode.AlignBottom)))) alignBottomButton.Pressed += () => _OnAlignmentButtonPressed(AlignmentMode.AlignBottom);
					alignmentSectionLabel.Visible = false; alignmentButtonsHBox1.Visible = false; alignmentButtonsHBox2.Visible = false; } }
			alignContextHBox = objectPropertiesPanel.GetNodeOrNull<HBoxContainer>("PropertiesVBox/AlignContextHBox");
			if (alignContextHBox != null) { alignContextOptionButton = alignContextHBox.GetNodeOrNull<OptionButton>("AlignContextOptionButton"); if (alignContextOptionButton != null) { alignContextOptionButton.Clear(); alignContextOptionButton.AddItem("First Selected", (int)AlignContext.ToFirstSelected); alignContextOptionButton.AddItem("Selection Bounds", (int)AlignContext.ToSelectionBounds); alignContextOptionButton.Selected = (int)AlignContext.ToFirstSelected; } else { GD.PrintErr("MainScene: AlignContextOptionButton not found in AlignContextHBox!"); } alignContextHBox.Visible = false; } else { GD.PrintErr("MainScene: AlignContextHBox not found in ObjectPropertiesPanelInstance!"); }
			distributionSectionLabel = objectPropertiesPanel.GetNodeOrNull<Label>("PropertiesVBox/DistributionSectionLabel"); distributionButtonsHBox = objectPropertiesPanel.GetNodeOrNull<HBoxContainer>("PropertiesVBox/DistributionButtonsHBox");
			if (distributionSectionLabel == null || distributionButtonsHBox == null) { GD.PrintErr("MainScene: Failed to get distribution group nodes from ObjectPropertiesPanelInstance!"); distributionSectionLabel = null; distributionButtonsHBox = null; } else {
				distributeHCentersButton = distributionButtonsHBox.GetNodeOrNull<Button>("DistributeHCentersButton"); distributeVCentersButton = distributionButtonsHBox.GetNodeOrNull<Button>("DistributeVCentersButton");
				if (distributeHCentersButton != null && !distributeHCentersButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalCenters)))) distributeHCentersButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalCenters);
				if (distributeVCentersButton != null && !distributeVCentersButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalCenters)))) distributeVCentersButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalCenters);
				distributeHSpacingButton = distributionButtonsHBox.GetNodeOrNull<Button>("DistributeHSpacingButton"); distributeVSpacingButton = distributionButtonsHBox.GetNodeOrNull<Button>("DistributeVSpacingButton");
				if (distributeHSpacingButton != null && !distributeHSpacingButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalSpacing)))) distributeHSpacingButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeHorizontalSpacing);
				if (distributeVSpacingButton != null && !distributeVSpacingButton.IsConnected(Button.SignalName.Pressed, Callable.From(() => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalSpacing)))) distributeVSpacingButton.Pressed += () => _OnDistributeObjectsButtonPressed(DistributionMode.DistributeVerticalSpacing);
				distributionSectionLabel.Visible = false; distributionButtonsHBox.Visible = false; }
			objectPropertiesPanel.Visible = false;
		} else { GD.PrintErr("MainScene: ObjectPropertiesPanelInstance node not found!"); }

		PackedScene rotGizmoSceneLoader = ResourceLoader.Load<PackedScene>(RotationGizmoScenePath); if (rotGizmoSceneLoader != null) { Node rotInstance = rotGizmoSceneLoader.Instantiate(); if (rotInstance is RotationGizmo rgi) { rotationGizmoInstance = rgi; AddChild(rotationGizmoInstance); rotationGizmoInstance.Visible = false; if(!rotationGizmoInstance.IsConnected(nameof(RotationGizmo.RotationStarted), Callable.From<float>(_OnGizmoRotationStarted))) rotationGizmoInstance.RotationStarted += _OnGizmoRotationStarted; if(!rotationGizmoInstance.IsConnected(nameof(RotationGizmo.RotationUpdated), Callable.From<float>(_OnGizmoRotationUpdated))) rotationGizmoInstance.RotationUpdated += _OnGizmoRotationUpdated; if(!rotationGizmoInstance.IsConnected(nameof(RotationGizmo.RotationFinished), Callable.From<float, float>(_OnGizmoRotationFinished))) rotationGizmoInstance.RotationFinished += _OnGizmoRotationFinished; } else { GD.PrintErr($"Failed to cast to RotationGizmo. Path: {RotationGizmoScenePath}"); rotInstance?.QueueFree(); } } else { GD.PrintErr($"Failed to load RotationGizmo scene from: {RotationGizmoScenePath}"); }
		PackedScene scaleGizmoPackedScene = ResourceLoader.Load<PackedScene>(ScaleGizmoScenePath); if (scaleGizmoPackedScene != null) { Node scaleInstance = scaleGizmoPackedScene.Instantiate(); if (scaleInstance is ScaleGizmo sgi) { scaleGizmoInstance = sgi; AddChild(scaleGizmoInstance); scaleGizmoInstance.Hide(); if (!scaleGizmoInstance.IsConnected(nameof(ScaleGizmo.ScaleStarted), Callable.From<Vector2, ScaleGizmo.HandleType>(_OnGizmoScaleStarted))) scaleGizmoInstance.Connect(nameof(ScaleGizmo.ScaleStarted), Callable.From<Vector2, ScaleGizmo.HandleType>(_OnGizmoScaleStarted)); if (!scaleGizmoInstance.IsConnected(nameof(ScaleGizmo.ScaleUpdated), Callable.From<Vector2>(_OnGizmoScaleUpdated))) scaleGizmoInstance.Connect(nameof(ScaleGizmo.ScaleUpdated), Callable.From<Vector2>(_OnGizmoScaleUpdated)); if (!scaleGizmoInstance.IsConnected(nameof(ScaleGizmo.ScaleFinished), Callable.From<Vector2, Vector2>(_OnGizmoScaleFinished))) scaleGizmoInstance.Connect(nameof(ScaleGizmo.ScaleFinished), Callable.From<Vector2, Vector2>(_OnGizmoScaleFinished)); } else { GD.PrintErr($"Failed to cast to ScaleGizmo. Path: {ScaleGizmoScenePath}"); scaleInstance?.QueueFree(); } } else { GD.PrintErr($"Failed to load ScaleGizmo scene from: {ScaleGizmoScenePath}"); }
		var appSettingsPlaceholder = GetNodeOrNull<PanelContainer>("AppSettingsPanelInstance"); if (appSettingsPlaceholder != null) { PackedScene appSettingsScene = ResourceLoader.Load<PackedScene>("res://ui/AppSettingsPanel.tscn"); if (appSettingsScene != null) { Node appSettingsNode = appSettingsScene.Instantiate(); if(appSettingsNode is AppSettingsPanelController controller) { appSettingsPanelInstance = controller; Node parent = appSettingsPlaceholder.GetParent(); parent.RemoveChild(appSettingsPlaceholder); appSettingsPlaceholder.QueueFree(); parent.AddChild(appSettingsPanelInstance); appSettingsPanelInstance.Name = "AppSettingsPanelInstance"; if (!appSettingsPanelInstance.IsConnected(AppSettingsPanelController.SignalName.GlobalAssetPathChangedAndRefreshed, Callable.From(_OnGlobalAssetPathChanged))) appSettingsPanelInstance.Connect(AppSettingsPanelController.SignalName.GlobalAssetPathChangedAndRefreshed, Callable.From(_OnGlobalAssetPathChanged)); appSettingsPanelInstance.Hide(); } else { GD.PrintErr("MainScene: Failed to cast AppSettingsPanel scene root to AppSettingsPanelController."); appSettingsNode?.QueueFree(); } } else GD.PrintErr("MainScene: Failed to load res://ui/AppSettingsPanel.tscn"); } else GD.PrintErr("MainScene: AppSettingsPanelInstance placeholder not found!");
		PackedScene snapFeedbackPackedScene = ResourceLoader.Load<PackedScene>(SnapFeedbackDisplayScenePath); if (snapFeedbackPackedScene != null) { Node snapInstance = snapFeedbackPackedScene.Instantiate(); if (snapInstance is SnapFeedbackDisplay display) { snapFeedbackDisplayInstance = display; AddChild(snapFeedbackDisplayInstance); } else { GD.PrintErr($"Failed to instance SnapFeedbackDisplay from scene: {SnapFeedbackDisplayScenePath}"); snapInstance?.QueueFree(); } } else { GD.PrintErr($"Failed to load SnapFeedbackDisplay scene from: {SnapFeedbackDisplayScenePath}"); }
		PackedScene combinedGizmoPackedScene = ResourceLoader.Load<PackedScene>(CombinedTransformGizmoScenePath); if (combinedGizmoPackedScene != null) { Node combinedInstance = combinedGizmoPackedScene.Instantiate(); if (combinedInstance is CombinedTransformGizmo gizmo) { combinedTransformGizmoInstance = gizmo; AddChild(combinedTransformGizmoInstance); combinedTransformGizmoInstance.HideGizmo(); if(!combinedTransformGizmoInstance.IsConnected(nameof(CombinedTransformGizmo.TranslationHandleGrabbed), Callable.From<string, PlacedObject>(_OnGizmoTranslationStarted))) combinedTransformGizmoInstance.TranslationHandleGrabbed += _OnGizmoTranslationStarted; if(!combinedTransformGizmoInstance.IsConnected(nameof(CombinedTransformGizmo.TranslationUpdated), Callable.From<Vector2, PlacedObject>(_OnGizmoTranslationUpdated))) combinedTransformGizmoInstance.TranslationUpdated += _OnGizmoTranslationUpdated; if(!combinedTransformGizmoInstance.IsConnected(nameof(CombinedTransformGizmo.TranslationHandleReleased), Callable.From<Vector2, PlacedObject>(_OnGizmoTranslationFinished))) combinedTransformGizmoInstance.TranslationHandleReleased += _OnGizmoTranslationFinished; } else { GD.PrintErr($"Failed to instance CombinedTransformGizmo from scene: {CombinedTransformGizmoScenePath}"); combinedInstance?.QueueFree(); } } else { GD.PrintErr($"Failed to load CombinedTransformGizmo scene from: {CombinedTransformGizmoScenePath}"); }
		topMenuBar = GetNodeOrNull<MenuBar>("MainLayoutVBox/TopMenuBar"); if (topMenuBar == null) { topMenuBar = GetNodeOrNull<MenuBar>("TopMenuBar"); }
		if (topMenuBar != null) { fileMenuPopup = new PopupMenu(); fileMenuPopup.AddItem("New", (int)FileMenuItemId.New); fileMenuPopup.SetItemShortcut((int)FileMenuItemId.New, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.N) } }); fileMenuPopup.AddItem("Open...", (int)FileMenuItemId.Open); fileMenuPopup.SetItemShortcut((int)FileMenuItemId.Open, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.O) } }); fileMenuPopup.AddItem("Save", (int)FileMenuItemId.Save); fileMenuPopup.SetItemShortcut((int)FileMenuItemId.Save, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.S) } }); fileMenuPopup.AddItem("Save As...", (int)FileMenuItemId.SaveAs); var saveAsShortcut = new Shortcut(); saveAsShortcut.Events.Add(InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl | KeyModifierMask.MaskShift, Key.S)); fileMenuPopup.SetItemShortcut((int)FileMenuItemId.SaveAs, saveAsShortcut); fileMenuPopup.AddSeparator(); fileMenuPopup.AddItem("App Settings...", (int)FileMenuItemId.AppSettings); fileMenuPopup.AddSeparator(); fileMenuPopup.AddItem("Quit", (int)FileMenuItemId.Quit); fileMenuPopup.IdPressed += _OnFileMenuItemPressed; topMenuBar.AddChild(fileMenuPopup); topMenuBar.SetMenuTitle(0, "File"); topMenuBar.SetSubmenuPopup(0, fileMenuPopup.GetPath()); editMenuPopup = new PopupMenu(); editMenuPopup.AddItem("Undo", (int)EditMenuItemId.Undo); undoMenuItemIndex = editMenuPopup.ItemCount - 1; editMenuPopup.SetItemShortcut(undoMenuItemIndex, new Shortcut { Events = { InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl, Key.Z) } }); editMenuPopup.SetItemDisabled(undoMenuItemIndex, !(undoRedoManagerInstance?.CanUndo() ?? false)); editMenuPopup.AddItem("Redo", (int)EditMenuItemId.Redo); redoMenuItemIndex = editMenuPopup.ItemCount - 1; var redoShortcut = new Shortcut(); redoShortcut.Events.Add(InputEventKey.CreateWithModifiers(KeyModifierMask.MaskCmdOrCtrl | KeyModifierMask.MaskShift, Key.Z)); editMenuPopup.SetItemShortcut(redoMenuItemIndex, redoShortcut); editMenuPopup.SetItemDisabled(redoMenuItemIndex, !(undoRedoManagerInstance?.CanRedo() ?? false)); editMenuPopup.IdPressed += _OnEditMenuItemPressed; int editMenuIndex = topMenuBar.GetMenuCount(); topMenuBar.AddChild(editMenuPopup); topMenuBar.SetMenuTitle(editMenuIndex, "Edit"); topMenuBar.SetSubmenuPopup(editMenuIndex, editMenuPopup.GetPath()); if (globalSettings != null) { viewMenuPopup = new PopupMenu(); viewMenuPopup.Name = "ViewMenuPopup"; viewMenuPopup.AddItem("Toggle Grid Snap", (int)ViewMenuItemId.ToggleGridSnap); toggleGridSnapMenuItemIndex = viewMenuPopup.ItemCount - 1; viewMenuPopup.SetItemAsCheckable(toggleGridSnapMenuItemIndex, true); viewMenuPopup.SetItemChecked(toggleGridSnapMenuItemIndex, globalSettings.IsSnapToGridEnabled); viewMenuPopup.AddItem("Toggle Object Snap", (int)ViewMenuItemId.ToggleObjectSnap); toggleObjectSnapMenuItemIndex = viewMenuPopup.ItemCount - 1; viewMenuPopup.SetItemAsCheckable(toggleObjectSnapMenuItemIndex, true); viewMenuPopup.SetItemChecked(toggleObjectSnapMenuItemIndex, globalSettings.IsObjectSnapEnabled); viewMenuPopup.IdPressed += _OnViewMenuItemPressed; int viewMenuIndex = topMenuBar.GetMenuCount(); topMenuBar.AddChild(viewMenuPopup); topMenuBar.SetMenuTitle(viewMenuIndex, "View"); topMenuBar.SetSubmenuPopup(viewMenuIndex, viewMenuPopup.GetPath()); } toolsMenuPopup = new PopupMenu(); toolsMenuPopup.Name = "ToolsMenuPopup"; toolMenuItemIndices = new Dictionary<DrawingMode, int>(); DrawingMode[] modesToShowInMenu = { DrawingMode.Tile, DrawingMode.Room, DrawingMode.ObjectPlacement, DrawingMode.SelectObject }; foreach (DrawingMode mode_ in modesToShowInMenu) { string itemName = GetToolModeUserFriendlyName(mode_); toolsMenuPopup.AddItem(itemName, (int)mode_); int itemIndex = toolsMenuPopup.ItemCount - 1; toolMenuItemIndices[mode_] = itemIndex; toolsMenuPopup.SetItemAsCheckable(itemIndex, true); toolsMenuPopup.SetItemChecked(itemIndex, mode_ == this.currentDrawingMode); } if (toolsMenuPopup != null && !toolsMenuPopup.IsConnected(PopupMenu.SignalName.IdPressed, Callable.From<long>(_OnToolsMenuItemPressed))) { toolsMenuPopup.IdPressed += _OnToolsMenuItemPressed;} int toolsMenuIndex = topMenuBar.GetMenuCount(); topMenuBar.AddChild(toolsMenuPopup); topMenuBar.SetMenuTitle(toolsMenuIndex, "Tools"); topMenuBar.SetSubmenuPopup(toolsMenuIndex, toolsMenuPopup.GetPath()); } else { GD.PrintErr("MainScene: TopMenuBar node not found! Menus cannot be created.");}

		PopulateToolbar();
		SetupFileDialogs();
		if (undoRedoManagerInstance != null) {
			_OnUndoStackChanged(undoRedoManagerInstance.CanUndo());
			_OnRedoStackChanged(undoRedoManagerInstance.CanRedo());
		}
		UpdateObjectPropertiesPanel();
		UpdateWindowTitle();
		GetTree().AutoAcceptQuit = false;
		layersPanelController = GetNodeOrNull<LayersPanelController>("LayersPanel");
		if (layersPanelController == null) GD.PrintErr("MainScene: LayersPanelController node not found!");
	}

	public override async void _Notification(int what) { if (what == NotificationWMCloseRequest) { GD.Print("MainScene: WMCloseRequest received, calling _RequestQuitApplication."); _RequestQuitApplication(); GetViewport().SetInputAsHandled(); } }
	public void SetMapDirty(bool newDirtyState) { if (IsMapDirty == newDirtyState) return; IsMapDirty = newDirtyState; UpdateWindowTitle(); EmitSignal(nameof(MapDirtyStateChanged), IsMapDirty); }
	public void SetCurrentMapFilePath(string path) { currentMapFilePath = path; UpdateWindowTitle(); EmitSignal(nameof(CurrentMapPathChanged), currentMapFilePath ?? "Untitled"); SetMapDirty(false);  }
	public string GetCurrentMapFilePath() { return currentMapFilePath; }
	private bool _SaveMap(string path) { if (tileDrawer == null) { GD.PrintErr("SaveMap: TileDrawer not found."); return false; } GD.Print($"Saving map to: {path}"); MapSaverLoader.SaveMap(tileDrawer, path); SetCurrentMapFilePath(path); GD.Print("Map saved successfully."); return true; }
	private Task<string> _SaveMapAsAsync() { saveAsTcs = new TaskCompletionSource<string>(); saveFileDialog.PopupCentered(); return saveAsTcs.Task; }
	private void UpdateWindowTitle() { string title = applicationName; string fileName = string.IsNullOrEmpty(currentMapFilePath) ? "Untitled" : Path.GetFileName(currentMapFilePath); title += $" - {fileName}"; if (IsMapDirty) title += "*"; DisplayServer.WindowSetTitle(title); }
	private async void _OnUnsavedChangesDialogButtonPressed(UnsavedChangesDialogResult result) { unsavedChangesDialogNode.Hide(); UnsavedChangesDialogResult finalResult = result; if (result == UnsavedChangesDialogResult.Save) { bool saveSuccess; if (string.IsNullOrEmpty(currentMapFilePath)) { string chosenPath = await _SaveMapAsAsync(); saveSuccess = (chosenPath != null);  } else { saveSuccess = _SaveMap(currentMapFilePath); } if (!saveSuccess) { finalResult = UnsavedChangesDialogResult.Cancel;  } } dialogTcs?.TrySetResult(finalResult);  }
    public async Task<UnsavedChangesDialogResult> ShowUnsavedChangesDialog(string actionContextDescription = "proceeding") { if (!IsMapDirty) { return UnsavedChangesDialogResult.DontSave;  } if (unsavedChangesDialogNode == null || unsavedDialogMessageLabel == null) { GD.PrintErr("Unsaved changes dialog not properly initialized."); return UnsavedChangesDialogResult.Cancel;  } dialogTcs = new TaskCompletionSource<UnsavedChangesDialogResult>(); string message = $"You have unsaved changes. Do you want to save before {actionContextDescription}?"; if (string.IsNullOrEmpty(actionContextDescription)) { message = "You have unsaved changes. What would you like to do?"; } unsavedDialogMessageLabel.Text = message; unsavedChangesDialogNode.Position = DisplayServer.WindowGetSize() / 2 - unsavedChangesDialogNode.MinSize / 2; unsavedChangesDialogNode.Popup(); return await dialogTcs.Task; }
	private void _OnAssetsChanged() { PopulateToolbar(); SetMapDirty(true); }
	private void _OnUndoRedoHistoryChanged() { UpdateObjectPropertiesPanel(); RefreshLayerList(); SetMapDirty(true); }
	public void DeselectAllObjects(PlacedObject excludeFromDeselection = null) { List<PlacedObject> objectsToActuallyDeselect = new List<PlacedObject>(currentlySelectedObjects); bool selectionTrulyChanged = false; foreach (PlacedObject obj in objectsToActuallyDeselect) { if (obj == excludeFromDeselection) continue; if (GodotObject.IsInstanceValid(obj)) { Sprite2D sprite = obj.GetNode<Sprite2D>("ObjectSprite"); if (sprite != null) { if (originalModulations.TryGetValue(obj, out Color originalColor)) { sprite.Modulate = originalColor; } else { sprite.Modulate = Colors.White; } } } currentlySelectedObjects.Remove(obj); originalModulations.Remove(obj); selectionTrulyChanged = true; } if (selectionTrulyChanged || (excludeFromDeselection == null && currentlySelectedObjects.Count == 0) || (currentlySelectedObjects.Count == 0 && excludeFromDeselection != null) || (currentlySelectedObjects.Count == 1 && excludeFromDeselection != null && currentlySelectedObjects[0] == excludeFromDeselection)) { UpdateObjectPropertiesPanel();  } }

	private void UpdateObjectPropertiesPanel() {
			if (objectPropertiesPanel == null || assetNameLabelInProps == null || deleteObjectButtonFromPanelProp == null) {
			if(objectPropertiesPanel != null) objectPropertiesPanel.Visible = false;
			if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance)) rotationGizmoInstance.UpdateGizmoVisuals(null);
			if (scaleGizmoInstance != null && GodotObject.IsInstanceValid(scaleGizmoInstance)) scaleGizmoInstance.Hide();
			if (combinedTransformGizmoInstance != null && GodotObject.IsInstanceValid(combinedTransformGizmoInstance)) combinedTransformGizmoInstance.HideGizmo();
			return;
		}
		isUpdatingPanelFromSelection = true;
		if (objectPropertiesPanelInstance != null)
		{
			objectPropertiesPanelInstance.DisplayControls(currentlySelectedObjects, this);
			objectPropertiesPanel.Visible = (currentlySelectedObjects.Count > 0);
		}
		else
		{
			if(objectPropertiesPanel != null) objectPropertiesPanel.Visible = false;
		}

		bool singleSelected = currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]);
		PlacedObject selectedObject = singleSelected ? currentlySelectedObjects[0] : null;

		if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance)) rotationGizmoInstance.UpdateGizmoVisuals(selectedObject);
		if (scaleGizmoInstance != null && GodotObject.IsInstanceValid(scaleGizmoInstance))
		{
			if (singleSelected) scaleGizmoInstance.ShowForTarget(selectedObject);
			else scaleGizmoInstance.Hide();
		}
		if (combinedTransformGizmoInstance != null && GodotObject.IsInstanceValid(combinedTransformGizmoInstance))
		{
			if (singleSelected) combinedTransformGizmoInstance.ShowForTarget(selectedObject);
			else combinedTransformGizmoInstance.HideGizmo();
		}

		if (alignmentSectionLabel != null) { bool showAlignControls = (currentlySelectedObjects.Count > 1); alignmentSectionLabel.Visible = showAlignControls; if (alignContextHBox != null) alignContextHBox.Visible = showAlignControls; if (alignmentButtonsHBox1 != null) alignmentButtonsHBox1.Visible = showAlignControls; if (alignmentButtonsHBox2 != null) alignmentButtonsHBox2.Visible = showAlignControls; if (!showAlignControls && alignContextOptionButton != null) { alignContextOptionButton.Selected = (int)AlignContext.ToFirstSelected; } }
		if (distributionSectionLabel != null) { bool showDistributeControls = (currentlySelectedObjects.Count > 2); distributionSectionLabel.Visible = showDistributeControls; if (distributionButtonsHBox != null) distributionButtonsHBox.Visible = showDistributeControls; }
		isUpdatingPanelFromSelection = false;
	}

	private void _OnPropsDeleteObjectPressed() { if (!isUpdatingPanelFromSelection) _HandleDeleteSelectedObjects(); }
	private void _HandleDeleteSelectedObjects() { if (currentlySelectedObjects.Count == 0) return; if (assetManager == null || undoRedoManagerInstance == null || tileDrawer == null) { GD.PrintErr("Delete deps null."); return; } Node placedObjectsRootNode = GetNode("PlacedObjectsRoot"); if (!(placedObjectsRootNode is Node2D objectsRootParent)) { GD.PrintErr("PlacedObjectsRoot not found/Node2D."); return; } List<PlacedObject> toDeleteCopy = new List<PlacedObject>(currentlySelectedObjects); DeselectAllObjects(); DeleteMultipleObjectsAction action = new DeleteMultipleObjectsAction( toDeleteCopy, objectsRootParent.GetPath(), assetManager, TileDrawer.PlacedObjectScenePath ); action.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true); }

	// Gizmo Handlers
	private void _OnGizmoRotationStarted(float initialRotationDegrees) { if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) { gizmoDragStartRotationForUndo = initialRotationDegrees; } }
	private void _OnGizmoRotationUpdated(float newRotationDegrees) { if (isUpdatingPanelFromSelection) return; if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]) && objectPropertiesPanelInstance != null) { objectPropertiesPanelInstance.UpdateDisplayedRotation(newRotationDegrees); } }
	private void _OnGizmoRotationFinished(float finalRotationDegrees, float originalRotationDegreesAtDragStart) { if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) { PlacedObject activeObject = currentlySelectedObjects[0]; activeObject.RotationDegrees = finalRotationDegrees; if (!Mathf.IsEqualApprox(originalRotationDegreesAtDragStart, finalRotationDegrees)) { if (undoRedoManagerInstance != null ) { RotateObjectAction action = new RotateObjectAction(activeObject, originalRotationDegreesAtDragStart, finalRotationDegrees); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true); } } if(objectPropertiesPanelInstance != null) objectPropertiesPanelInstance.UpdateDisplayedRotation(finalRotationDegrees); } }
	private void _OnGizmoScaleStarted(Vector2 initialScale, ScaleGizmo.HandleType handleType) { if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) { gizmoDragStartScaleForUndo = initialScale;  } }
	private void _OnGizmoScaleUpdated(Vector2 newScale) { if (isUpdatingPanelFromSelection) return; if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]) && objectPropertiesPanelInstance != null) { objectPropertiesPanelInstance.UpdateDisplayedScale(newScale); } }
	private void _OnGizmoScaleFinished(Vector2 finalScale, Vector2 originalScaleOnDragStart) { if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) { PlacedObject activeObject = currentlySelectedObjects[0]; activeObject.Scale = finalScale; if (!originalScaleOnDragStart.IsEqualApprox(finalScale)) {  if (undoRedoManagerInstance != null) { ScaleObjectAction action = new ScaleObjectAction(activeObject, originalScaleOnDragStart, finalScale); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true); } } if(objectPropertiesPanelInstance != null) objectPropertiesPanelInstance.UpdateDisplayedScale(finalScale); } }

	public override void _UnhandledInput(InputEvent @event) { if (combinedTransformGizmoInstance != null && combinedTransformGizmoInstance.Visible && combinedTransformGizmoInstance.IsInputCapturedByGizmo()) { GetViewport().SetInputAsHandled(); return; } if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsDragging()) { GetViewport().SetInputAsHandled(); return; }  if (scaleGizmoInstance != null && scaleGizmoInstance.Visible && scaleGizmoInstance.IsDragging()) { GetViewport().SetInputAsHandled(); return; } if (GetViewport().GuiGetFocusOwner() != null && !(GetViewport().GuiGetFocusOwner() is Button)) return; if (currentDrawingMode == DrawingMode.SelectObject) { if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left) { if (GetViewport().IsInputHandled()) return;  if (mouseButtonEvent.Pressed) { if (combinedTransformGizmoInstance != null && combinedTransformGizmoInstance.Visible && combinedTransformGizmoInstance.IsMouseOverAnyHandle()) { return; }  if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsMouseOverHandle()) { return; } if (scaleGizmoInstance != null && scaleGizmoInstance.Visible && scaleGizmoInstance.IsMouseOverAnyHandle()) { return; } Vector2 mousePos = GetGlobalMousePosition(); PhysicsDirectSpaceState2D spaceState = GetTree().Root.World2D.DirectSpaceState; var queryParams = new PhysicsPointQueryParameters2D { Position = mousePos, CollideWithAreas = true, CollideWithBodies = false, CollisionMask = 1 }; var results = spaceState.IntersectPoint(queryParams, 1); PlacedObject newlyClickedObject = null; if (results.Count > 0) { if (results[0]["collider"].Obj is Area2D area && area.GetParent() is PlacedObject po) newlyClickedObject = po; } bool isShiftPressed = Input.IsKeyPressed(Key.Shift) || Input.IsKeyPressed(Key.CmdOrCtrl); if (newlyClickedObject != null) { if (isShiftPressed) { if (currentlySelectedObjects.Contains(newlyClickedObject)) { if (GodotObject.IsInstanceValid(newlyClickedObject)) { Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite"); if (sprite != null && originalModulations.TryGetValue(newlyClickedObject, out Color originalColor)) sprite.Modulate = originalColor; else if (sprite != null) sprite.Modulate = Colors.White;  } currentlySelectedObjects.Remove(newlyClickedObject); originalModulations.Remove(newlyClickedObject); } else { currentlySelectedObjects.Add(newlyClickedObject); Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite"); if (sprite != null) { if (!originalModulations.ContainsKey(newlyClickedObject)) originalModulations[newlyClickedObject] = sprite.Modulate; sprite.Modulate = new Color(0.7f, 0.7f, 1.0f, 0.8f);  } } } else { if (!currentlySelectedObjects.Contains(newlyClickedObject) || currentlySelectedObjects.Count > 1) { DeselectAllObjects(excludeFromDeselection: newlyClickedObject); if (!currentlySelectedObjects.Contains(newlyClickedObject)) currentlySelectedObjects.Add(newlyClickedObject);  Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite"); if (sprite != null) { originalModulations[newlyClickedObject] = sprite.Modulate; sprite.Modulate = new Color(0.7f, 0.7f, 1.0f, 0.8f); } } else if (currentlySelectedObjects.Count == 1 && currentlySelectedObjects[0] != newlyClickedObject) { DeselectAllObjects(); currentlySelectedObjects.Add(newlyClickedObject); Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite"); if (sprite != null) { originalModulations[newlyClickedObject] = sprite.Modulate; sprite.Modulate = new Color(0.7f, 0.7f, 1.0f, 0.8f); } } } if (currentlySelectedObjects.Count == 1) { isDraggingObject = true; dragStartMousePosition = mousePos; dragStartObjectOriginalPosition = currentlySelectedObjects[0].GlobalPosition; } else { isDraggingObject = false; } } else { if (!isShiftPressed) DeselectAllObjects(); isDraggingObject = false;  } UpdateObjectPropertiesPanel(); GetViewport().SetInputAsHandled(); } else { if (isDraggingObject && currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) { PlacedObject draggedObject = currentlySelectedObjects[0]; Vector2 finalPos = draggedObject.GlobalPosition; if (!dragStartObjectOriginalPosition.IsEqualApprox(finalPos)) { draggedObject.GlobalPosition = dragStartObjectOriginalPosition; MoveObjectAction action = new MoveObjectAction(draggedObject, dragStartObjectOriginalPosition, finalPos); action.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true); UpdateObjectPropertiesPanel();  } } isDraggingObject = false; if (isDraggingObject && mouseButtonEvent.Pressed == false && currentlySelectedObjects.Count ==1 ) { GetViewport().SetInputAsHandled(); } } } else if (@event is InputEventMouseMotion mouseMotionEvent && isDraggingObject) { if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) { PlacedObject draggedObject = currentlySelectedObjects[0]; Vector2 mouseDelta = GetGlobalMousePosition() - dragStartMousePosition; Vector2 newPotentialPos = dragStartObjectOriginalPosition + mouseDelta; Vector2 finalVisualPos = newPotentialPos; if (globalSettings == null) { GD.PrintErr("MainScene Drag: GlobalSettings not available! Snapping will be skipped."); finalVisualPos = newPotentialPos;  } else { Sprite2D sprite = draggedObject.GetNodeOrNull<Sprite2D>("ObjectSprite"); PlacedObject.GlobalSnapPoints hypotheticalSnapPoints; if (sprite != null && sprite.Texture != null) { Vector2 textureSize = sprite.Texture.GetSize(); float scaledHalfWidth = (textureSize.X * draggedObject.CurrentScale.X) / 2.0f; float scaledHalfHeight = (textureSize.Y * draggedObject.CurrentScale.Y) / 2.0f; hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = newPotentialPos, LeftX = newPotentialPos.X - scaledHalfWidth, RightX = newPotentialPos.X + scaledHalfWidth, TopY = newPotentialPos.Y - scaledHalfHeight, BottomY = newPotentialPos.Y + scaledHalfHeight }; } else { hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = newPotentialPos, LeftX = newPotentialPos.X, RightX = newPotentialPos.X, TopY = newPotentialPos.Y, BottomY = newPotentialPos.Y }; } List<PlacedObject> allObjectsInScene = GetAllPlacedObjects(); finalVisualPos = ApplySnappingRules( newPotentialPos, hypotheticalSnapPoints, draggedObject, allObjectsInScene, out bool xObjSnapped, out float xObjSnapLine, out bool yObjSnapped, out float yObjSnapLine, out bool xGridSn,   out float xGridLine, out bool yGridSn,   out float yGridLine ); if (snapFeedbackDisplayInstance != null) { snapFeedbackDisplayInstance.ClearLines(); Rect2 vpRect = GetViewportRectForSnapLines(); if (xObjSnapped) { snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xObjSnapLine, vpRect.Position.Y), new Vector2(xObjSnapLine, vpRect.End.Y), Colors.Aqua); } if (yObjSnapped) { snapFeedbackDisplayInstance.AddSnapLine(new Vector2(vpRect.Position.X, yObjSnapLine), new Vector2(vpRect.End.X, yObjSnapLine), Colors.Aqua); } if (xGridSn) { snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xGridLine, vpRect.Position.Y), new Vector2(xGridLine, vpRect.End.Y), Colors.LightGreen); } if (yGridSn) { snapFeedbackDisplayInstance.AddSnapLine(new Vector2(vpRect.Position.X, yGridLine), new Vector2(vpRect.End.X, yGridLine), Colors.LightGreen); } } } draggedObject.GlobalPosition = finalVisualPos; if (!isUpdatingPanelFromSelection) { UpdateObjectPropertiesPanel(); } GetViewport().SetInputAsHandled(); } } else if ((@event.IsActionPressed("delete_object_action") || (@event is InputEventKey keyDel && keyDel.Keycode == Key.Delete && keyDel.Pressed && !keyDel.IsEcho())) && currentlySelectedObjects.Count > 0 ) { if (GetViewport().GuiGetFocusOwner() != null && !(GetViewport().GuiGetFocusOwner() is Button)) return; _HandleDeleteSelectedObjects(); GetViewport().SetInputAsHandled(); } } }
	private void SetupFileDialogs() { saveFileDialog = new FileDialog(); saveFileDialog.Title = "Save Map"; saveFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile; saveFileDialog.AddFilter("*.map ; Map Files"); saveFileDialog.FileSelected += _OnSaveFileDialogFileSelected; saveFileDialog.Canceled += _OnSaveFileDialogCancelled; AddChild(saveFileDialog); loadFileDialog = new FileDialog(); loadFileDialog.Title = "Load Map"; loadFileDialog.FileMode = FileDialog.FileModeEnum.OpenFile; loadFileDialog.AddFilter("*.map ; Map Files"); loadFileDialog.FileSelected += OnLoadFileDialogFileSelected; AddChild(loadFileDialog); }
	private void _OnSaveFileDialogCancelled() { saveAsTcs?.TrySetResult(null); }
	private void PopulateToolbar() { foreach (Node child in toolbarPanel.GetChildren()) { toolbarPanel.RemoveChild(child); child.QueueFree(); } manageAssetsButton = new Button(); manageAssetsButton.Name = "ManageAssetsButton"; manageAssetsButton.Text = "Manage Assets"; manageAssetsButton.Pressed += _OnManageAssetsButtonPressed; toolbarPanel.AddChild(manageAssetsButton); scatterModeCheckBox = new CheckBox(); scatterModeCheckBox.Name = "ScatterModeCheckBox"; scatterModeCheckBox.Text = "Scatter (Rand Rot)"; scatterModeCheckBox.ButtonPressed = false; toolbarPanel.AddChild(scatterModeCheckBox); Button selectObjectToolButton = new Button(); selectObjectToolButton.Name = "SelectObjectToolButton"; selectObjectToolButton.Text = "Select Obj"; selectObjectToolButton.Pressed += _OnSelectObjectToolButtonPressed; toolbarPanel.AddChild(selectObjectToolButton); eraserButton = new Button(); eraserButton.Name = "EraserButton"; eraserButton.Text = "Eraser"; if (assetManager != null) { AssetData eraserAssetForIcon = assetManager.GetAsset("Eraser"); if (eraserAssetForIcon != null && eraserAssetForIcon.PreviewTexture != null) eraserButton.Icon = eraserAssetForIcon.PreviewTexture; } eraserButton.Pressed += _OnEraserButtonPressed; toolbarPanel.AddChild(eraserButton); undoButton = new Button(); undoButton.Name = "UndoButton"; undoButton.Text = "Undo"; undoButton.Disabled = true; undoButton.Pressed += _OnUndoButtonPressed; toolbarPanel.AddChild(undoButton); redoButton = new Button(); redoButton.Name = "RedoButton"; redoButton.Text = "Redo"; redoButton.Disabled = true; redoButton.Pressed += _OnRedoButtonPressed; toolbarPanel.AddChild(redoButton); Button newMapButton = new Button(); newMapButton.Text = "New Map"; newMapButton.Pressed += _OnNewMapButtonPressed; toolbarPanel.AddChild(newMapButton); Button saveButton = new Button(); saveButton.Text = "Save Map"; saveButton.Pressed += _OnSaveMapButtonPressed; toolbarPanel.AddChild(saveButton); Button loadButton = new Button(); loadButton.Text = "Load Map"; loadButton.Pressed += _OnLoadMapButtonPressed; toolbarPanel.AddChild(loadButton); Button appSettingsButton = new Button(); appSettingsButton.Name = "AppSettingsButton"; appSettingsButton.Text = "App Settings"; appSettingsButton.Pressed += _OnAppSettingsButtonPressed; toolbarPanel.AddChild(appSettingsButton); Button startRoomModeButton = new Button(); startRoomModeButton.Text = "Start Room"; startRoomModeButton.Pressed += OnStartRoomModePressed; toolbarPanel.AddChild(startRoomModeButton); Button tileModeButton = new Button(); tileModeButton.Text = "Back to Tile Mode"; tileModeButton.Pressed += OnTileModePressed; toolbarPanel.AddChild(tileModeButton); if (assetManager != null) { List<AssetData> assets = assetManager.GetAllAssets(); if (assets != null && assets.Count > 0) { foreach (var asset in assets) { Button button = new Button(); button.Name = "AssetButton_" + asset.Name; button.Text = asset.Name; if (asset.PreviewTexture != null) button.Icon = asset.PreviewTexture; button.Pressed += () => OnToolbarButtonPressed(asset.Name); toolbarPanel.AddChild(button); } } } else { GD.PrintErr("AssetManager null in PopulateToolbar."); } }
	private void _OnManageAssetsButtonPressed() { if (assetLibraryPanelInstance != null) assetLibraryPanelInstance.Visible = !assetLibraryPanelInstance.Visible; else GD.PrintErr("AssetLibraryPanelInstance null."); }
	private void _OnUndoButtonPressed() { if (undoRedoManagerInstance != null) undoRedoManagerInstance.Undo(); SetMapDirty(true); }
	private void _OnRedoButtonPressed() { if (undoRedoManagerInstance != null) undoRedoManagerInstance.Redo(); SetMapDirty(true); }
	private void _OnUndoStackChanged(bool canUndo)  { if (undoButton != null) undoButton.Disabled = !canUndo; if (editMenuPopup != null && undoMenuItemIndex != -1) { editMenuPopup.SetItemDisabled(undoMenuItemIndex, !canUndo); } }
	private void _OnRedoStackChanged(bool canRedo)  { if (redoButton != null) redoButton.Disabled = !canRedo; if (editMenuPopup != null && redoMenuItemIndex != -1) { editMenuPopup.SetItemDisabled(redoMenuItemIndex, !canRedo); } }
	private void _OnSelectObjectToolButtonPressed() { ChangeDrawingMode(DrawingMode.SelectObject); }
	private void _OnEraserButtonPressed() { if (assetManager == null || tileDrawer == null) { GD.PrintErr("Eraser dependencies null."); return; } AssetData eraserAsset = assetManager.GetAsset("Eraser"); if (eraserAsset != null) { tileDrawer.SetCurrentAsset(eraserAsset); if (currentDrawingMode != DrawingMode.Tile) ChangeDrawingMode(DrawingMode.Tile); } else { GD.PrintErr("Eraser asset not found!"); } }
	private void OnStartRoomModePressed() { ChangeDrawingMode(DrawingMode.Room); }
	private void OnTileModePressed() { ChangeDrawingMode(DrawingMode.Tile); }
	private void OnToolbarButtonPressed(string assetName) { AssetData asset = assetManager.GetAsset(assetName); if (asset != null && tileDrawer != null) tileDrawer.SetCurrentAsset(asset); else { if (asset == null) GD.PrintErr($"Asset '{assetName}' not found."); if (tileDrawer == null) GD.PrintErr("TileDrawer null."); } }
	private async void _OnSaveMapButtonPressed() { if (string.IsNullOrEmpty(currentMapFilePath) || Input.IsKeyPressed(Key.Shift)) { string chosenPath = await _SaveMapAsAsync(); if (chosenPath == null) { GD.Print("Save As was cancelled."); } } else { _SaveMap(currentMapFilePath); } }
	private async void _OnNewMapButtonPressed() { UnsavedChangesDialogResult result = await ShowUnsavedChangesDialog("creating a new map"); if (result == UnsavedChangesDialogResult.Cancel) return; GD.Print("Creating new map..."); if(tileDrawer != null) tileDrawer.Clear(); Node placedObjectsRoot = GetNodeOrNull("PlacedObjectsRoot"); if (placedObjectsRoot != null) { foreach (Node child in placedObjectsRoot.GetChildren()) child.QueueFree(); } if (undoRedoManagerInstance != null) undoRedoManagerInstance.ClearHistory(); SetCurrentMapFilePath(null); SetMapDirty(false); if (layersPanelController != null) layersPanelController.RefreshLayerList(); UpdateObjectPropertiesPanel(); GD.Print("New map created."); }
	private async void _OnLoadMapButtonPressed() { UnsavedChangesDialogResult result = await ShowUnsavedChangesDialog("loading another map"); if (result == UnsavedChangesDialogResult.Cancel) return; if (loadFileDialog != null) loadFileDialog.PopupCentered(); else GD.PrintErr("LoadFileDialog is null."); }
	private void _OnAppSettingsButtonPressed() { if (appSettingsPanelInstance != null) { if (appSettingsPanelInstance.Visible) appSettingsPanelInstance.Hide(); else appSettingsPanelInstance.Show(); } }
	private void _OnGlobalAssetPathChanged() { GD.Print("MainScene: Detected GlobalAssetPathChangedAndRefreshed. Refreshing relevant UI."); PopulateToolbar(); UpdateObjectPropertiesPanel(); if (layersPanelController != null) layersPanelController.RefreshLayerList(); }
	private void _OnAlignmentButtonPressed(AlignmentMode mode) { if (currentlySelectedObjects.Count < 2) { GD.Print("Alignment requires at least two objects to be selected."); return; } if (undoRedoManagerInstance == null || tileDrawer == null) { GD.PrintErr("MainScene: UndoRedoManager or TileDrawer not available for AlignObjectsAction."); return; } List<PlacedObject> selectionCopy = new List<PlacedObject>(currentlySelectedObjects); AlignContext currentAlignContext = AlignContext.ToFirstSelected; if (alignContextOptionButton != null) currentAlignContext = (AlignContext)alignContextOptionButton.Selected; AlignObjectsAction action = new AlignObjectsAction(selectionCopy, mode, currentAlignContext); if (action.IsEmpty()) { GD.Print("Objects are already aligned in the selected mode. No action taken."); return; } action.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(action); GD.Print($"Alignment action '{mode}' performed and recorded."); }
	private void _OnDistributeObjectsButtonPressed(DistributionMode mode) { if (currentlySelectedObjects.Count < 3) { GD.Print("Distribution requires at least three objects to be selected."); return; } if (undoRedoManagerInstance == null || tileDrawer == null) { GD.PrintErr("MainScene: UndoRedoManager or TileDrawer not available for DistributeObjectsAction."); return; } List<PlacedObject> selectionCopy = new List<PlacedObject>(currentlySelectedObjects); AlignContext currentAlignContext = AlignContext.ToFirstSelected; if (alignContextOptionButton != null) currentAlignContext = (AlignContext)alignContextOptionButton.Selected; else GD.PrintWarn("MainScene: alignContextOptionButton is null. Defaulting to AlignContext.ToFirstSelected."); AlignObjectsAction action = new AlignObjectsAction(selectionCopy, mode, currentAlignContext); if (action.IsEmpty()) { GD.Print("Objects are already distributed in the selected mode or not enough objects to distribute between extremes. No action taken."); return; } action.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(action); GD.Print($"Distribution action '{mode}' performed and recorded."); }
	private void _OnSaveFileDialogFileSelected(string filePath) { _SaveMap(filePath); saveAsTcs?.TrySetResult(filePath); }
	private void OnLoadFileDialogFileSelected(string filePath) { MapSaverLoader.LoadMap(tileDrawer, filePath); RefreshLayerList(); UpdateObjectPropertiesPanel(); SetCurrentMapFilePath(filePath); SetMapDirty(false); if (undoRedoManagerInstance != null) undoRedoManagerInstance.ClearHistory(); DeselectAllObjects(); }
	public DrawingMode GetCurrentDrawingMode() { return currentDrawingMode; }
	public void ChangeDrawingMode(DrawingMode newMode) { if (currentDrawingMode == newMode) return; string oldModeName = currentDrawingMode.ToString(); if (currentDrawingMode == DrawingMode.SelectObject && newMode != DrawingMode.SelectObject) DeselectAllObjects(); currentDrawingMode = newMode; GD.Print($"MainScene: DrawingMode changed from {oldModeName} to {newMode}"); EmitSignal(nameof(DrawingModeChanged), (long)currentDrawingMode);  }
	private void RefreshLayerList() { var layersPanelCtrl = GetNodeOrNull<LayersPanelController>("LayersPanel"); layersPanelCtrl?.RefreshLayerList(); }
	private Vector2 TrySnapPosition( Vector2 currentPotentialPivotPosition, PlacedObject.GlobalSnapPoints activeObjectSnapPointsAtPotentialPosition, List<PlacedObject> allOtherPlacedObjects, out bool xWasSnapped, out float xSnapLineGuideCoord, out bool yWasSnapped, out float ySnapLineGuideCoord) { xWasSnapped = false; xSnapLineGuideCoord = 0f; yWasSnapped = false; ySnapLineGuideCoord = 0f; if (globalSettings == null) { GD.PrintErr("TrySnapPosition: GlobalSettings not initialized!"); return currentPotentialPivotPosition; } if (!globalSettings.IsObjectSnapEnabled || allOtherPlacedObjects == null || allOtherPlacedObjects.Count == 0) return currentPotentialPivotPosition; Vector2 finalSnappedPosition = currentPotentialPivotPosition; float bestSnapDeltaX = float.MaxValue; float bestSnapDeltaY = float.MaxValue; float snapDistanceThreshold = globalSettings.ObjectSnapDistanceWorld; float currentBestXTargetCoord = 0f; float currentBestYTargetCoord = 0f; foreach (PlacedObject otherObject in allOtherPlacedObjects) { if (otherObject == null || !GodotObject.IsInstanceValid(otherObject)) continue; PlacedObject.GlobalSnapPoints otherSnapPoints = otherObject.GetCurrentGlobalSnapPoints(); float currentDelta; if (globalSettings.ObjectSnapToPivots) { currentDelta = otherSnapPoints.VerticalCenterX - activeObjectSnapPointsAtPotentialPosition.VerticalCenterX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.VerticalCenterX;} } if (globalSettings.ObjectSnapToEdges) { currentDelta = otherSnapPoints.LeftX - activeObjectSnapPointsAtPotentialPosition.LeftX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.LeftX;} currentDelta = otherSnapPoints.RightX - activeObjectSnapPointsAtPotentialPosition.LeftX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.RightX;} currentDelta = otherSnapPoints.LeftX - activeObjectSnapPointsAtPotentialPosition.RightX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.LeftX;} currentDelta = otherSnapPoints.RightX - activeObjectSnapPointsAtPotentialPosition.RightX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.RightX;} currentDelta = otherSnapPoints.LeftX - activeObjectSnapPointsAtPotentialPosition.VerticalCenterX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.LeftX;} currentDelta = otherSnapPoints.RightX - activeObjectSnapPointsAtPotentialPosition.VerticalCenterX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.RightX;} currentDelta = otherSnapPoints.VerticalCenterX - activeObjectSnapPointsAtPotentialPosition.LeftX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.VerticalCenterX;} currentDelta = otherSnapPoints.VerticalCenterX - activeObjectSnapPointsAtPotentialPosition.RightX; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaX)) { bestSnapDeltaX = currentDelta; currentBestXTargetCoord = otherSnapPoints.VerticalCenterX;} } if (globalSettings.ObjectSnapToPivots) { currentDelta = otherSnapPoints.HorizontalCenterY - activeObjectSnapPointsAtPotentialPosition.HorizontalCenterY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.HorizontalCenterY;} } if (globalSettings.ObjectSnapToEdges) { currentDelta = otherSnapPoints.TopY - activeObjectSnapPointsAtPotentialPosition.TopY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.TopY;} currentDelta = otherSnapPoints.BottomY - activeObjectSnapPointsAtPotentialPosition.TopY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.BottomY;} currentDelta = otherSnapPoints.TopY - activeObjectSnapPointsAtPotentialPosition.BottomY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.TopY;} currentDelta = otherSnapPoints.BottomY - activeObjectSnapPointsAtPotentialPosition.BottomY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.BottomY;} currentDelta = otherSnapPoints.TopY - activeObjectSnapPointsAtPotentialPosition.HorizontalCenterY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.TopY;} currentDelta = otherSnapPoints.BottomY - activeObjectSnapPointsAtPotentialPosition.HorizontalCenterY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.BottomY;} currentDelta = otherSnapPoints.HorizontalCenterY - activeObjectSnapPointsAtPotentialPosition.TopY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.HorizontalCenterY;} currentDelta = otherSnapPoints.HorizontalCenterY - activeObjectSnapPointsAtPotentialPosition.BottomY; if (Mathf.Abs(currentDelta) < snapDistanceThreshold && Mathf.Abs(currentDelta) < Mathf.Abs(bestSnapDeltaY)) { bestSnapDeltaY = currentDelta; currentBestYTargetCoord = otherSnapPoints.HorizontalCenterY;} } } if (Mathf.Abs(bestSnapDeltaX) < snapDistanceThreshold) { finalSnappedPosition.X += bestSnapDeltaX; xWasSnapped = true; xSnapLineGuideCoord = currentBestXTargetCoord; } if (Mathf.Abs(bestSnapDeltaY) < snapDistanceThreshold) { finalSnappedPosition.Y += bestSnapDeltaY; yWasSnapped = true; ySnapLineGuideCoord = currentBestYTargetCoord; } return finalSnappedPosition; }
	private Vector2 ApplySnappingRules( Vector2 potentialPivotPosition, PlacedObject.GlobalSnapPoints activeObjHypotheticalSnapPoints, PlacedObject objectBeingMovedOrNull, List<PlacedObject> allPlacedObjectsInScene, out bool xObjectSnapped, out float xObjectSnapLineCoord, out bool yObjectSnapped, out float yObjectSnapLineCoord, out bool xGridSnapped,   out float xGridSnapLineCoord, out bool yGridSnapped,   out float yGridSnapLineCoord ) { xObjectSnapped = false; xObjectSnapLineCoord = 0f; yObjectSnapped = false; yObjectSnapLineCoord = 0f; xGridSnapped = false;   xGridSnapLineCoord = 0f; yGridSnapped = false;   yGridSnapLineCoord = 0f; Vector2 positionAfterObjectSnap = potentialPivotPosition; if (globalSettings == null) { GD.PrintErr("ApplySnappingRules: GlobalSettings not available!"); return potentialPivotPosition; } if (globalSettings.IsObjectSnapEnabled) { List<PlacedObject> otherObjects = new List<PlacedObject>(); if (allPlacedObjectsInScene != null) { foreach (PlacedObject po in allPlacedObjectsInScene) { if (po != objectBeingMovedOrNull) { otherObjects.Add(po); } } } if (otherObjects.Count > 0) { positionAfterObjectSnap = TrySnapPosition( potentialPivotPosition, activeObjHypotheticalSnapPoints, otherObjects, out xObjectSnapped, out xObjectSnapLineCoord, out yObjectSnapped, out yObjectSnapLineCoord ); } } Vector2 finalSnappedPosition = positionAfterObjectSnap; if (globalSettings.IsSnapToGridEnabled) { Vector2 originalPosForGridSnap = finalSnappedPosition; finalSnappedPosition = GlobalSettings.SnapPositionToGrid(originalPosForGridSnap, globalSettings.GridSize); if (!Mathf.IsEqualApprox(originalPosForGridSnap.X, finalSnappedPosition.X)) { xGridSnapped = true; xGridSnapLineCoord = finalSnappedPosition.X; } if (!Mathf.IsEqualApprox(originalPosForGridSnap.Y, finalSnappedPosition.Y)) { yGridSnapped = true; yGridSnapLineCoord = finalSnappedPosition.Y; } } return finalSnappedPosition; }
	private void _OnGizmoTranslationStarted(string axisOrType, PlacedObject targetObject) { if (targetObject != null && GodotObject.IsInstanceValid(targetObject)) { gizmoDragStartObjectPositionsForUndo_Translate.Clear(); gizmoDragStartObjectPositionsForUndo_Translate[targetObject] = targetObject.GlobalPosition; } else { gizmoDragStartObjectPositionsForUndo_Translate.Clear();  } }
	private void _OnGizmoTranslationUpdated(Vector2 rawNewDesiredPosition, PlacedObject targetObject) { if (targetObject == null || !GodotObject.IsInstanceValid(targetObject) || globalSettings == null || snapFeedbackDisplayInstance == null ) { snapFeedbackDisplayInstance?.ClearAllLines(); return;  } if (currentlySelectedObjects.Count != 1 || currentlySelectedObjects[0] != targetObject) { snapFeedbackDisplayInstance.ClearAllLines(); return; } Sprite2D sprite = targetObject.GetNodeOrNull<Sprite2D>("ObjectSprite"); PlacedObject.GlobalSnapPoints activeObjHypotheticalSnapPoints; if (sprite != null && sprite.Texture != null) { Vector2 textureSize = sprite.Texture.GetSize(); float scaledHalfWidth = (textureSize.X * targetObject.CurrentScale.X) / 2.0f; float scaledHalfHeight = (textureSize.Y * targetObject.CurrentScale.Y) / 2.0f; activeObjHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = rawNewDesiredPosition, LeftX = rawNewDesiredPosition.X - scaledHalfWidth, RightX = rawNewDesiredPosition.X + scaledHalfWidth, TopY = rawNewDesiredPosition.Y - scaledHalfHeight, BottomY = rawNewDesiredPosition.Y + scaledHalfHeight }; } else { activeObjHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = rawNewDesiredPosition, LeftX = rawNewDesiredPosition.X, RightX = rawNewDesiredPosition.X, TopY = rawNewDesiredPosition.Y, BottomY = rawNewDesiredPosition.Y }; } List<PlacedObject> allObjectsInScene = GetAllPlacedObjects(); Vector2 finalSnappedPosition = ApplySnappingRules( rawNewDesiredPosition, activeObjHypotheticalSnapPoints, targetObject, allObjectsInScene, out bool xObjSnapped, out float xObjSnapLine, out bool yObjSnapped, out float yObjSnapLine, out bool xGridSnapped, out float xGridSnapLine, out bool yGridSnapped, out float yGridSnapLine ); targetObject.GlobalPosition = finalSnappedPosition; if (combinedTransformGizmoInstance != null) combinedTransformGizmoInstance.GlobalPosition = finalSnappedPosition; snapFeedbackDisplayInstance.ClearAllLines(); Rect2 viewRect = GetViewportRectForSnapLines(); if (xObjSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xObjSnapLine, viewRect.Position.Y), new Vector2(xObjSnapLine, viewRect.End.Y), Colors.Aqua); if (yObjSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(viewRect.Position.X, yObjSnapLine), new Vector2(viewRect.End.X, yObjSnapLine), Colors.Aqua); if (xGridSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(xGridSnapLine, viewRect.Position.Y), new Vector2(xGridSnapLine, viewRect.End.Y), Colors.LightGreen); if (yGridSnapped) snapFeedbackDisplayInstance.AddSnapLine(new Vector2(viewRect.Position.X, yGridSnapLine), new Vector2(viewRect.End.X, yGridSnapLine), Colors.LightGreen); if (!isUpdatingPanelFromSelection && objectPropertiesPanelInstance != null && objectPropertiesPanelInstance.Visible) { objectPropertiesPanelInstance.UpdateDisplayedPositionFromSnap(finalSnappedPosition); } }
	private void _OnGizmoTranslationFinished(Vector2 finalAppliedPositionByGizmo, PlacedObject targetObject) { if (targetObject == null || !GodotObject.IsInstanceValid(targetObject) || globalSettings == null) return; if (!gizmoDragStartObjectPositionsForUndo_Translate.TryGetValue(targetObject, out Vector2 originalPositionAtDragStart)) { GD.PrintErr("MainScene: Could not find original drag start position for undo. GizmoTranslationFinished aborted."); snapFeedbackDisplayInstance?.ClearAllLines(); UpdateObjectPropertiesPanel(); return; } Sprite2D sprite = targetObject.GetNodeOrNull<Sprite2D>("ObjectSprite"); PlacedObject.GlobalSnapPoints finalHypotheticalSnapPoints; if (sprite != null && sprite.Texture != null) { Vector2 textureSize = sprite.Texture.GetSize(); float scaledHalfWidth = (textureSize.X * targetObject.CurrentScale.X) / 2.0f; float scaledHalfHeight = (textureSize.Y * targetObject.CurrentScale.Y) / 2.0f; finalHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = finalAppliedPositionByGizmo, LeftX = finalAppliedPositionByGizmo.X - scaledHalfWidth, RightX = finalAppliedPositionByGizmo.X + scaledHalfWidth, TopY = finalAppliedPositionByGizmo.Y - scaledHalfHeight, BottomY = finalAppliedPositionByGizmo.Y + scaledHalfHeight }; } else { finalHypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = finalAppliedPositionByGizmo, LeftX = finalAppliedPositionByGizmo.X, RightX = finalAppliedPositionByGizmo.X, TopY = finalAppliedPositionByGizmo.Y, BottomY = finalAppliedPositionByGizmo.Y }; } List<PlacedObject> allObjectsInScene = GetAllPlacedObjects(); Vector2 trueFinalSnappedPosition = ApplySnappingRules( finalAppliedPositionByGizmo, finalHypotheticalSnapPoints, targetObject, allObjectsInScene, out bool _, out float _, out bool _, out float _, out bool _, out float _, out bool _, out float _ ); targetObject.GlobalPosition = trueFinalSnappedPosition; if (combinedTransformGizmoInstance != null) combinedTransformGizmoInstance.GlobalPosition = trueFinalSnappedPosition; if (!originalPositionAtDragStart.IsEqualApprox(trueFinalSnappedPosition)) { if (undoRedoManagerInstance != null && tileDrawer != null) { MoveObjectAction action = new MoveObjectAction(targetObject, originalPositionAtDragStart, trueFinalSnappedPosition); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true);  } } gizmoDragStartObjectPositionsForUndo_Translate.Clear(); UpdateObjectPropertiesPanel(); snapFeedbackDisplayInstance?.ClearAllLines();  }
    private Rect2 GetViewportRectForSnapLines() { Camera2D mapCam = GetNodeOrNull<Camera2D>("MapCamera"); if (mapCam != null) return mapCam.GetVisibleRect(); return GetViewportRect();  }
	private List<PlacedObject> GetAllPlacedObjects() { List<PlacedObject> allObjects = new List<PlacedObject>(); Node placedObjectsRootNode = GetNodeOrNull("PlacedObjectsRoot"); if (placedObjectsRootNode != null) { foreach (Node child in placedObjectsRootNode.GetChildren()) { if (child is PlacedObject po) { allObjects.Add(po); } } } return allObjects; }
	private void OnPanelPositionChangeRequested(NodePath objectPath, Vector2 newRawPosition) { PlacedObject targetObject = GetNodeOrNull<PlacedObject>(objectPath); if (targetObject == null || !GodotObject.IsInstanceValid(targetObject)) { GD.PrintErr($"MainScene: Target object not found or invalid at path: {objectPath}"); return; } Vector2 originalPosition = targetObject.GlobalPosition; Sprite2D sprite = targetObject.GetNodeOrNull<Sprite2D>("ObjectSprite"); PlacedObject.GlobalSnapPoints hypotheticalSnapPoints; if (sprite != null && sprite.Texture != null) { Vector2 textureSize = sprite.Texture.GetSize(); float scaledHalfWidth = (textureSize.X * targetObject.CurrentScale.X) / 2.0f; float scaledHalfHeight = (textureSize.Y * targetObject.CurrentScale.Y) / 2.0f; hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = newRawPosition, LeftX = newRawPosition.X - scaledHalfWidth, RightX = newRawPosition.X + scaledHalfWidth, TopY = newRawPosition.Y - scaledHalfHeight, BottomY = newRawPosition.Y + scaledHalfHeight }; } else { hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { Center = newRawPosition, LeftX = newRawPosition.X, RightX = newRawPosition.X, TopY = newRawPosition.Y, BottomY = newRawPosition.Y }; } List<PlacedObject> allObjectsInScene = GetAllPlacedObjects(); Vector2 finalSnappedPosition = ApplySnappingRules( newRawPosition, hypotheticalSnapPoints, targetObject, allObjectsInScene, out bool _, out float _, out bool _, out float _, out bool _, out float _, out bool _, out float _ ); if (!originalPosition.IsEqualApprox(finalSnappedPosition)) { targetObject.GlobalPosition = finalSnappedPosition; if (combinedTransformGizmoInstance != null && combinedTransformGizmoInstance.Visible && currentlySelectedObjects.Count == 1 && currentlySelectedObjects[0] == targetObject) { combinedTransformGizmoInstance.GlobalPosition = finalSnappedPosition; } if (undoRedoManagerInstance != null) { MoveObjectAction action = new MoveObjectAction(targetObject, originalPosition, finalSnappedPosition); undoRedoManagerInstance.RecordAction(action); SetMapDirty(true); } if (!newRawPosition.IsEqualApprox(finalSnappedPosition) && objectPropertiesPanelInstance != null) { objectPropertiesPanelInstance.UpdateDisplayedPositionFromSnap(finalSnappedPosition); } UpdateObjectPropertiesPanel(); } else { if (!newRawPosition.IsEqualApprox(originalPosition) && objectPropertiesPanelInstance != null) { objectPropertiesPanelInstance.UpdateDisplayedPositionFromSnap(originalPosition); } } }
	private void _OnActualDrawingModeChanged(DrawingMode newModeEnumValue) { if (toolsMenuPopup == null || toolMenuItemIndices == null) return; GD.Print($"MainScene: Actual drawing mode changed to {newModeEnumValue}. Updating Tools menu checkmarks."); foreach (KeyValuePair<DrawingMode, int> entry in toolMenuItemIndices) { DrawingMode modeForThisItem = entry.Key; int itemIndexInMenu = entry.Value; if (itemIndexInMenu >= 0 && itemIndexInMenu < toolsMenuPopup.ItemCount) { toolsMenuPopup.SetItemChecked(itemIndexInMenu, modeForThisItem == newModeEnumValue); } } }
	public SnapFeedbackDisplay GetSnapFeedbackDisplay() { return snapFeedbackDisplayInstance; }
	private void _OnFileMenuItemPressed(long id) { FileMenuItemId itemId = (FileMenuItemId)id; switch (itemId) { case FileMenuItemId.New: _OnNewMapButtonPressed(); break; case FileMenuItemId.Open: _OnLoadMapButtonPressed(); break; case FileMenuItemId.Save: _OnSaveMapButtonPressed(); break; case FileMenuItemId.SaveAs: _OnSaveMapAsButtonPressed(); break; case FileMenuItemId.AppSettings: _OnAppSettingsButtonPressed(); break; case FileMenuItemId.Quit: _RequestQuitApplication(); break; } }
	private async void _OnSaveMapAsButtonPressed() { string chosenPath = await _SaveMapAsAsync(); if (chosenPath == null) { GD.Print("Save As (from menu) was cancelled."); } else { GD.Print($"Map saved via 'Save As...' to {chosenPath}"); } }
	private async void _RequestQuitApplication() { UnsavedChangesDialogResult result = await ShowUnsavedChangesDialog("quitting the application"); if (result != UnsavedChangesDialogResult.Cancel) { GetTree().Quit(); } else { GD.Print("Application close cancelled by user."); } }
	private void _OnEditMenuItemPressed(long id) { EditMenuItemId itemId = (EditMenuItemId)id; if (undoRedoManagerInstance == null) return; switch (itemId) { case EditMenuItemId.Undo: undoRedoManagerInstance.Undo(); break; case EditMenuItemId.Redo: undoRedoManagerInstance.Redo(); break; } }
	private void _OnViewMenuItemPressed(long id) { ViewMenuItemId itemId = (ViewMenuItemId)id; if (globalSettings == null) return; switch (itemId) { case ViewMenuItemId.ToggleGridSnap: globalSettings.IsSnapToGridEnabled = !globalSettings.IsSnapToGridEnabled; break; case ViewMenuItemId.ToggleObjectSnap: globalSettings.IsObjectSnapEnabled = !globalSettings.IsObjectSnapEnabled; break; } }
	private void _OnGridSettingsChangedForViewMenu() { if (viewMenuPopup != null && toggleGridSnapMenuItemIndex != -1 && globalSettings != null) { viewMenuPopup.SetItemChecked(toggleGridSnapMenuItemIndex, globalSettings.IsSnapToGridEnabled); } }
	private void _OnObjectSnapSettingsChangedForViewMenu() { if (viewMenuPopup != null && toggleObjectSnapMenuItemIndex != -1 && globalSettings != null) { viewMenuPopup.SetItemChecked(toggleObjectSnapMenuItemIndex, globalSettings.IsObjectSnapEnabled); } }
	private string GetToolModeUserFriendlyName(DrawingMode mode) { switch (mode) { case DrawingMode.Tile: return "Tile Drawing"; case DrawingMode.Room: return "Room Drawing"; case DrawingMode.ObjectPlacement: return "Place Object"; case DrawingMode.SelectObject: return "Select/Edit Object"; default: return mode.ToString(); } }
	private void _OnToolsMenuItemPressed(long id) { DrawingMode selectedMode = (DrawingMode)id; ChangeDrawingMode(selectedMode);  }

	// New handlers for signals from ObjectPropertiesPanel for Rotation and Scale
	private void OnPanelRotationChangeRequested(NodePath objectPath, float newRawRotationDegrees)
	{
		PlacedObject targetObject = GetNodeOrNull<PlacedObject>(objectPath);
		if (targetObject == null || !GodotObject.IsInstanceValid(targetObject) || currentlySelectedObjects.Count != 1 || currentlySelectedObjects[0] != targetObject)
		{
			GD.PrintErr($"MainScene: Target object for rotation change not valid or not the uniquely selected object. Path: {objectPath}");
			if (objectPropertiesPanelInstance != null) objectPropertiesPanelInstance.DisplayControls(currentlySelectedObjects, this); // Revert panel
			return;
		}

		float originalRotation = targetObject.RotationDegrees;
		float finalRotation = Mathf.Wrap(newRawRotationDegrees, -360f, 360f);

		if (!Mathf.IsEqualApprox(originalRotation, finalRotation))
		{
			targetObject.RotationDegrees = finalRotation;

			if (rotationGizmoInstance != null && rotationGizmoInstance.Visible) rotationGizmoInstance.UpdateGizmoVisuals(targetObject);
			if (combinedTransformGizmoInstance != null && combinedTransformGizmoInstance.Visible) combinedTransformGizmoInstance.UpdateGizmoHandles();

			if (undoRedoManagerInstance != null)
			{
				RotateObjectAction action = new RotateObjectAction(targetObject, originalRotation, finalRotation);
				undoRedoManagerInstance.RecordAction(action);
				SetMapDirty(true);
			} else {
				UpdateObjectPropertiesPanel();
			}
		}

		if (objectPropertiesPanelInstance != null) objectPropertiesPanelInstance.UpdateDisplayedRotation(targetObject.RotationDegrees);
	}

	private void OnPanelScaleChangeRequested(NodePath objectPath, Vector2 newRawScale)
	{
		PlacedObject targetObject = GetNodeOrNull<PlacedObject>(objectPath);
		if (targetObject == null || !GodotObject.IsInstanceValid(targetObject) || currentlySelectedObjects.Count != 1 || currentlySelectedObjects[0] != targetObject)
		{
			GD.PrintErr($"MainScene: Target object for scale change not valid or not the uniquely selected object. Path: {objectPath}");
			if (objectPropertiesPanelInstance != null) objectPropertiesPanelInstance.DisplayControls(currentlySelectedObjects, this); // Revert panel
			return;
		}

		Vector2 originalScale = targetObject.Scale;
		Vector2 finalScale = newRawScale;

		if (finalScale.X < 0.01f) finalScale.X = 0.01f;
		if (finalScale.Y < 0.01f) finalScale.Y = 0.01f;

		if (!originalScale.IsEqualApprox(finalScale))
		{
			targetObject.Scale = finalScale;

            if (scaleGizmoInstance != null && scaleGizmoInstance.Visible) scaleGizmoInstance.ShowForTarget(targetObject);
			if (combinedTransformGizmoInstance != null && combinedTransformGizmoInstance.Visible) combinedTransformGizmoInstance.UpdateGizmoHandles();

			if (undoRedoManagerInstance != null)
			{
				ScaleObjectAction action = new ScaleObjectAction(targetObject, originalScale, finalScale);
				undoRedoManagerInstance.RecordAction(action);
				SetMapDirty(true);
			} else {
				UpdateObjectPropertiesPanel();
			}
		}
		if (objectPropertiesPanelInstance != null) objectPropertiesPanelInstance.UpdateDisplayedScale(targetObject.Scale);
	}
}
