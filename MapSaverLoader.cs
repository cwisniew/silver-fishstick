using Godot;
using System.Collections.Generic;
// Using Godot.Json for serialization/deserialization now
// using System.Text.Json; // Will be replaced by Godot.Json

public static class MapSaverLoader
{
	public class PlacedObjectSaveData
	{
		public string AssetNameRef { get; set; }
		public int MapLayerIndex { get; set; }
		public float PosX { get; set; }
		public float PosY { get; set; }
		public float RotationDegrees { get; set; } = 0.0f;
		public float ScaleX { get; set; } = 1.0f;
		public float ScaleY { get; set; } = 1.0f;
	}

	public class MapDataContainer
	{
		// Need to use Godot collection types if using Godot.Json for direct object serialization,
		// or handle conversion if using System.Text.Json with List<>.
		// For Godot.Json.Stringify, it's often easier if the root is a Godot.Collections.Dictionary.
		// However, if we manually build the dictionary for Stringify from these C# classes, it's fine.
		// Let's assume we'll manually construct the dictionary for Stringify from these C# classes with List<>.
		public List<TileSaveData> Tiles { get; set; } = new List<TileSaveData>();
		public List<PlacedObjectSaveData> PlacedObjects { get; set; } = new List<PlacedObjectSaveData>();
	}

	public class TileSaveData
	{
		public int X { get; set; }
		public int Y { get; set; }
		public int SourceId { get; set; }
		public int AtlasX { get; set; }
		public int AtlasY { get; set; }
		public int LayerIndex { get; set; } // Added
		// public int AlternativeTileId { get; set; } // Optional for Godot 4.x features
	}

	public static void SaveMap(TileMap tileMap, string filePath)
	{
		if (tileMap == null)
		{
			GD.PrintErr("MapSaverLoader.SaveMap: TileMap node is null.");
			return;
		}

		List<TileSaveData> tilesToSave = new List<TileSaveData>();
		int layersCount = tileMap.GetLayersCount();

		for (int layer = 0; layer < layersCount; layer++)
		{
			Godot.Collections.Array<Vector2I> usedCellsOnLayer = tileMap.GetUsedCells(layer);
			foreach (Vector2I cellPos in usedCellsOnLayer)
			{
				int sourceId = tileMap.GetCellSourceId(layer, cellPos);
				Vector2I atlasCoords = tileMap.GetCellAtlasCoords(layer, cellPos);
				// int alternativeTile = tileMap.GetCellAlternativeTile(layer, cellPos); // If using this feature

				if (sourceId != -1) // Only save actual tiles
				{
					tilesToSave.Add(new TileSaveData
					{
						X = cellPos.X,
						Y = cellPos.Y,
						SourceId = sourceId,
						AtlasX = atlasCoords.X,
						AtlasY = atlasCoords.Y,
						LayerIndex = layer // Store the layer index
						// AlternativeTileId = alternativeTile // If using this
					});
				}
			}
		}

		try
		{
			// --- Create MapDataContainer ---
			MapDataContainer mapDataToSave = new MapDataContainer();
			mapDataToSave.Tiles = tilesToSave;

			// --- Collect Placed Objects ---
			// Path assumes TileMap node (TileDrawer) is a child of MainScene root, and PlacedObjectsRoot is also child of MainScene root.
			Node2D placedObjectsRoot = tileMap.GetNode<Node2D>("../PlacedObjectsRoot");
			if (placedObjectsRoot != null)
			{
				foreach (Node child in placedObjectsRoot.GetChildren())
				{
					if (child is PlacedObject placedObjectScript)
					{
						mapDataToSave.PlacedObjects.Add(new PlacedObjectSaveData
						{
							AssetNameRef = placedObjectScript.AssetNameRef,
							MapLayerIndex = placedObjectScript.MapLayerIndex,
							PosX = placedObjectScript.GlobalPosition.X,
							PosY = placedObjectScript.GlobalPosition.Y,
							RotationDegrees = placedObjectScript.CurrentRotationDegrees,
							ScaleX = placedObjectScript.CurrentScale.X,
							ScaleY = placedObjectScript.CurrentScale.Y
						});
					}
				}
			} else { GD.PrintErr("MapSaverLoader.SaveMap: PlacedObjectsRoot node not found at ../PlacedObjectsRoot relative to TileMap. Objects not saved."); }

			// --- Serialize using Godot.Json ---
			// For Godot.Json.Stringify to work well with custom C# classes containing List<T>,
			// the lists themselves might need to be Godot.Collections.Array or the whole container a Dictionary.
			// A common approach is to convert to a Godot Dictionary first.
			var saveDict = new Godot.Collections.Dictionary();
			var tilesArray = new Godot.Collections.Array();
			foreach(var tile in mapDataToSave.Tiles) {
				tilesArray.Add(new Godot.Collections.Dictionary {
					{"X", tile.X}, {"Y", tile.Y}, {"SourceId", tile.SourceId},
					{"AtlasX", tile.AtlasX}, {"AtlasY", tile.AtlasY}, {"LayerIndex", tile.LayerIndex}
				});
			}
			saveDict["Tiles"] = tilesArray;

			var objectsArray = new Godot.Collections.Array();
			foreach(var obj in mapDataToSave.PlacedObjects) {
				objectsArray.Add(new Godot.Collections.Dictionary {
					{"AssetNameRef", obj.AssetNameRef}, {"MapLayerIndex", obj.MapLayerIndex},
					{"PosX", obj.PosX}, {"PosY", obj.PosY},
					{"RotationDegrees", obj.RotationDegrees}, // Added
					{"ScaleX", obj.ScaleX},                   // Added
					{"ScaleY", obj.ScaleY}                    // Added
				});
			}
			saveDict["PlacedObjects"] = objectsArray;

			string jsonString = Json.Stringify(saveDict, "\t"); // Pretty print

			using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PrintErr($"MapSaverLoader.SaveMap: Error opening file for writing: {FileAccess.GetOpenError()}");
				return;
			}
			file.StoreString(jsonString);
			GD.Print($"MapSaverLoader.SaveMap: Map saved successfully to {filePath} with {tilesToSave.Count} tiles and {mapDataToSave.PlacedObjects.Count} objects across {layersCount} layers.");
		}
		catch (System.Exception e)
		{
			GD.PrintErr($"SaveMap: Error serializing or writing map data: {e.Message}");
		}
	}

	public static void LoadMap(TileMap tileMap, string filePath)
	{
		if (tileMap == null) { GD.PrintErr("MapSaverLoader.LoadMap: TileMap node is null."); return; }
		if (!FileAccess.FileExists(filePath)) { GD.PrintErr($"MapSaverLoader.LoadMap: File not found: {filePath}"); return; }

		MapDataContainer loadedMapData = new MapDataContainer();

		try
		{
			using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
			if (file == null) { GD.PrintErr($"MapSaverLoader.LoadMap: Error opening file: {FileAccess.GetOpenError()}"); return; }

			string jsonString = file.GetAsText();
			Variant parsedVariant = Json.ParseString(jsonString);

			if (parsedVariant.VariantType == Variant.Type.Nil) {
				GD.PrintErr($"MapSaverLoader.LoadMap: Failed to parse JSON. Error: {Json.GetErrorMessage()} at line {Json.GetErrorLine()}.");
				return;
			}

			var mapDict = parsedVariant.AsGodotDictionary();

			if (mapDict.TryGetValue("Tiles", out Variant tilesVar) && tilesVar.VariantType == Variant.Type.Array)
			{
				var tilesArray = tilesVar.AsGodotArray();
				foreach (var tileEntryVar in tilesArray)
			{
					var tileDict = tileEntryVar.AsGodotDictionary();
					loadedMapData.Tiles.Add(new TileSaveData {
						X = tileDict.GetValueOrDefault("X", 0).AsInt32(),
						Y = tileDict.GetValueOrDefault("Y", 0).AsInt32(),
						SourceId = tileDict.GetValueOrDefault("SourceId", -1).AsInt32(),
						AtlasX = tileDict.GetValueOrDefault("AtlasX", -1).AsInt32(),
						AtlasY = tileDict.GetValueOrDefault("AtlasY", -1).AsInt32(),
						LayerIndex = tileDict.GetValueOrDefault("LayerIndex", 0).AsInt32()
					});
			}
		}

			if (mapDict.TryGetValue("PlacedObjects", out Variant objectsVar) && objectsVar.VariantType == Variant.Type.Array)
			{
				var objectsArray = objectsVar.AsGodotArray();
				foreach (var objEntryVar in objectsArray)
			{
					var objDict = objEntryVar.AsGodotDictionary();
					loadedMapData.PlacedObjects.Add(new PlacedObjectSaveData {
						AssetNameRef = objDict.GetValueOrDefault("AssetNameRef", "").ToString(),
						MapLayerIndex = objDict.GetValueOrDefault("MapLayerIndex", 0).AsInt32(),
						PosX = (float)objDict.GetValueOrDefault("PosX", 0.0).AsDouble(),
						PosY = (float)objDict.GetValueOrDefault("PosY", 0.0).AsDouble(),
						RotationDegrees = (float)objDict.GetValueOrDefault("RotationDegrees", 0.0).AsDouble(), // Added
						ScaleX = (float)objDict.GetValueOrDefault("ScaleX", 1.0).AsDouble(),                   // Added
						ScaleY = (float)objDict.GetValueOrDefault("ScaleY", 1.0).AsDouble()                    // Added
					});
			}
			}
		}
		catch (System.Exception e) { GD.PrintErr($"MapSaverLoader.LoadMap: Error reading or deserializing: {e.Message}"); return; }

		tileMap.Clear();

		// --- Load Tiles ---
		int maxLayerIndexRequired = -1;
		if (loadedMapData.Tiles.Count > 0) {
			foreach (TileSaveData tileData in loadedMapData.Tiles) {
				if (tileData.LayerIndex > maxLayerIndexRequired) maxLayerIndexRequired = tileData.LayerIndex;
			}
		}
		// Also check for layers from objects, in case map has objects but no tiles on higher layers
		if (loadedMapData.PlacedObjects.Count > 0) {
			foreach (PlacedObjectSaveData objData in loadedMapData.PlacedObjects) {
				if (objData.MapLayerIndex > maxLayerIndexRequired) maxLayerIndexRequired = objData.MapLayerIndex;
			}
		}


		if (maxLayerIndexRequired == -1 && loadedMapData.Tiles.Count == 0 && loadedMapData.PlacedObjects.Count == 0) {
			GD.Print("MapSaverLoader.LoadMap: Loaded map data is empty. TileMap cleared.");
			return;
		}

		for (int i = 0; i <= maxLayerIndexRequired; i++) {
			if (i >= tileMap.GetLayersCount()) {
				tileMap.AddLayer(-1);
				tileMap.SetLayerName(i, $"Layer {i} (Loaded)");
			}
		}

		foreach (TileSaveData tileSaveEntry in loadedMapData.Tiles) {
			Vector2I cellCoords = new Vector2I(tileSaveEntry.X, tileSaveEntry.Y);
			if (tileSaveEntry.LayerIndex < tileMap.GetLayersCount()) {
				tileMap.SetCell(
					tileSaveEntry.LayerIndex, cellCoords, tileSaveEntry.SourceId,
					new Vector2I(tileSaveEntry.AtlasX, tileSaveEntry.AtlasY)
				);
			} else {
				GD.PrintErr($"MapSaverLoader.LoadMap: Tile on non-existent layer {tileSaveEntry.LayerIndex}. Max layer is {tileMap.GetLayersCount()-1}.");
			}
		}
		GD.Print($"MapSaverLoader.LoadMap: Loaded {loadedMapData.Tiles.Count} tiles.");

		// --- Load Placed Objects ---
		Node2D placedObjectsRoot = tileMap.GetNode<Node2D>("../PlacedObjectsRoot"); // Assumes TileDrawer is child of MainScene
		AssetManager assetManager = tileMap.GetNode<AssetManager>("/root/AssetManager");

		if (placedObjectsRoot == null) { GD.PrintErr("MapSaverLoader.LoadMap: PlacedObjectsRoot not found! Cannot load objects."); return; }
		if (assetManager == null) { GD.PrintErr("MapSaverLoader.LoadMap: AssetManager not found! Cannot load objects."); return; }

		foreach (Node child in placedObjectsRoot.GetChildren()) { child.QueueFree(); } // Clear existing

		if (loadedMapData.PlacedObjects != null && loadedMapData.PlacedObjects.Count > 0) {
			PackedScene placedObjectScene = ResourceLoader.Load<PackedScene>(TileDrawer.PlacedObjectScenePath);
			if (placedObjectScene == null) {
				GD.PrintErr($"MapSaverLoader.LoadMap: Failed to load PlacedObject scene from '{TileDrawer.PlacedObjectScenePath}'!");
				return;
			}

			foreach (PlacedObjectSaveData objSaveData in loadedMapData.PlacedObjects) {
				AssetData assetData = assetManager.GetAsset(objSaveData.AssetNameRef); // Use GetAsset
				if (assetData == null || assetData.Category != AssetManager.AssetCategory.PlaceableObject) {
					GD.PrintErr($"LoadMap: AssetData '{objSaveData.AssetNameRef}' not found or not PlaceableObject. Skipping.");
					continue;
			}
				Node newInstance = placedObjectScene.Instantiate();
				if (newInstance is PlacedObject placedObjectScript) {
					placedObjectScript.Initialize(assetData, objSaveData.MapLayerIndex);
					placedObjectScript.GlobalPosition = new Vector2(objSaveData.PosX, objSaveData.PosY);

					// Load and apply rotation and scale using the properties
					placedObjectScript.CurrentRotationDegrees = objSaveData.RotationDegrees;
					placedObjectScript.CurrentScale = new Vector2(objSaveData.ScaleX, objSaveData.ScaleY);

					placedObjectsRoot.AddChild(placedObjectScript);
				} else {
					GD.PrintErr("LoadMap: Failed to instance PlacedObject or cast to script.");
					newInstance.QueueFree();
			}
		}
		}
		GD.Print($"MapSaverLoader.LoadMap: Loaded {loadedMapData.PlacedObjects?.Count ?? 0} objects.");
		GD.Print($"MapSaverLoader.LoadMap: Full map load complete. Total layers in TileMap: {tileMap.GetLayersCount()}");
	}
}
