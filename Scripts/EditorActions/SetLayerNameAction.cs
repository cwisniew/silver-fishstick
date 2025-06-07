using Godot;

public class SetLayerNameAction : EditorAction
{
	private int layerIndex;
	private string oldName;
	private string newName;

	public SetLayerNameAction(int layerIndex, string oldName, string newName)
	{
		this.layerIndex = layerIndex;
		this.oldName = oldName;
		this.newName = newName;
		// Description = $"Rename Layer {layerIndex} from '{oldName}' to '{newName}'";
	}

	public override void Execute(TileMap tileMap)
	{
		if (layerIndex >= 0 && layerIndex < tileMap.GetLayersCount())
		{
			tileMap.SetLayerName(layerIndex, newName);
		}
		else
		{
			GD.PrintErr($"SetLayerNameAction Execute: Invalid layer index {layerIndex}. Max layers: {tileMap.GetLayersCount()}");
		}
	}

	public override void Undo(TileMap tileMap)
	{
		if (layerIndex >= 0 && layerIndex < tileMap.GetLayersCount())
		{
			tileMap.SetLayerName(layerIndex, oldName);
		}
		else
		{
			GD.PrintErr($"SetLayerNameAction Undo: Invalid layer index {layerIndex}. Max layers: {tileMap.GetLayersCount()}");
		}
	}
}
