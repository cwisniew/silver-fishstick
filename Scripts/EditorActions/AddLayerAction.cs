using Godot;

// Assuming TileDrawer, EditorAction are accessible, ensure correct using statements if they are in namespaces
// e.g., using YourProject.Scripts.Core;
// e.g., using YourProject.Scripts.Tools;

public partial class AddLayerAction : EditorAction
{
    private string layerName;
    private int layerZIndex;
    // private bool layerIsEnabled; // Could add more properties if needed for constructor

    private int addedLayerActualIndex = -1; // Stores the actual index after TileMap.AddLayer()
    private int previousCurrentDrawingLayer = -1; // To restore on undo

    // Constructor can take initial properties for the new layer
    public AddLayerAction(string name = null, int zIndex = 0) // Removed isEnabled from constructor for now, new layers default to enabled
    {
        this.layerName = name;
        this.layerZIndex = zIndex;
    }

    public override void Execute(TileMap tileMap)
    {
        if (!(tileMap is TileDrawer tileDrawerInstance))
        {
            GD.PrintErr("AddLayerAction Execute: tileMap context is not a TileDrawer instance.");
            return;
        }

        previousCurrentDrawingLayer = tileDrawerInstance.CurrentDrawingLayer;

        tileMap.AddLayer(-1); // Add to the end, Godot will assign it an index
        addedLayerActualIndex = tileMap.GetLayersCount() - 1;

        string finalLayerName = string.IsNullOrEmpty(layerName) ? $"Layer {addedLayerActualIndex}" : layerName;
        tileMap.SetLayerName(addedLayerActualIndex, finalLayerName);
        tileMap.SetLayerZIndex(addedLayerActualIndex, layerZIndex); // Set desired Z-index
        tileMap.SetLayerEnabled(addedLayerActualIndex, true); // New layers are visible by default

        // Set the new layer as the current drawing layer
        tileDrawerInstance.SetCurrentDrawingLayer(addedLayerActualIndex);

        GD.Print($"AddLayerAction Execute: Added Layer '{finalLayerName}' at index {addedLayerActualIndex} with Z-index {layerZIndex}. Set as current drawing layer.");
    }

    public override void Undo(TileMap tileMap)
    {
        if (!(tileMap is TileDrawer tileDrawerInstance))
        {
            GD.PrintErr("AddLayerAction Undo: tileMap context is not a TileDrawer instance.");
            return;
        }

        if (addedLayerActualIndex != -1 && addedLayerActualIndex < tileMap.GetLayersCount())
        {
            // It's possible another action (like delete layer) modified the layer count or indices.
            // We should only remove if the layer we expect to remove is still there.
            // However, TileMap.RemoveLayer(index) is robust. If index is out of bounds, it errors.
            // Let's assume for now that the index is valid if this action is being undone correctly.
            string nameOfRemovedLayer = tileMap.GetLayerName(addedLayerActualIndex); // Get name before removing
            tileMap.RemoveLayer(addedLayerActualIndex);
            GD.Print($"AddLayerAction Undo: Removed Layer '{nameOfRemovedLayer}' (was at index {addedLayerActualIndex}).");

            // Important: After removing a layer, indices above it shift down.
            // The previousCurrentDrawingLayer needs to be adjusted if it was above the removed layer.
            // However, CurrentDrawingLayer is just an index. TileDrawer should handle selection logic.

            // Restore previous drawing layer selection
            // If previousCurrentDrawingLayer was the one just removed, it will be invalid.
            // If previousCurrentDrawingLayer was > addedLayerActualIndex, it effectively shifts down by 1.

            int layerToSelectAfterUndo = previousCurrentDrawingLayer;
            if (previousCurrentDrawingLayer == addedLayerActualIndex) { // If we were drawing on the layer we just removed
                 // Select the layer that is now at the removed layer's index, or the one before, or 0
                layerToSelectAfterUndo = Mathf.Min(addedLayerActualIndex, tileMap.GetLayersCount() -1 );
                 if (tileMap.GetLayersCount() == 0) layerToSelectAfterUndo = -1; // No layers left
                 else layerToSelectAfterUndo = Mathf.Max(0, layerToSelectAfterUndo);

            } else if (previousCurrentDrawingLayer > addedLayerActualIndex) {
                layerToSelectAfterUndo = previousCurrentDrawingLayer - 1; // Adjust for shifted index
            }
            // If previousCurrentDrawingLayer < addedLayerActualIndex, its index remains valid.

            if (tileMap.GetLayersCount() > 0) {
                 tileDrawerInstance.SetCurrentDrawingLayer(Mathf.Clamp(layerToSelectAfterUndo, 0, tileMap.GetLayersCount() - 1));
            } else {
                 // No layers left. LayersPanelController should ensure at least one layer always exists if this is an issue.
                 // TileDrawer might need a specific state for no valid drawing layer.
                 tileDrawerInstance.SetCurrentDrawingLayer(-1); // Or some other indicator of no valid layer
                 GD.Print("AddLayerAction Undo: No layers left after undo or fallback selection failed.");
            }

            addedLayerActualIndex = -1; // Reset for potential redo
        }
        else
        {
            GD.PrintErr($"AddLayerAction Undo: Invalid addedLayerActualIndex ({addedLayerActualIndex}) or layer already removed (current layer count: {tileMap.GetLayersCount()}).");
        }
    }
}
