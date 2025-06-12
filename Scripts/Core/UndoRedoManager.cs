using Godot;
using System.Collections.Generic;

// Assuming EditorAction.cs and TileData.cs are in a namespace like YourProject.Scripts.EditorActions
// If not, ensure they are accessible, or adjust using directives.
// For this example, assuming they are directly accessible (e.g. no specific namespace for them).

public partial class UndoRedoManager : Node
{
	private List<EditorAction> undoStack = new List<EditorAction>();
	private List<EditorAction> redoStack = new List<EditorAction>();
	private const int MaxHistory = 100;

	private TileMap tileMap;

	// Signals for UI updates
	[Signal] public delegate void UndoStackChangedEventHandler(bool canUndo);
	[Signal] public delegate void RedoStackChangedEventHandler(bool canRedo);
	[Signal] public delegate void HistoryChangedEventHandler(); // Added new signal


	public void Initialize(TileMap targetTileMap)
	{
		if (targetTileMap == null)
		{
			GD.PrintErr("UndoRedoManager: Initialization with a null TileMap is not allowed.");
			return;
		}
		this.tileMap = targetTileMap;
		GD.Print("UndoRedoManager initialized with TileMap: " + targetTileMap.Name);
	}

	public void RecordAction(EditorAction action)
	{
		if (tileMap == null)
		{
			GD.PrintErr("UndoRedoManager: TileMap not initialized! Cannot record action.");
			return;
		}
		if (action == null)
		{
			GD.PrintErr("UndoRedoManager: Cannot record a null action.");
			return;
		}

		undoStack.Add(action);
		if (MaxHistory > 0 && undoStack.Count > MaxHistory)
		{
			undoStack.RemoveAt(0); // Remove oldest action
		}

		bool redoWasPossible = redoStack.Count > 0;
		redoStack.Clear();

		EmitSignal(nameof(UndoStackChanged), true);
		if (redoWasPossible)
		{
			EmitSignal(nameof(RedoStackChanged), false);
		}
		EmitSignal(nameof(HistoryChanged)); // Emit after new action is recorded
		GD.Print("UndoRedoManager: HistoryChanged signal emitted after RecordAction.");
		// GD.Print("Action recorded. Undo stack: " + undoStack.Count + ", Redo stack: " + redoStack.Count);
	}

	public void Undo()
	{
		if (tileMap == null)
		{
			GD.PrintErr("UndoRedoManager: TileMap not initialized! Cannot undo.");
			return;
		}

		if (undoStack.Count > 0)
		{
			EditorAction lastAction = undoStack[undoStack.Count - 1];
			undoStack.RemoveAt(undoStack.Count - 1);

			lastAction.Undo(tileMap);
			redoStack.Add(lastAction);

			EmitSignal(nameof(UndoStackChanged), undoStack.Count > 0);
			EmitSignal(nameof(RedoStackChanged), true);
			EmitSignal(nameof(HistoryChanged)); // Emit after undo
			// GD.Print("UndoRedoManager: HistoryChanged signal emitted after Undo.");
			// GD.Print("Action undone. Undo stack: " + undoStack.Count + ", Redo stack: " + redoStack.Count);
		}
		else
		{
			// GD.Print("Undo stack empty.");
		}
	}

	public void Redo()
	{
		if (tileMap == null)
		{
			GD.PrintErr("UndoRedoManager: TileMap not initialized! Cannot redo.");
			return;
		}

		if (redoStack.Count > 0)
		{
			EditorAction nextAction = redoStack[redoStack.Count - 1];
			redoStack.RemoveAt(redoStack.Count - 1);

			nextAction.Execute(tileMap); // Re-execute the action
			undoStack.Add(nextAction);

			EmitSignal(nameof(UndoStackChanged), true);
			EmitSignal(nameof(RedoStackChanged), redoStack.Count > 0);
			EmitSignal(nameof(HistoryChanged)); // Emit after redo
			// GD.Print("UndoRedoManager: HistoryChanged signal emitted after Redo.");
			// GD.Print("Action redone. Undo stack: " + undoStack.Count + ", Redo stack: " + redoStack.Count);
		}
		else
		{
			// GD.Print("Redo stack empty.");
		}
	}

	public bool CanUndo() => tileMap != null && undoStack.Count > 0;
	public bool CanRedo() => tileMap != null && redoStack.Count > 0;

	public void ClearHistory()
	{
		bool couldUndo = undoStack.Count > 0;
		bool couldRedo = redoStack.Count > 0;

		undoStack.Clear();
		redoStack.Clear();

		if (couldUndo) EmitSignal(nameof(UndoStackChanged), false);
		if (couldRedo) EmitSignal(nameof(RedoStackChanged), false);
		EmitSignal(nameof(HistoryChanged)); // History is now empty
		GD.Print("UndoRedoManager: History cleared.");
	}
}
