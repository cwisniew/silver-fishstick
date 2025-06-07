using Godot;
using System.Collections.Generic; // For List

public partial class LayersPanelController : PanelContainer
{
	[Export] private PackedScene layerEntryScene;

	private VBoxContainer layerListContainer;
	private Button addLayerButton;
	private Button removeLayerButton;

	private TileDrawer tileDrawer;
	private int currentlySelectedLayerIndexInUI = -1;
	private List<Button> selectLayerButtons = new List<Button>();
	private UndoRedoManager undoRedoManager; // Added

	public override void _Ready()
	{
		layerListContainer = GetNode<VBoxContainer>("VBoxContainer/ScrollContainer/LayerListContainer");
		addLayerButton = GetNode<Button>("VBoxContainer/HBoxButtonControls/AddLayerButton");
		removeLayerButton = GetNode<Button>("VBoxContainer/HBoxButtonControls/RemoveLayerButton");

		// Get UndoRedoManager
		undoRedoManager = GetNode<UndoRedoManager>("/root/UndoRedoManager");
		if (undoRedoManager == null)
		{
			GD.PrintErr("LayersPanelController: UndoRedoManager not found! Undo/Redo for layer properties will not work or trigger UI refresh.");
		}
		else
		{
			// Connect to HistoryChanged signal for full UI refresh after Undo/Redo
			// Assuming signal name is "HistoryChanged" as emitted by UndoRedoManager
			if (!undoRedoManager.IsConnected("HistoryChanged", Callable.From(_OnHistoryChanged)))
			{
				undoRedoManager.Connect("HistoryChanged", Callable.From(_OnHistoryChanged));
				GD.Print("LayersPanelController: Connected to UndoRedoManager's HistoryChanged signal.");
			}
		}

		// Attempt to get TileDrawer
		tileDrawer = GetNode<TileDrawer>("/root/MainScene/TileMap");
		if (tileDrawer == null) {
			GD.PrintErr("LayersPanelController: TileDrawer node (expected at /root/MainScene/TileMap) not found! Panel will be disabled.");
			addLayerButton.Disabled = true;
			removeLayerButton.Disabled = true;
			return;
		}

		if (layerEntryScene == null) {
			GD.PrintErr("LayersPanelController: LayerEntryScene not packed/assigned in Inspector! UI will be minimal.");
		}

		addLayerButton.Pressed += _OnAddLayerButtonPressed;
		removeLayerButton.Pressed += _OnRemoveLayerButtonPressed;

		RefreshLayerList();

		// Ensure CurrentDrawingLayer (from TileDrawer, default 0) is selected in UI by default
		if (tileDrawer.GetLayersCount() > 0) {
			SelectLayerInUI(tileDrawer.CurrentDrawingLayer, false); // false: don't re-update TileDrawer, just sync UI
		}
	}

	private void ClearLayerListUI()
	{
		foreach (Node child in layerListContainer.GetChildren())
		{
			child.QueueFree();
		}
		selectLayerButtons.Clear();
		currentlySelectedLayerIndexInUI = -1;
	}

	public void RefreshLayerList()
	{
		if (tileDrawer == null || layerListContainer == null) return;

		ClearLayerListUI();

		int layersCount = tileDrawer.GetLayersCount();
		if (layersCount == 0)
		{
			tileDrawer.AddLayer(-1);
			tileDrawer.SetLayerName(0, "Default Layer");
			// tileDrawer.SetLayerZIndex(0,0); // Default Z-index is fine
			layersCount = 1;
			GD.Print("LayersPanelController: Created initial default layer in TileDrawer.");
		}

		for (int i = 0; i < layersCount; i++)
		{
			Node layerEntryInstance;
			CheckBox visibleCheck = null;
			LineEdit nameEdit = null;
			Button selectButton = null;
				SpinBox zIndexSpinBox = null;
				Button moveUpButton = null; // Added
				Button moveDownButton = null; // Added

			if (layerEntryScene != null) {
				layerEntryInstance = layerEntryScene.Instantiate();
				nameEdit = layerEntryInstance.GetNode<LineEdit>("HBoxContainer/LayerNameLineEdit");
				visibleCheck = layerEntryInstance.GetNode<CheckBox>("HBoxContainer/VisibleCheckBox");
				selectButton = layerEntryInstance.GetNode<Button>("HBoxContainer/SelectLayerButton");
					zIndexSpinBox = layerEntryInstance.GetNode<SpinBox>("HBoxContainer/ZIndexSpinBox");
					moveUpButton = layerEntryInstance.GetNode<Button>("HBoxContainer/MoveUpButton"); // Get MoveUpButton
					moveDownButton = layerEntryInstance.GetNode<Button>("HBoxContainer/MoveDownButton"); // Get MoveDownButton
			} else {
					layerEntryInstance = new HBoxContainer();
				visibleCheck = new CheckBox();
				nameEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				selectButton = new Button { Text = "Select", ToggleMode = true };
					// Note: Programmatic fallback will lack ZIndexSpinBox unless added here too.
					// For this task, assume layerEntryScene is packed and paths are correct.
				layerEntryInstance.AddChild(visibleCheck);
					// No ZIndexSpinBox in fallback for brevity, focus on layerEntryScene path
				layerEntryInstance.AddChild(nameEdit);
				layerEntryInstance.AddChild(selectButton);
			}

				// Critical check for all required components from LayerEntry.tscn
				// If using layerEntryScene, all buttons must be found.
				bool mainControlsMissing = nameEdit == null || visibleCheck == null || selectButton == null || (layerEntryScene != null && zIndexSpinBox == null);
				bool moveButtonsMissing = layerEntryScene != null && (moveUpButton == null || moveDownButton == null);

				if (mainControlsMissing || moveButtonsMissing) {
					GD.PrintErr($"LayersPanelController: Failed to get all required nodes from LayerEntryInstance for layer {i}. MainControlsMissing: {mainControlsMissing}, MoveButtonsMissing: {moveButtonsMissing}. Scene packed incorrectly or paths changed?");
					if(layerEntryInstance != null) layerEntryInstance.QueueFree();
				continue;
			}

			nameEdit.Text = tileDrawer.GetLayerName(i) ?? $"Layer {i}";
				nameEdit.Editable = true;

			visibleCheck.SetMeta("layer_index", Variant.From(i));
			visibleCheck.ButtonPressed = tileDrawer.IsLayerEnabled(i);
			// Disconnect first to prevent multiple connections on refresh if not freeing properly
			// Disconnect previous TextSubmitted signal before connecting new one to prevent multiple calls if entries are reused (though we QueueFree them)
			// This is more of a defensive measure. The `ClearLayerListUI` should handle freeing.
			// nameEdit.TextSubmitted -= (newName) => _OnLayerNameSubmitted(newName, (int)nameEdit.GetMeta("layer_index_for_submit")); // Can't easily remove specific lambda
			// It's generally safer to ensure old nodes are freed, or connect signals once if nodes are reused.
			// Since we QueueFree in ClearLayerListUI, new instances should not have old connections.

			nameEdit.SetMeta("layer_index_for_submit", Variant.From(i));
			nameEdit.TextSubmitted += (newName) => _OnLayerNameSubmitted(newName, (int)nameEdit.GetMeta("layer_index_for_submit"));

			visibleCheck.Toggled += (toggled) => _OnLayerVisibilityToggled((int)visibleCheck.GetMeta("layer_index"), toggled);

			selectButton.Pressed += () => _OnSelectLayerButtonPressedInUI((int)selectButton.GetMeta("layer_index"));
			selectLayerButtons.Add(selectButton);

			// Setup ZIndexSpinBox if it exists (i.e., if layerEntryScene was used)
			if (zIndexSpinBox != null)
			{
				zIndexSpinBox.Value = tileDrawer.GetLayerZIndex(i);
				zIndexSpinBox.SetMeta("layer_index", Variant.From(i));
				// ValueChanged signal for SpinBox emits a double
				zIndexSpinBox.ValueChanged += (newValue) => _OnLayerZIndexChanged(newValue, (int)zIndexSpinBox.GetMeta("layer_index"));
			}

			// Setup Move Up/Down Buttons if they exist (i.e., if layerEntryScene was used)
			if (moveUpButton != null && moveDownButton != null)
			{
				moveUpButton.SetMeta("layer_index", Variant.From(i));
				moveDownButton.SetMeta("layer_index", Variant.From(i));

				moveUpButton.Pressed += () => _OnMoveLayerUpPressed((int)moveUpButton.GetMeta("layer_index"));
				moveDownButton.Pressed += () => _OnMoveLayerDownPressed((int)moveDownButton.GetMeta("layer_index"));

				moveUpButton.Disabled = (i == 0); // Disable "Up" for the first layer (topmost in UI)
				moveDownButton.Disabled = (i == layersCount - 1); // Disable "Down" for the last layer (bottommost in UI)
			}

			layerListContainer.AddChild(layerEntryInstance);
		}

		removeLayerButton.Disabled = (layersCount <= 1);

		int targetLayerToSelect = tileDrawer.CurrentDrawingLayer;
		if (targetLayerToSelect >= 0 && targetLayerToSelect < layersCount) {
				SelectLayerInUI(targetLayerToSelect, false);
		} else if (layersCount > 0) {
				SelectLayerInUI(0, true); // Default to layer 0 and ensure TileDrawer is synced
		}
	}

	// Dummy methods for checking signal connections if needed, not used in final logic
	private void _OnLayerVisibilityToggledDummy(bool toggled) {}
	private void _OnSelectLayerButtonPressedInUIDummy() {}


	private void SelectLayerInUI(int layerIndex, bool updateTileDrawerTarget) {
		if (layerIndex < 0 || layerIndex >= selectLayerButtons.Count) {
			// GD.Print($"SelectLayerInUI: Invalid layerIndex {layerIndex} for {selectLayerButtons.Count} buttons.");
			if (selectLayerButtons.Count > 0) { // Default to 0 if current selection becomes invalid
				currentlySelectedLayerIndexInUI = 0;
				if (updateTileDrawerTarget && tileDrawer != null) tileDrawer.SetCurrentDrawingLayer(0);
			} else {
				currentlySelectedLayerIndexInUI = -1;
				// No layer to select in TileDrawer
			}
		} else {
			currentlySelectedLayerIndexInUI = layerIndex;
			if (updateTileDrawerTarget && tileDrawer != null) {
				tileDrawer.SetCurrentDrawingLayer(layerIndex);
			}
		}

		for(int i = 0; i < selectLayerButtons.Count; i++) {
			selectLayerButtons[i].ButtonPressed = (i == currentlySelectedLayerIndexInUI);
		}
		// GD.Print($"UI: Layer {currentlySelectedLayerIndexInUI} selected. Update TileDrawer: {updateTileDrawerTarget}");
	}

	private void _OnSelectLayerButtonPressedInUI(int layerIndex)
	{
		SelectLayerInUI(layerIndex, true);
	}

	private void _OnLayerVisibilityToggled(int layerIndex, bool toggled)
	{
		if (tileDrawer != null)
		{
			tileDrawer.SetLayerEnabled(layerIndex, toggled);
			// GD.Print($"Layer {layerIndex} visibility set to {toggled}");
		}
	}

	private void _OnAddLayerButtonPressed()
	{
		if (tileDrawer != null)
		{
			tileDrawer.AddLayer(-1);
			int newLayerIndex = tileDrawer.GetLayersCount() - 1;
			tileDrawer.SetLayerName(newLayerIndex, $"Layer {newLayerIndex}");
			RefreshLayerList();
			SelectLayerInUI(newLayerIndex, true);
			// GD.Print($"Added new layer: {newLayerIndex}");
		}
	}

	private void _OnRemoveLayerButtonPressed()
	{
		if (tileDrawer != null && tileDrawer.GetLayersCount() > 1 &&
			currentlySelectedLayerIndexInUI >= 0 && currentlySelectedLayerIndexInUI < tileDrawer.GetLayersCount())
		{
			int layerToRemove = currentlySelectedLayerIndexInUI;
			// GD.Print($"Removing layer: {layerToRemove}");
			tileDrawer.RemoveLayer(layerToRemove);

			int newLayerToSelect = Mathf.Max(0, layerToRemove - 1);
			if (tileDrawer.GetLayersCount() == 0) { // Should be caught by > 1 check but defensive
				// This state should not be reached if removeLayerButton.Disabled correctly managed
			} else if (newLayerToSelect >= tileDrawer.GetLayersCount()) { // If last item was removed
				newLayerToSelect = tileDrawer.GetLayersCount() - 1;
			}

			RefreshLayerList();
			if (tileDrawer.GetLayersCount() > 0) {
				SelectLayerInUI(newLayerToSelect, true);
			}
		}
		else
		{
			GD.Print("Cannot remove layer. Select a layer to remove, or it's the last layer.");
		}
	}

	private void _OnLayerNameSubmitted(string newName, int layerIndex)
	{
		if (tileDrawer == null)
		{
			GD.PrintErr("LayersPanelController: TileDrawer is null, cannot rename layer.");
			return;
		}
		if (layerIndex < 0 || layerIndex >= tileDrawer.GetLayersCount())
		{
			GD.PrintErr($"LayersPanelController: Invalid layer index {layerIndex} for renaming.");
			return;
		}

		string trimmedNewName = newName.Trim();
		string oldName = tileDrawer.GetLayerName(layerIndex); // Get old name before any changes

		if (string.IsNullOrEmpty(trimmedNewName))
		{
			GD.Print("Layer name cannot be empty. Reverting.");
			if (layerIndex < layerListContainer.GetChildCount())
			{
				var layerEntryNode = layerListContainer.GetChild(layerIndex);
				var nameEdit = layerEntryNode?.GetNode<LineEdit>("HBoxContainer/LayerNameLineEdit");
				if (nameEdit != null) nameEdit.Text = oldName ?? $"Layer {layerIndex}";
			}
			return;
		}

		if (oldName == trimmedNewName) return;

		if (undoRedoManager != null)
		{
			SetLayerNameAction action = new SetLayerNameAction(layerIndex, oldName, trimmedNewName);
			action.Execute(tileDrawer); // Execute action first
			undoRedoManager.RecordAction(action);
			GD.Print($"Layer {layerIndex} renamed from '{oldName}' to '{trimmedNewName}' (Undoable).");
		}
		else
		{
			tileDrawer.SetLayerName(layerIndex, trimmedNewName); // Fallback
			GD.PrintErr("UndoRedoManager not found. Layer rename not undoable.");
		}
		// LineEdit already shows newName due to user input.
	}

	private void _OnLayerZIndexChanged(double newValue, int layerIndex)
	{
		if (tileDrawer == null)
		{
			GD.PrintErr("LayersPanelController: TileDrawer is null, cannot change Z-index.");
			return;
		}
		if (layerIndex < 0 || layerIndex >= tileDrawer.GetLayersCount())
		{
			GD.PrintErr($"LayersPanelController: Invalid layer index {layerIndex} for Z-index change.");
			return;
		}

		int newZIndex = (int)newValue; // Cast to int for TileMap Z-index
		int oldZIndex = tileDrawer.GetLayerZIndex(layerIndex);

		if (oldZIndex == newZIndex) return; // No actual change

		// TODO: Implement Undo/Redo for Z-index change in a later step
		tileDrawer.SetLayerZIndex(layerIndex, newZIndex);
		GD.Print($"Layer {layerIndex} Z-index changed from {oldZIndex} to {newZIndex}.");

		// Note: Changing Z-index might visually reorder things on the TileMap,
		// but the list order in LayersPanel UI remains unchanged by this action.
		// Reordering the UI list itself is a separate feature.
	}

	private bool IsValidLayerIndex(int index)
	{
		if (tileDrawer == null) return false;
		return index >= 0 && index < tileDrawer.GetLayersCount();
	}

	private void _OnMoveLayerUpPressed(int layerIndex)
	{
		if (tileDrawer == null || layerIndex <= 0 || !IsValidLayerIndex(layerIndex))
		{
			return;
		}

		int targetVisualIndex = layerIndex - 1;
		// string originalLayerName = tileDrawer.GetLayerName(layerIndex); // For logging

		string activeLayerNameBeforeMove = null;
		if (IsValidLayerIndex(tileDrawer.CurrentDrawingLayer))
		{
			activeLayerNameBeforeMove = tileDrawer.GetLayerName(tileDrawer.CurrentDrawingLayer);
		}

		if (undoRedoManager != null)
		{
			MoveLayerAction action = new MoveLayerAction(layerIndex, targetVisualIndex);
			action.Execute(tileDrawer); // This changes the TileMap state
			undoRedoManager.RecordAction(action);
			// GD.Print($"MoveLayerAction recorded: Layer {layerIndex} ('{originalLayerName}') moved to {targetVisualIndex}.");
		}
		else
		{
			GD.PrintErr("UndoRedoManager not found. Layer move not undoable.");
			tileDrawer.MoveLayer(layerIndex, targetVisualIndex); // Fallback direct call
		}

		// After TileMap state is changed, find the original active layer by name to restore selection
		if (activeLayerNameBeforeMove != null)
		{
			int newIndexOfOriginalActiveLayer = -1;
			for(int i=0; i < tileDrawer.GetLayersCount(); ++i) {
				if (tileDrawer.GetLayerName(i) == activeLayerNameBeforeMove) {
					newIndexOfOriginalActiveLayer = i;
					break;
				}
			}
			if (newIndexOfOriginalActiveLayer != -1 && IsValidLayerIndex(newIndexOfOriginalActiveLayer)) {
				 tileDrawer.SetCurrentDrawingLayer(newIndexOfOriginalActiveLayer);
			} else if (tileDrawer.GetLayersCount() > 0) { // Fallback if name not found or invalid
				tileDrawer.SetCurrentDrawingLayer(0);
			}
		} else if (tileDrawer.GetLayersCount() > 0) { // If no prior active layer or name was null
			tileDrawer.SetCurrentDrawingLayer(0);
		}


		RefreshLayerList(); // Refresh UI. It uses tileDrawer.CurrentDrawingLayer to highlight.
	}

	private void _OnMoveLayerDownPressed(int layerIndex)
	{
		if (tileDrawer == null || layerIndex < 0 || layerIndex >= tileDrawer.GetLayersCount() - 1 || !IsValidLayerIndex(layerIndex))
		{
			return;
		}

		int targetVisualIndex = layerIndex + 1;
		// string originalLayerName = tileDrawer.GetLayerName(layerIndex); // For logging

		string activeLayerNameBeforeMove = null;
		if (IsValidLayerIndex(tileDrawer.CurrentDrawingLayer))
		{
			activeLayerNameBeforeMove = tileDrawer.GetLayerName(tileDrawer.CurrentDrawingLayer);
		}

		if (undoRedoManager != null)
		{
			MoveLayerAction action = new MoveLayerAction(layerIndex, targetVisualIndex);
			action.Execute(tileDrawer);
			undoRedoManager.RecordAction(action);
			// GD.Print($"MoveLayerAction recorded: Layer {layerIndex} ('{originalLayerName}') moved to {targetVisualIndex}.");
		}
		else
		{
			GD.PrintErr("UndoRedoManager not found. Layer move not undoable.");
			tileDrawer.MoveLayer(layerIndex, targetVisualIndex); // Fallback
		}

		if (activeLayerNameBeforeMove != null)
		{
			int newIndexOfOriginalActiveLayer = -1;
			for(int i=0; i < tileDrawer.GetLayersCount(); ++i) {
				if (tileDrawer.GetLayerName(i) == activeLayerNameBeforeMove) {
					newIndexOfOriginalActiveLayer = i;
					break;
				}
			}
			if (newIndexOfOriginalActiveLayer != -1 && IsValidLayerIndex(newIndexOfOriginalActiveLayer)) {
				 tileDrawer.SetCurrentDrawingLayer(newIndexOfOriginalActiveLayer);
			} else if (tileDrawer.GetLayersCount() > 0) {
				tileDrawer.SetCurrentDrawingLayer(0);
			}
		} else if (tileDrawer.GetLayersCount() > 0) {
			tileDrawer.SetCurrentDrawingLayer(0);
		}

		RefreshLayerList();
	}

	private void _OnHistoryChanged()
	{
		GD.Print("LayersPanelController: Detected HistoryChanged signal from UndoRedoManager. Refreshing layer list UI.");
		if (tileDrawer == null)
		{
			GD.PrintErr("LayersPanelController: TileDrawer is null in _OnHistoryChanged. Cannot refresh.");
			return;
		}

		// RefreshLayerList rebuilds the UI from the TileMap's current state
		// and re-applies selection based on tileDrawer.CurrentDrawingLayer.
		// This assumes that any EditorAction's Execute/Undo methods that could affect
		// the validity or index of CurrentDrawingLayer (e.g., MoveLayerAction, or a future RemoveLayerAction)
		// are responsible for updating tileDrawer.CurrentDrawingLayer appropriately.
		RefreshLayerList();
	}
}
