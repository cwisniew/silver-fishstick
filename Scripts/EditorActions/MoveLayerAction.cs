using Godot;

// Assuming TileDrawer is accessible (e.g. same namespace or via using directive)
// using YourProject.Scripts.Tools;

public partial class MoveLayerAction : EditorAction
{
	private int layerIndexAtStart;
	private int newVisualIndex;

	private TileDrawer tileDrawerInstance;
	private string activeLayerNameBeforeOperation;
	private int originalCurrentDrawingLayerIndex;

	public MoveLayerAction(int originalIndexOfMovedLayer, int targetVisualIndex, TileDrawer drawer)
	{
		this.layerIndexAtStart = originalIndexOfMovedLayer;
		this.newVisualIndex = targetVisualIndex;
		this.tileDrawerInstance = drawer;

		if (this.tileDrawerInstance != null &&
			this.tileDrawerInstance.CurrentDrawingLayer >= 0 &&
			this.tileDrawerInstance.CurrentDrawingLayer < this.tileDrawerInstance.GetLayersCount())
		{
			this.originalCurrentDrawingLayerIndex = this.tileDrawerInstance.CurrentDrawingLayer;
			this.activeLayerNameBeforeOperation = this.tileDrawerInstance.GetLayerName(this.originalCurrentDrawingLayerIndex);
		}
		else
		{
			this.originalCurrentDrawingLayerIndex = -1;
			this.activeLayerNameBeforeOperation = null;
			if(this.tileDrawerInstance == null) GD.PrintErr("MoveLayerAction Constructor: TileDrawer instance is null.");
		}
	}

	public override void Execute(TileMap tileMapContext) // tileMapContext is expected to be the TileDrawer
	{
		if (tileDrawerInstance == null) {
			// Try to cast if not set by constructor, though constructor should be primary way
			tileDrawerInstance = tileMapContext as TileDrawer;
			if (tileDrawerInstance == null) {
				GD.PrintErr("MoveLayerAction Execute: TileDrawer instance is null or context is not TileDrawer.");
				return;
			}
		}

		if (IsValidIndex(tileDrawerInstance, layerIndexAtStart) && IsValidIndex(tileDrawerInstance, newVisualIndex))
		{
			tileDrawerInstance.MoveLayer(layerIndexAtStart, newVisualIndex);
			// GD.Print($"MoveLayerAction Execute: Layer moved from {layerIndexAtStart} to {newVisualIndex}.");

			RestoreActiveLayerSelection();
		}
		else
		{
			GD.PrintErr($"MoveLayerAction Execute: Invalid indices. From: {layerIndexAtStart}, To: {newVisualIndex}, Layers: {tileDrawerInstance.GetLayersCount()}");
		}
	}

	public override void Undo(TileMap tileMapContext) // tileMapContext is expected to be the TileDrawer
	{
		if (tileDrawerInstance == null) {
			tileDrawerInstance = tileMapContext as TileDrawer;
			if (tileDrawerInstance == null) {
				GD.PrintErr("MoveLayerAction Undo: TileDrawer instance is null or context is not TileDrawer.");
				return;
			}
		}

		// The layer we moved is NOW at 'newVisualIndex'. We want to move it back to 'layerIndexAtStart'.
		if (IsValidIndex(tileDrawerInstance, newVisualIndex) && IsValidIndex(tileDrawerInstance, layerIndexAtStart))
		{
			tileDrawerInstance.MoveLayer(newVisualIndex, layerIndexAtStart);
			// GD.Print($"MoveLayerAction Undo: Layer moved from {newVisualIndex} back to {layerIndexAtStart}.");

			RestoreActiveLayerSelection();
		}
		else
		{
			GD.PrintErr($"MoveLayerAction Undo: Invalid indices. From: {newVisualIndex}, To: {layerIndexAtStart}, Layers: {tileDrawerInstance.GetLayersCount()}");
		}
	}

	private void RestoreActiveLayerSelection()
	{
		if (tileDrawerInstance == null || tileDrawerInstance.GetLayersCount() == 0) return;

		if (!string.IsNullOrEmpty(activeLayerNameBeforeOperation))
		{
			bool found = false;
			for (int i = 0; i < tileDrawerInstance.GetLayersCount(); i++)
			{
				if (tileDrawerInstance.GetLayerName(i) == activeLayerNameBeforeOperation)
				{
					tileDrawerInstance.SetCurrentDrawingLayer(i);
					found = true;
					// GD.Print($"MoveLayerAction: Restored active layer to '{activeLayerNameBeforeOperation}' at new index {i}.");
					break;
				}
			}
			if (!found) {
				// GD.Print($"MoveLayerAction: Active layer '{activeLayerNameBeforeOperation}' not found after move. Defaulting selection.");
				tileDrawerInstance.SetCurrentDrawingLayer(0); // Fallback to first layer
			}
		} else if (originalCurrentDrawingLayerIndex != -1) { // If no name, try to restore by original index if still valid
            int targetIndex = Mathf.Clamp(originalCurrentDrawingLayerIndex, 0, tileDrawerInstance.GetLayersCount() - 1);
            tileDrawerInstance.SetCurrentDrawingLayer(targetIndex);
            // GD.Print($"MoveLayerAction: Restored active layer by original index (or clamped) to {targetIndex}.");
        }
		else if (tileDrawerInstance.GetLayersCount() > 0) // Absolute fallback
		{
			 tileDrawerInstance.SetCurrentDrawingLayer(0);
			 // GD.Print("MoveLayerAction: No active layer name/index, defaulting to layer 0.");
		}
	}

	// Renamed tileMap parameter to td for clarity within this method
	private bool IsValidIndex(TileMap td, int index)
	{
		if (td == null) return false;
		return index >= 0 && index < td.GetLayersCount();
	}
}
