using Godot;

public class MoveLayerAction : EditorAction
{
	private int layerIndexAtStart; // The original index of the layer that was moved
	private int newVisualIndex;    // The index it was moved TO

	public MoveLayerAction(int originalIndexOfMovedLayer, int targetVisualIndex)
	{
		this.layerIndexAtStart = originalIndexOfMovedLayer;
		this.newVisualIndex = targetVisualIndex;
		// Description = $"Move Layer from {originalIndexOfMovedLayer} to {targetVisualIndex}";
	}

	public override void Execute(TileMap tileMap)
	{
		// When executing (or redoing), the layer we intend to move is assumed to be
		// currently at 'layerIndexAtStart'. This is true for the first execution.
		// For redo, Undo() would have moved it from 'newVisualIndex' back to 'layerIndexAtStart'.
		if (IsValidIndex(tileMap, layerIndexAtStart) && IsValidTargetIndex(tileMap, newVisualIndex, layerIndexAtStart > newVisualIndex))
		{
			tileMap.MoveLayer(layerIndexAtStart, newVisualIndex);
		}
		else
		{
			GD.PrintErr($"MoveLayerAction Execute: Invalid indices. From: {layerIndexAtStart}, To: {newVisualIndex}, Layers: {tileMap.GetLayersCount()}");
		}
	}

	public override void Undo(TileMap tileMap)
	{
		// The layer we moved is NOW at 'newVisualIndex'. We want to move it back to 'layerIndexAtStart'.
		// So, the layer currently at 'newVisualIndex' is the one we are targeting.
		// The position it should return to is 'layerIndexAtStart'.
		if (IsValidIndex(tileMap, newVisualIndex) && IsValidTargetIndex(tileMap, layerIndexAtStart, newVisualIndex > layerIndexAtStart))
		{
			tileMap.MoveLayer(newVisualIndex, layerIndexAtStart);
		}
		else
		{
			GD.PrintErr($"MoveLayerAction Undo: Invalid indices. From: {newVisualIndex}, To: {layerIndexAtStart}, Layers: {tileMap.GetLayersCount()}");
		}
	}

	private bool IsValidIndex(TileMap tileMap, int index)
	{
		return index >= 0 && index < tileMap.GetLayersCount();
	}

	// MoveLayer's to_position can be tricky. It's where it slots in.
	// If moving layer 2 to 0 (up): MoveLayer(2,0). Layer 0->1, 1->2. Original 2 is now at 0.
	// If moving layer 0 to 2 (down): MoveLayer(0,2). Layer 1->0, 2->1. Original 0 is now at 2.
	// The to_position is the final index.
	private bool IsValidTargetIndex(TileMap tileMap, int targetIndex, bool movingUp)
	{
		// When moving a layer, to_position is the new index for the layer.
		// If moving layer from index `src` to `dst`:
		// If `src < dst` (moving down), layers between `src+1` and `dst` shift up by 1. The layer lands at `dst`.
		// If `src > dst` (moving up), layers between `dst` and `src-1` shift down by 1. The layer lands at `dst`.
		// So, targetIndex must be within [0, GetLayersCount() - 1].
		return targetIndex >= 0 && targetIndex < tileMap.GetLayersCount();
	}
}
