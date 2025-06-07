using Godot;
using System.Collections.Generic;
using System.Linq; // For Count() on IEnumerable

public partial class MainScene : Control
{
	[Signal] public delegate void DrawingModeChangedEventHandler(DrawingMode newMode);

	public enum DrawingMode { Tile, Room, ObjectPlacement, SelectObject }
	private DrawingMode currentDrawingMode = DrawingMode.Tile;

	// Core Components
	private AssetManager assetManager;
	private TileDrawer tileDrawer;
	private UndoRedoManager undoRedoManagerInstance;

	// UI Panels & Controls
	private Panel toolbarPanel;
	private AssetLibraryPanel assetLibraryPanelInstance;
	private PanelContainer objectPropertiesPanel;

	// Toolbar Buttons
	private Button manageAssetsButton;
	private CheckBox scatterModeCheckBox;
	private Button undoButton;
	private Button redoButton;
	private Button eraserButton;

	// Object Properties Panel Controls
	private Label assetNameLabelInProps;
	private SpinBox rotationSpinBoxProp;
	private SpinBox scaleXSpinBoxProp;
	private SpinBox scaleYSpinBoxProp;
	private Button deleteObjectButtonFromPanelProp;

	// File Dialogs
	private FileDialog saveFileDialog;
	private FileDialog loadFileDialog;

	// State
	private List<PlacedObject> currentlySelectedObjects = new List<PlacedObject>();
	private Dictionary<PlacedObject, Color> originalModulations = new Dictionary<PlacedObject, Color>();
	private bool isDraggingObject = false;
	private Vector2 dragStartMousePosition;
	private Vector2 dragStartObjectOriginalPosition;
	private bool isUpdatingPanelFromSelection = false;

	// Gizmos
	private RotationGizmo rotationGizmoInstance;
	private const string RotationGizmoScenePath = "res://ui/gizmos/RotationGizmo.tscn";
	private float gizmoDragStartRotationForUndo; // To store rotation when gizmo drag begins


	public override void _Ready()
	{
		assetManager = GetNode<AssetManager>("/root/AssetManager");
		undoRedoManagerInstance = GetNode<UndoRedoManager>("/root/UndoRedoManager");

		if (undoRedoManagerInstance != null)
		{
			undoRedoManagerInstance.UndoStackChanged += _OnUndoStackChanged;
			undoRedoManagerInstance.RedoStackChanged += _OnRedoStackChanged;
			if (!undoRedoManagerInstance.IsConnected(UndoRedoManager.SignalName.HistoryChanged, Callable.From(_OnUndoRedoHistoryChanged)))
			{
				undoRedoManagerInstance.Connect(UndoRedoManager.SignalName.HistoryChanged, Callable.From(_OnUndoRedoHistoryChanged));
			}
		} else { GD.PrintErr("MainScene: UndoRedoManager singleton not found!");}


		var tileMapNodes = GetTree().GetNodesInGroup("TileMapGroup");
		if (tileMapNodes.Count > 0) tileDrawer = tileMapNodes[0] as TileDrawer;
		if (tileDrawer == null) tileDrawer = GetNode<TileDrawer>("TileMap");
		if (tileDrawer == null) { GD.PrintErr("MainScene: TileDrawer node not found! Critical error."); GetTree().Quit(); return; }

		toolbarPanel = GetNode<Panel>("ToolbarPanel");
		if (toolbarPanel == null) { GD.PrintErr("MainScene: ToolbarPanel not found! Critical error."); GetTree().Quit(); return; }

		assetLibraryPanelInstance = GetNode<AssetLibraryPanel>("AssetLibraryPanelInstance");
		if (assetLibraryPanelInstance != null)
		{
			assetLibraryPanelInstance.Visible = false;
			assetLibraryPanelInstance.AssetsChanged += _OnAssetsChanged;
		}
		else { GD.PrintErr("MainScene: AssetLibraryPanelInstance not found!"); }

		if (undoRedoManagerInstance != null) undoRedoManagerInstance.Initialize(tileDrawer);

		objectPropertiesPanel = GetNode<PanelContainer>("ObjectPropertiesPanelInstance");
		if (objectPropertiesPanel != null)
		{
			assetNameLabelInProps = objectPropertiesPanel.GetNode<Label>("PropertiesVBox/AssetNameHBox/AssetNameLabel");
			rotationSpinBoxProp = objectPropertiesPanel.GetNode<SpinBox>("PropertiesVBox/RotationHBox/RotationSpinBox");
			scaleXSpinBoxProp = objectPropertiesPanel.GetNode<SpinBox>("PropertiesVBox/ScaleXHBox/ScaleXSpinBox");
			scaleYSpinBoxProp = objectPropertiesPanel.GetNode<SpinBox>("PropertiesVBox/ScaleYHBox/ScaleYSpinBox");
			deleteObjectButtonFromPanelProp = objectPropertiesPanel.GetNode<Button>("PropertiesVBox/DeleteObjectButton");

			bool panelControlsValid = assetNameLabelInProps != null && rotationSpinBoxProp != null &&
									  scaleXSpinBoxProp != null && scaleYSpinBoxProp != null &&
									  deleteObjectButtonFromPanelProp != null;
			if (!panelControlsValid) { GD.PrintErr("MainScene: Failed to get all control nodes from ObjectPropertiesPanelInstance!"); }
			else {
				rotationSpinBoxProp.ValueChanged += _OnPropsRotationChanged;
				scaleXSpinBoxProp.ValueChanged += _OnPropsScaleXChanged;
				scaleYSpinBoxProp.ValueChanged += _OnPropsScaleYChanged;
				deleteObjectButtonFromPanelProp.Pressed += _OnPropsDeleteObjectPressed;
			}
			objectPropertiesPanel.Visible = false;
		}
		else { GD.PrintErr("MainScene: ObjectPropertiesPanelInstance not found!"); }

		// Instance and connect Rotation Gizmo
		PackedScene gizmoSceneLoader = ResourceLoader.Load<PackedScene>(RotationGizmoScenePath);
		if (gizmoSceneLoader != null)
		{
			Node instance = gizmoSceneLoader.Instantiate();
			if (instance is RotationGizmo rgi) {
				rotationGizmoInstance = rgi;
				// Parent to a CanvasLayer for top rendering, or directly if ZIndex is high enough
				var gizmoLayer = GetNode<CanvasLayer>("GizmoCanvasLayer"); // Assuming you add a CanvasLayer node
				if (gizmoLayer != null) gizmoLayer.AddChild(rotationGizmoInstance);
				else AddChild(rotationGizmoInstance); // Fallback: add to MainScene's root Control node

				rotationGizmoInstance.Visible = false;
				rotationGizmoInstance.RotationStarted += _OnGizmoRotationStarted;
				rotationGizmoInstance.RotationUpdated += _OnGizmoRotationUpdated;
				rotationGizmoInstance.RotationFinished += _OnGizmoRotationFinished;
				GD.Print("RotationGizmo instance created, added, and signals connected.");
			} else {
				GD.PrintErr($"Failed to cast instanced gizmo to RotationGizmo type. Path: {RotationGizmoScenePath}");
				instance?.QueueFree();
			}
		}
		else { GD.PrintErr($"Failed to load RotationGizmo scene from: {RotationGizmoScenePath}"); }

		PopulateToolbar();
		SetupFileDialogs();

		if (undoRedoManagerInstance != null)
		{
			_OnUndoStackChanged(undoRedoManagerInstance.CanUndo());
			_OnRedoStackChanged(undoRedoManagerInstance.CanRedo());
		}
		UpdateObjectPropertiesPanel();
	}

	private void _OnAssetsChanged() { PopulateToolbar(); }
	private void _OnUndoRedoHistoryChanged() { UpdateObjectPropertiesPanel(); RefreshLayerList(); }

	public void DeselectAllObjects(PlacedObject excludeFromDeselection = null)
	{
		List<PlacedObject> objectsToActuallyDeselect = new List<PlacedObject>(currentlySelectedObjects);
		bool selectionTrulyChanged = false;

		foreach (PlacedObject obj in objectsToActuallyDeselect)
		{
			if (obj == excludeFromDeselection) continue;
			if (GodotObject.IsInstanceValid(obj))
			{
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
		if (objectPropertiesPanel == null || assetNameLabelInProps == null || rotationSpinBoxProp == null ||
			scaleXSpinBoxProp == null || scaleYSpinBoxProp == null || deleteObjectButtonFromPanelProp == null) {
			if(objectPropertiesPanel != null) objectPropertiesPanel.Visible = false;
			if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance)) rotationGizmoInstance.Visible = false;
			return;
		}

		isUpdatingPanelFromSelection = true;
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]))
		{
			PlacedObject selected = currentlySelectedObjects[0];
			assetNameLabelInProps.Text = selected.AssetNameRef;
			rotationSpinBoxProp.Value = selected.CurrentRotationDegrees;
			scaleXSpinBoxProp.Value = selected.CurrentScale.X;
			scaleYSpinBoxProp.Value = selected.CurrentScale.Y;
			deleteObjectButtonFromPanelProp.Disabled = false;
			rotationSpinBoxProp.Editable = true;
			scaleXSpinBoxProp.Editable = true; scaleYSpinBoxProp.Editable = true;
			objectPropertiesPanel.Visible = true;

			if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance))
			{
				rotationGizmoInstance.UpdateGizmoVisuals(selected);
			}
		}
		else // Multiple or no objects selected
		{
			if (currentlySelectedObjects.Count > 1) {
				PlacedObject firstSelected = currentlySelectedObjects[0];
				assetNameLabelInProps.Text = $"[Multiple ({currentlySelectedObjects.Count}) Selected]";
				rotationSpinBoxProp.Value = firstSelected.CurrentRotationDegrees;
				scaleXSpinBoxProp.Value = firstSelected.CurrentScale.X;
				scaleYSpinBoxProp.Value = firstSelected.CurrentScale.Y;
				rotationSpinBoxProp.Editable = true;
				scaleXSpinBoxProp.Editable = true;
				scaleYSpinBoxProp.Editable = true;
				deleteObjectButtonFromPanelProp.Disabled = false;
				objectPropertiesPanel.Visible = true;
			} else { // No objects selected
				assetNameLabelInProps.Text = "[No Object Selected]";
				rotationSpinBoxProp.Value = 0; rotationSpinBoxProp.Editable = false;
				scaleXSpinBoxProp.Value = 1; scaleXSpinBoxProp.Editable = false;
				scaleYSpinBoxProp.Value = 1; scaleYSpinBoxProp.Editable = false;
				deleteObjectButtonFromPanelProp.Disabled = true;
				objectPropertiesPanel.Visible = false;
			}
			if (rotationGizmoInstance != null && GodotObject.IsInstanceValid(rotationGizmoInstance)) rotationGizmoInstance.Visible = false;
		}
		isUpdatingPanelFromSelection = false;
	}

	// --- Property Panel & Gizmo Signal Handlers ---
	private void _OnPropsRotationChanged(double newValue)
	{
		if (isUpdatingPanelFromSelection || undoRedoManagerInstance == null || tileDrawer == null || currentlySelectedObjects.Count == 0) return;
		float newRotationDeg = (float)newValue;
		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects) {
			if (GodotObject.IsInstanceValid(obj)) {
				float oldRotationDeg = obj.CurrentRotationDegrees;
				batchActions.Add(new RotateObjectAction(obj, oldRotationDeg, newRotationDeg));
			}
		}
		if (batchActions.Count > 0) {
			CompositeEditorAction ca = new CompositeEditorAction(batchActions, $"Batch Rotate {batchActions.Count} Objs");
			ca.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(ca);
		}
	}

	private void _OnPropsScaleXChanged(double newValue)
	{
		if (isUpdatingPanelFromSelection || undoRedoManagerInstance == null || tileDrawer == null || currentlySelectedObjects.Count == 0) return;
		float newScaleX = (float)newValue;
		if (newScaleX < 0.01f) newScaleX = 0.01f;
		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects) {
			if (GodotObject.IsInstanceValid(obj)) {
				Vector2 oldScale = obj.CurrentScale; Vector2 newScale = new Vector2(newScaleX, oldScale.Y);
				batchActions.Add(new ScaleObjectAction(obj, oldScale, newScale));
			}
		}
		if (batchActions.Count > 0) {
			CompositeEditorAction ca = new CompositeEditorAction(batchActions, $"Batch ScaleX {batchActions.Count} Objs");
			ca.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(ca);
		}
	}

	private void _OnPropsScaleYChanged(double newValue)
	{
		if (isUpdatingPanelFromSelection || undoRedoManagerInstance == null || tileDrawer == null || currentlySelectedObjects.Count == 0) return;
		float newScaleY = (float)newValue;
		if (newScaleY < 0.01f) newScaleY = 0.01f;
		List<EditorAction> batchActions = new List<EditorAction>();
		foreach (PlacedObject obj in currentlySelectedObjects) {
			if (GodotObject.IsInstanceValid(obj)) {
				Vector2 oldScale = obj.CurrentScale; Vector2 newScale = new Vector2(oldScale.X, newScaleY);
				batchActions.Add(new ScaleObjectAction(obj, oldScale, newScale));
			}
		}
		if (batchActions.Count > 0) {
			CompositeEditorAction ca = new CompositeEditorAction(batchActions, $"Batch ScaleY {batchActions.Count} Objs");
			ca.Execute(tileDrawer); undoRedoManagerInstance.RecordAction(ca);
		}
	}

	private void _OnPropsDeleteObjectPressed() { if (!isUpdatingPanelFromSelection) _HandleDeleteSelectedObjects(); }
	private void _HandleDeleteSelectedObjects()
	{
		if (currentlySelectedObjects.Count == 0) return;
		if (assetManager == null || undoRedoManagerInstance == null || tileDrawer == null) { GD.PrintErr("Delete deps null."); return; }
		Node placedObjectsRootNode = GetNode("PlacedObjectsRoot");
		if (!(placedObjectsRootNode is Node2D objectsRootParent)) { GD.PrintErr("PlacedObjectsRoot not found/Node2D."); return; }

		List<PlacedObject> toDeleteCopy = new List<PlacedObject>(currentlySelectedObjects);
		DeselectAllObjects();

		DeleteMultipleObjectsAction action = new DeleteMultipleObjectsAction(
			toDeleteCopy, objectsRootParent.GetPath(), assetManager, TileDrawer.PlacedObjectScenePath
		);
		action.Execute(tileDrawer);
		undoRedoManagerInstance.RecordAction(action);
	}

	private void _OnGizmoRotationStarted(float initialRotationDegrees) {
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			gizmoDragStartRotationForUndo = initialRotationDegrees; // Store the rotation when gizmo drag starts
		}
	}
	private void _OnGizmoRotationUpdated(float newRotationDegrees) {
		// Live update the spinbox in the properties panel if a single object is selected
		if (isUpdatingPanelFromSelection) return; // Avoid feedback if panel itself is source of change
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]) && rotationSpinBoxProp != null) {
			isUpdatingPanelFromSelection = true; // Prevent this change from re-triggering _OnPropsRotationChanged
			rotationSpinBoxProp.Value = newRotationDegrees;
			isUpdatingPanelFromSelection = false;
		}
	}
	private void _OnGizmoRotationFinished(float finalRotationDegrees, float originalRotationDegreesAtDragStart) {
		if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0])) {
			PlacedObject activeObject = currentlySelectedObjects[0];
			// Ensure the object's state matches the gizmo's final state for the action
			activeObject.CurrentRotationDegrees = finalRotationDegrees;

			if (!Mathf.IsEqualApprox(originalRotationDegreesAtDragStart, finalRotationDegrees)) {
				if (undoRedoManagerInstance != null && tileDrawer != null) {
					RotateObjectAction action = new RotateObjectAction(activeObject, originalRotationDegreesAtDragStart, finalRotationDegrees);
					// Execute is already visually done by the gizmo's _Process.
					// For action consistency, we could revert, then action.Execute().
					// Or, assume visual state is final, and action is for undo/redo state capture.
					// For now, let's assume the visual state is what we want to record.
					// The action's Execute will ensure this state on Redo.
					undoRedoManagerInstance.RecordAction(action);
					GD.Print($"Rotation via gizmo finished. Action recorded for {activeObject.AssetNameRef}.");
				}
			}
			UpdateObjectPropertiesPanel(); // Refresh panel to show final value and ensure consistency
		}
	}
	// --- End Gizmo Handlers ---

	public override void _UnhandledInput(InputEvent @event)
	{
		// Allow gizmo to process input first if it's active and dragging
        if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsHandlingInput()) { // IsHandlingInput() needs to be added to RotationGizmo
            // If gizmo handles it, it should call GetViewport().SetInputAsHandled().
            // For now, assume Gizmo's own _Input or _UnhandledInput will consume if it needs to.
            // If the gizmo itself uses _UnhandledInput, then this structure might need adjustment,
            // or the gizmo's _UnhandledInput should also check GetViewport().IsInputHandled().
        }

		if (GetViewport().GuiGetFocusOwner() != null && !(GetViewport().GuiGetFocusOwner() is Button))
		{
			return;
		}

		if (currentDrawingMode == DrawingMode.SelectObject)
		{
			// Defer to gizmo if it's visible and might be interacting
            // This check might be too broad; gizmo should specifically consume events it uses.
            // if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && @event is InputEventMouse) {
            //     // Let gizmo handle mouse events if it's visible, and it will SetInputAsHandled if it uses them.
            // } else
			if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
			{
				// If gizmo is visible and handles this click, let it.
                // This requires gizmo to SetInputAsHandled().
                // The current gizmo connects to Area2D.InputEvent which handles this.
				if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsMouseOverHandle()) { // IsMouseOverHandle() new helper
					// Let gizmo's Area2D handle it. If it does, it should consume.
					// If not consumed by gizmo Area2D, then proceed with object selection/deselection.
					// This is tricky. For now, assume Area2D consumes and MainScene won't re-process.
					// If click is NOT on gizmo handle:
				}

				if (mouseButtonEvent.Pressed)
				{
					if (rotationGizmoInstance != null && rotationGizmoInstance.Visible && rotationGizmoInstance.IsMouseOverHandle()) {
                        // Let Gizmo's _OnHandleAreaInputEvent handle this press.
                        // It will call SetInputAsHandled().
                        return;
                    }

					Vector2 mousePos = GetGlobalMousePosition();
					PhysicsDirectSpaceState2D spaceState = GetTree().Root.World2D.DirectSpaceState;
					var queryParams = new PhysicsPointQueryParameters2D
					{
						Position = mousePos, CollideWithAreas = true, CollideWithBodies = false, CollisionMask = 1u << 0 | 1u << 1 // Check object layer (0) AND gizmo layer (1)
                                                                                                                            // Or just object layer (1) if gizmo doesn't block selection underneath.
                                                                                                                            // Assuming objects are on layer 1.
					};
					queryParams.CollisionMask = 1; // Objects on physics layer 1

					var results = spaceState.IntersectPoint(queryParams, 1);

					PlacedObject newlyClickedObject = null;
					if (results.Count > 0) {
						if (results[0]["collider"].Obj is Area2D area && area.GetParent() is PlacedObject po)
							newlyClickedObject = po;
					}

					bool isShiftPressed = Input.IsKeyPressed(Key.Shift) || Input.IsKeyPressed(Key.CmdOrCtrl);

					if (newlyClickedObject != null) {
						if (isShiftPressed) {
							if (currentlySelectedObjects.Contains(newlyClickedObject)) {
								if (GodotObject.IsInstanceValid(newlyClickedObject)) {
									Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite");
									if (sprite != null && originalModulations.TryGetValue(newlyClickedObject, out Color originalColor)) {
										sprite.Modulate = originalColor;
									} else if (sprite != null) { sprite.Modulate = Colors.White; }
								}
								currentlySelectedObjects.Remove(newlyClickedObject);
								originalModulations.Remove(newlyClickedObject);
							} else {
								currentlySelectedObjects.Add(newlyClickedObject);
								Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite");
								if (sprite != null) {
									if (!originalModulations.ContainsKey(newlyClickedObject)) {
										originalModulations[newlyClickedObject] = sprite.Modulate;
									}
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
                                DeselectAllObjects();
                                currentlySelectedObjects.Add(newlyClickedObject);
                                Sprite2D sprite = newlyClickedObject.GetNode<Sprite2D>("ObjectSprite");
                                if (sprite != null) {
                                    originalModulations[newlyClickedObject] = sprite.Modulate;
                                    sprite.Modulate = new Color(0.7f, 0.7f, 1.0f, 0.8f);
                                }
                            }
						}

						if (currentlySelectedObjects.Count == 1) { // Only start drag if one object ends up selected
							isDraggingObject = true; // This drag is for the object itself, not the gizmo handle
							dragStartMousePosition = mousePos;
							dragStartObjectOriginalPosition = currentlySelectedObjects[0].GlobalPosition;
						} else {
							isDraggingObject = false;
						}
					} else { // Clicked empty space (and not on a gizmo handle)
						if (!isShiftPressed) DeselectAllObjects();
						isDraggingObject = false;
					}
					UpdateObjectPropertiesPanel();
					GetViewport().SetInputAsHandled();
				}
				else // MouseButton.Left Released
				{
					// Dragging object logic (not gizmo)
					if (isDraggingObject && currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]))
					{
						PlacedObject draggedObject = currentlySelectedObjects[0];
						Vector2 finalPos = draggedObject.GlobalPosition;
						if (!dragStartObjectOriginalPosition.IsEqualApprox(finalPos))
						{
							draggedObject.GlobalPosition = dragStartObjectOriginalPosition;
							MoveObjectAction action = new MoveObjectAction(
								draggedObject, dragStartObjectOriginalPosition, finalPos
							);
							action.Execute(tileDrawer);
							undoRedoManagerInstance.RecordAction(action);
							UpdateObjectPropertiesPanel();
						}
					}
					isDraggingObject = false;
				}
			}
			else if (@event is InputEventMouseMotion mouseMotionEvent && isDraggingObject) // Object dragging
			{
				if (currentlySelectedObjects.Count == 1 && GodotObject.IsInstanceValid(currentlySelectedObjects[0]))
				{
					PlacedObject draggedObject = currentlySelectedObjects[0];
					Vector2 mouseDelta = GetGlobalMousePosition() - dragStartMousePosition;
					draggedObject.GlobalPosition = dragStartObjectOriginalPosition + mouseDelta;
					UpdateObjectPropertiesPanel();
					GetViewport().SetInputAsHandled();
				}
			}
			else if ((@event.IsActionPressed("delete_object_action") ||
					 (@event is InputEventKey keyDel && keyDel.Keycode == Key.Delete && keyDel.Pressed && !keyDel.IsEcho()))
					 && currentlySelectedObjects.Count > 0
					)
			{
				if (GetViewport().GuiGetFocusOwner() != null && !(GetViewport().GuiGetFocusOwner() is Button)) return;
				_HandleDeleteSelectedObjects();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void SetupFileDialogs()
	{
		saveFileDialog = new FileDialog(); saveFileDialog.Title = "Save Map"; saveFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
		saveFileDialog.AddFilter("*.map ; Map Files"); saveFileDialog.FileSelected += OnSaveFileDialogFileSelected; AddChild(saveFileDialog);
		loadFileDialog = new FileDialog(); loadFileDialog.Title = "Load Map"; loadFileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
		loadFileDialog.AddFilter("*.map ; Map Files"); loadFileDialog.FileSelected += OnLoadFileDialogFileSelected; AddChild(loadFileDialog);
	}

	private void PopulateToolbar()
	{
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
		Button saveButton = new Button(); saveButton.Text = "Save Map"; saveButton.Pressed += OnSaveMapPressed; toolbarPanel.AddChild(saveButton);
		Button loadButton = new Button(); loadButton.Text = "Load Map"; loadButton.Pressed += OnLoadMapPressed; toolbarPanel.AddChild(loadButton);
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
	private void _OnUndoButtonPressed() { if (undoRedoManagerInstance != null) undoRedoManagerInstance.Undo(); }
	private void _OnRedoButtonPressed() { if (undoRedoManagerInstance != null) undoRedoManagerInstance.Redo(); }
	private void _OnUndoStackChanged(bool canUndo) { if (undoButton != null) undoButton.Disabled = !canUndo; }
	private void _OnRedoStackChanged(bool canRedo) { if (redoButton != null) redoButton.Disabled = !canRedo; }
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
	private void OnSaveMapPressed() { if (tileDrawer != null) saveFileDialog.PopupCentered(); else GD.PrintErr("TileDrawer null for Save."); }
	private void OnLoadMapPressed() { if (tileDrawer != null) loadFileDialog.PopupCentered(); else GD.PrintErr("TileDrawer null for Load."); }
	private void OnSaveFileDialogFileSelected(string filePath) { MapSaverLoader.SaveMap(tileDrawer, filePath); }
	private void OnLoadFileDialogFileSelected(string filePath) { MapSaverLoader.LoadMap(tileDrawer, filePath); RefreshLayerList(); UpdateObjectPropertiesPanel(); /*Refresh layers & potentially selected obj panel after load*/ }
	public DrawingMode GetCurrentDrawingMode() { return currentDrawingMode; }
	public void ChangeDrawingMode(DrawingMode newMode) {
		if (currentDrawingMode == newMode) return;
		string oldModeName = currentDrawingMode.ToString();
		if (currentDrawingMode == DrawingMode.SelectObject && newMode != DrawingMode.SelectObject) DeselectAllObjects();
		currentDrawingMode = newMode;
		GD.Print($"MainScene: DrawingMode changed from {oldModeName} to {newMode}");
		EmitSignal(SignalName.DrawingModeChanged, (long)currentDrawingMode);
	}
	private void RefreshLayerList() {
		var layersPanelCtrl = GetNode<LayersPanelController>("LayersPanel");
		layersPanelCtrl?.RefreshLayerList();
	}

}
