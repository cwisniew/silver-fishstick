using Godot;

public abstract class EditorAction
{
	public abstract void Execute(TileMap tileMap);
	public abstract void Undo(TileMap tileMap);
	// public string Description { get; protected set; } // Optional for UI history
}
