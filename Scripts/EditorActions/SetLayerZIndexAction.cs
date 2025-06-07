using Godot;

public class SetLayerZIndexAction : EditorAction
{
	private int layerIndex;
	private int oldZIndex;
	private int newZIndex;

	public SetLayerZIndexAction(int layerIndex, int oldZIndex, int newZIndex)
	{
		this.layerIndex = layerIndex;
		this.oldZIndex = oldZIndex;
		this.newZIndex = newZIndex;
		// Description = $"Set Layer {layerIndex} Z-Index from {oldZIndex} to {newZIndex}";
	}

	public override void Execute(TileMap tileMap)
	{
		if (layerIndex >= 0 && layerIndex < tileMap.GetLayersCount())
		{
			tileMap.SetLayerZIndex(layerIndex, newZIndex);
		}
		else
		{
			GD.PrintErr($"SetLayerZIndexAction Execute: Invalid layer index {layerIndex}. Max layers: {tileMap.GetLayersCount()}");
		}
	}

	public override void Undo(TileMap tileMap)
	{
		if (layerIndex >= 0 && layerIndex < tileMap.GetLayersCount())
		{
			tileMap.SetLayerZIndex(layerIndex, oldZIndex);
		}
		else
		{
			GD.PrintErr($"SetLayerZIndexAction Undo: Invalid layer index {layerIndex}. Max layers: {tileMap.GetLayersCount()}");
		}
	}
}
