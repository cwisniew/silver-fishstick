using Godot;
using System.Collections.Generic; // For List<>

// Ensure correct using statements if types are in namespaces
// e.g., using YourProject.Scripts.Core;
// e.g., using YourProject.Scripts.Data; // For MapSaverLoader content
// e.g., using YourProject.Scripts.Nodes; // For PlacedObject

public partial class RemoveLayerAction : EditorAction
{
    private int removedLayerIndex;
    private string removedLayerName;
    private int removedLayerZIndex;
    private bool removedLayerIsEnabled;
    // TODO: Add other layer properties if they are saved/loaded (e.g. modulate, custom_data, etc.)

    private List<MapSaverLoader.TileSaveData> tilesOnRemovedLayer;
    private List<MapSaverLoader.PlacedObjectSaveData> objectsOnRemovedLayer;

    private NodePath placedObjectsRootPath;
    private string placedObjectScenePath;
    private AssetManager assetManagerInstance;

    private int previousCurrentDrawingLayer = -1;
    private bool wasDrawingLayerRemoved = false;


    public RemoveLayerAction(int layerIndexToRemove, TileDrawer tileDrawer, NodePath objectsRootPath, AssetManager manager, string pObjectScenePath)
    {
        this.removedLayerIndex = layerIndexToRemove;
        this.placedObjectsRootPath = objectsRootPath;
        this.assetManagerInstance = manager;
        this.placedObjectScenePath = pObjectScenePath;

        if (tileDrawer == null || manager == null || string.IsNullOrEmpty(pObjectScenePath) || objectsRootPath == null)
        {
            GD.PrintErr("RemoveLayerAction Constructor: Null argument provided. Action may not function correctly.");
            // Initialize lists to prevent null reference errors later, though action will be problematic
            this.tilesOnRemovedLayer = new List<MapSaverLoader.TileSaveData>();
            this.objectsOnRemovedLayer = new List<MapSaverLoader.PlacedObjectSaveData>();
            return;
        }

        this.removedLayerName = tileDrawer.GetLayerName(layerIndexToRemove);
        this.removedLayerZIndex = tileDrawer.GetLayerZIndex(layerIndexToRemove);
        this.removedLayerIsEnabled = tileDrawer.IsLayerEnabled(layerIndexToRemove);
        // TODO: Capture other layer properties like modulate, YSortEnabled etc.

        this.tilesOnRemovedLayer = new List<MapSaverLoader.TileSaveData>();
        Godot.Collections.Array<Vector2I> usedCells = tileDrawer.GetUsedCells(layerIndexToRemove);
        foreach (Vector2I cellPos in usedCells)
        {
            int sourceId = tileDrawer.GetCellSourceId(layerIndexToRemove, cellPos);
            Vector2I atlasCoords = tileDrawer.GetCellAtlasCoords(layerIndexToRemove, cellPos);
            // int alternativeTile = tileDrawer.GetCellAlternativeTile(layerIndexToRemove, cellPos); // If needed
            if (sourceId != -1) // Only save actual tiles
            {
                tilesOnRemovedLayer.Add(new MapSaverLoader.TileSaveData
                {
                    X = cellPos.X, Y = cellPos.Y,
                    SourceId = sourceId, AtlasX = atlasCoords.X, AtlasY = atlasCoords.Y,
                    LayerIndex = layerIndexToRemove // Though on Undo, it will be this layer
                });
            }
        }

        this.objectsOnRemovedLayer = new List<MapSaverLoader.PlacedObjectSaveData>();
        Node objectsRootNode = tileDrawer.GetTree().Root.GetNode(objectsRootPath);
        if (objectsRootNode != null)
        {
            foreach (Node child in objectsRootNode.GetChildren())
            {
                if (child is PlacedObject po && po.MapLayerIndex == layerIndexToRemove)
                {
                    objectsOnRemovedLayer.Add(new MapSaverLoader.PlacedObjectSaveData
                    {
                        AssetNameRef = po.AssetNameRef, MapLayerIndex = po.MapLayerIndex,
                        PosX = po.GlobalPosition.X, PosY = po.GlobalPosition.Y,
                        RotationDegrees = po.CurrentRotationDegrees,
                        ScaleX = po.CurrentScale.X, ScaleY = po.CurrentScale.Y
                        // TODO: Add custom properties if PlacedObject has them
                    });
                }
            }
        } else {
             GD.PrintErr($"RemoveLayerAction Constructor: Could not find PlacedObjectsRoot at path: {objectsRootPath}");
        }
    }

    public override void Execute(TileMap tileMap)
    {
        if (!(tileMap is TileDrawer tileDrawerInstance)) {
            GD.PrintErr("RemoveLayerAction Execute: tileMap context is not a TileDrawer instance.");
            return;
        }

        previousCurrentDrawingLayer = tileDrawerInstance.CurrentDrawingLayer;
        wasDrawingLayerRemoved = (previousCurrentDrawingLayer == removedLayerIndex);

        Node objectsRootNode = tileDrawerInstance.GetTree().Root.GetNode(placedObjectsRootPath);
        if (objectsRootNode != null)
        {
            List<PlacedObject> objectsToRemoveFromScene = new List<PlacedObject>();
            foreach (Node child in objectsRootNode.GetChildren())
            {
                if (child is PlacedObject po && po.MapLayerIndex == removedLayerIndex)
                {
                    objectsToRemoveFromScene.Add(po);
                }
            }
            foreach(PlacedObject poInstance in objectsToRemoveFromScene)
            {
                GD.Print($"RemoveLayerAction Execute: Removing object '{poInstance.AssetNameRef}' from scene (was on layer {removedLayerIndex})");
                poInstance.GetParent().RemoveChild(poInstance);
                poInstance.QueueFree();
            }
        }

        if (removedLayerIndex >= 0 && removedLayerIndex < tileMap.GetLayersCount())
        {
            tileMap.RemoveLayer(removedLayerIndex);
            GD.Print($"RemoveLayerAction Execute: Removed TileMap Layer '{removedLayerName}' (was at index {removedLayerIndex}).");
        } else {
            GD.PrintErr($"RemoveLayerAction Execute: Invalid layer index {removedLayerIndex} or layer already removed (current count: {tileMap.GetLayersCount()}).");
            return;
        }

        int currentLayersCount = tileMap.GetLayersCount();
        if (currentLayersCount == 0) {
             // LayersPanelController should handle adding a default layer if count becomes 0 after this action.
             // TileDrawer might need a specific state for "no layer selected".
             tileDrawerInstance.SetCurrentDrawingLayer(-1); // Indicate no valid layer
        } else {
            if (wasDrawingLayerRemoved)
            {
                // Select the layer that took the place of the removed one, or the new last layer.
                // Both removedLayerIndex and currentLayersCount - 1 are >= 0 here, so newLayerToSelect will be >= 0.
                tileDrawerInstance.SetCurrentDrawingLayer(Mathf.Min(removedLayerIndex, currentLayersCount - 1));
            }
            else if (previousCurrentDrawingLayer > removedLayerIndex)
            {
                tileDrawerInstance.SetCurrentDrawingLayer(previousCurrentDrawingLayer - 1);
            }
            // else current drawing layer was below removed one, index is still valid or was already invalid.
        }
    }

    public override void Undo(TileMap tileMap)
    {
         if (!(tileMap is TileDrawer tileDrawerInstance)) {
             GD.PrintErr("RemoveLayerAction Undo: tileMap context is not a TileDrawer instance.");
             return;
         }
         if (assetManagerInstance == null) {
             GD.PrintErr("RemoveLayerAction Undo: AssetManager instance is missing."); return;
         }
         if (string.IsNullOrEmpty(placedObjectScenePath)) {
             GD.PrintErr("RemoveLayerAction Undo: PlacedObject scene path is missing."); return;
         }


        tileMap.AddLayer(-1);
        int newRuntimeIndex = tileMap.GetLayersCount() - 1;

        // Move to original slot if it's valid and different
        if (removedLayerIndex >= 0 && removedLayerIndex <= newRuntimeIndex) // Allow inserting at end if removedLayerIndex == newRuntimeIndex+1 (original end)
        {
             if (newRuntimeIndex != removedLayerIndex) {
                tileMap.MoveLayer(newRuntimeIndex, removedLayerIndex);
             }
        } else {
            GD.PrintErr($"RemoveLayerAction Undo: Invalid original removedLayerIndex {removedLayerIndex} for current layer count {tileMap.GetLayersCount()}. Layer restored at end.");
            // If this happens, the 'removedLayerIndex' for tiles/objects might be wrong if it's not the actual index.
            // For simplicity, assume it's restored at removedLayerIndex or MoveLayer handles it.
            // If MoveLayer fails, tiles/objects will be on the wrong layer.
            // Let's assume removedLayerIndex will be the valid index after MoveLayer or if it was added at the end correctly.
        }

        tileMap.SetLayerName(removedLayerIndex, removedLayerName);
        tileMap.SetLayerZIndex(removedLayerIndex, removedLayerZIndex);
        tileMap.SetLayerEnabled(removedLayerIndex, removedLayerIsEnabled);
        // TODO: Restore other layer properties (modulate, YSortEnabled, etc.)

        GD.Print($"RemoveLayerAction Undo: Restored TileMap Layer '{removedLayerName}' to index {removedLayerIndex}.");

        foreach (var tileData in tilesOnRemovedLayer)
        {
            tileMap.SetCell(removedLayerIndex, new Vector2I(tileData.X, tileData.Y), tileData.SourceId, new Vector2I(tileData.AtlasX, tileData.AtlasY));
        }

        Node objectsRootNode = tileDrawerInstance.GetTree().Root.GetNode(placedObjectsRootPath);
        PackedScene scene = ResourceLoader.Load<PackedScene>(this.placedObjectScenePath);
        if (objectsRootNode != null && scene != null)
        {
            foreach (var objectData in objectsOnRemovedLayer)
            {
                AssetData assetData = assetManagerInstance.GetAssetByName(objectData.AssetNameRef);
                if (assetData != null) {
                    Node newInstance = scene.Instantiate();
                    if (newInstance is PlacedObject po)
                    {
                        // Ensure MapLayerIndex is the restored layer's actual index
                        po.Initialize(assetData, removedLayerIndex);
                        po.GlobalPosition = new Vector2(objectData.PosX, objectData.PosY);
                        po.CurrentRotationDegrees = objectData.RotationDegrees;
                        po.CurrentScale = new Vector2(objectData.ScaleX, objectData.ScaleY);
                        objectsRootNode.AddChild(po);
                        GD.Print($"RemoveLayerAction Undo: Restored object '{po.AssetNameRef}' to layer {removedLayerIndex}");
                    } else { newInstance.QueueFree(); GD.PrintErr("RemoveLayerAction Undo: Failed to cast new instance to PlacedObject.");}
                } else { GD.PrintErr($"RemoveLayerAction Undo: Could not find AssetData for '{objectData.AssetNameRef}'. Object not restored."); }
            }
        } else {
            if(objectsRootNode == null) GD.PrintErr("RemoveLayerAction Undo: PlacedObjectsRoot node not found.");
            if(scene == null) GD.PrintErr($"RemoveLayerAction Undo: Failed to load PlacedObject scene from '{this.placedObjectScenePath}'.");
        }

        // Restore CurrentDrawingLayer selection
        // After re-adding and moving, layer indices might have shifted.
        // The 'previousCurrentDrawingLayer' was relative to the state *before* Execute.
        // If it was the one removed, select 'removedLayerIndex'.
        // If it was after the one removed, its index effectively increased by 1.
        // If it was before, its index is unchanged.
        int layerToSelectAfterUndo = previousCurrentDrawingLayer;
        if (wasDrawingLayerRemoved) {
            layerToSelectAfterUndo = removedLayerIndex;
        } else if (previousCurrentDrawingLayer >= removedLayerIndex) {
            layerToSelectAfterUndo = previousCurrentDrawingLayer + 1; // Shifted up due to insertion
        }
        // else: previousCurrentDrawingLayer < removedLayerIndex, index is fine.

        if (IsValidLayerIndex(tileDrawerInstance, layerToSelectAfterUndo)) {
             tileDrawerInstance.SetCurrentDrawingLayer(layerToSelectAfterUndo);
        } else if (IsValidLayerIndex(tileDrawerInstance, removedLayerIndex)) { // Fallback to the restored layer itself
             tileDrawerInstance.SetCurrentDrawingLayer(removedLayerIndex);
        } else if (tileDrawerInstance.GetLayersCount() > 0) { // Fallback to first valid layer
             tileDrawerInstance.SetCurrentDrawingLayer(0);
        } else {
             tileDrawerInstance.SetCurrentDrawingLayer(-1); // No valid layer
        }
    }

    private bool IsValidLayerIndex(TileDrawer td, int index) {
        return index >= 0 && index < td.GetLayersCount();
    }
}
