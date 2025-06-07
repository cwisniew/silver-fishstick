using Godot;
using System.Collections.Generic;
using System.Linq;

// Assuming EditorAction is in the same namespace or accessible via using directive.

public class CompositeEditorAction : EditorAction
{
	private List<EditorAction> actionsToPerform;
	private string actionDescription; // Renamed from 'description' to avoid conflict with Node.Description if ever used as such.

	public CompositeEditorAction(List<EditorAction> actions, string description = "Batch Operation")
	{
		// It's important to take a new list or copy to prevent external modification
		// of the list after the action is created.
		this.actionsToPerform = new List<EditorAction>(actions ?? new List<EditorAction>());
		this.actionDescription = description;
		// base.Description = description; // If EditorAction had a Description property to set
	}

	public override void Execute(TileMap tileMapContext)
	{
		// GD.Print($"CompositeAction Execute: {actionDescription} - {actionsToPerform.Count} sub-actions.");
		foreach (EditorAction action in actionsToPerform)
		{
			if (action != null) // Basic null check for safety
			{
				action.Execute(tileMapContext);
			}
		}
	}

	public override void Undo(TileMap tileMapContext)
	{
		// GD.Print($"CompositeAction Undo: {actionDescription} - {actionsToPerform.Count} sub-actions.");
		// Undo in reverse order of execution for correctness
		foreach (EditorAction action in ((IEnumerable<EditorAction>)actionsToPerform).Reverse())
		{
			if (action != null) // Basic null check
			{
				action.Undo(tileMapContext);
			}
		}
	}

	public bool IsEmpty()
	{
		return actionsToPerform == null || actionsToPerform.Count == 0;
	}

	// Optional: Method to add actions if building incrementally, though constructor is typical
	public void AddAction(EditorAction action)
	{
		if (action != null)
		{
			actionsToPerform.Add(action);
		}
	}

	public int ActionCount => actionsToPerform.Count;
}
