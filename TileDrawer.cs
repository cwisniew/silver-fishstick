using Godot;

public partial class TileDrawer : TileMap
{
	public const string PlacedObjectScenePath = "res://objects/PlacedObject.tscn";

	// Assuming MainScene.DrawingMode is accessible. If MainScene is in a different namespace, adjust.
	// For simplicity, if direct enum access is problematic without circular refs or complex setup,
	// we can pass int and cast. But Godot usually handles cross-script enum access if they are part of the same build.
	private MainScene.DrawingMode currentMode = MainScene.DrawingMode.Tile;
	private Vector2I roomDragStartPoint;
	private bool isDraggingRoom = false;

	private AssetManager assetManager;
	private UndoRedoManager undoRedoManager; // Added
	private AssetManager.AssetCategory currentSelectedAssetCategory = AssetManager.AssetCategory.General;

	// Fields for the currently selected tile's properties within the MasterTileSet
	private int currentTileSourceIdInMasterSet = -1;
	private Vector2I currentTileAtlasCoordsInSource = Vector2I.MinValue;
	private AssetData currentObjectAssetToPlace;

	public int CurrentDrawingLayer { get; private set; } = 0;
	private MainScene mainSceneInstance;
	private GlobalSettings globalSettings; // Added for snapping logic


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// It's generally safer to ensure TileMap has a TileSet assigned.
		// AssetManager now manages a MasterTileSet.tres. This should be assigned to the TileMap in the scene editor.
		if (this.TileSet == null)
		{
			GD.PrintErr("TileDrawer: TileMap node does not have a TileSet assigned! Please assign MasterTileSet.tres in the editor.");
		}

		assetManager = GetNode<AssetManager>("/root/AssetManager");
		if (assetManager == null)
		{
			GD.PrintErr("TileDrawer: AssetManager not found!");
		}

		undoRedoManager = GetNode<UndoRedoManager>("/root/UndoRedoManager");
		if (undoRedoManager == null)
		{
			GD.PrintErr("TileDrawer: UndoRedoManager not found! Undo/Redo will not work.");
		}

		// Connect to MainScene's drawing mode changed signal
		var mainSceneNode = GetTree().Root.GetNode<MainScene>("MainScene");
		if (mainSceneNode != null)
		{
			mainSceneNode.DrawingModeChanged += OnDrawingModeChanged;
			// Initialize with current mode from MainScene, in case TileDrawer is ready after MainScene sets initial mode.
			// This direct access is okay if MainScene's currentDrawingMode is public or has a public getter.
			// However, relying on the signal being emitted upon MainScene's _Ready or mode change is cleaner.
			// For now, the default currentMode = Tile should suffice until a signal is received.
			GD.Print("TileDrawer connected to MainScene's DrawingModeChanged signal.");

	mainSceneInstance = mainSceneNode; // Store the reference
		}
		else
		{
			GD.PrintErr("TileDrawer: Could not find MainScene node to connect DrawingModeChanged signal or store instance.");
		}

		globalSettings = GetNode<GlobalSettings>("/root/GlobalSettings");
		if (globalSettings == null) GD.PrintErr("TileDrawer: GlobalSettings node not found!");
	}

public void OnDrawingModeChanged(MainScene.DrawingMode newMode)
	{
	currentMode = newMode;
	isDraggingRoom = false;
	GD.Print($"TileDrawer: Internal mode changed to {newMode} via signal.");
	}


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void DrawTile(Vector2I position, int sourceId, Vector2I atlasCoords)
	{
		// Note: SetCell expects sourceId, atlasCoords, and alternativeTile.
		// For simplicity, we're using tileId directly as atlasCoords if it's a simple atlas.
		// The AssetData now includes SourceId and AtlasCoords.
		// The old tileId can be thought of as the direct ID if the TileSet is simple,
		// or an index into a more complex structure. For BasicTiles.tres, TileId 0 is atlas (0,0) and TileId 1 is (1,0) in source 0.
		SetCell(sourceId, atlasCoords, 0); // alternativeTile = 0 for now
	}

	// Updated SetCurrentTile to receive AssetData, or at least category along with IDs
	// Let's change it to receive the full AssetData object for clarity
	public void SetCurrentAsset(AssetData asset)
	{
		if (asset == null)
		{
			GD.PrintErr("SetCurrentAsset: Received null asset.");
			currentTileSourceIdInMasterSet = -1;
			currentObjectAssetToPlace = null;
			return;
		}

		this.currentSelectedAssetCategory = asset.Category;
		this.currentObjectAssetToPlace = null;

		// Use the mainSceneInstance field now
		if (mainSceneInstance == null) {
			GD.PrintErr("SetCurrentAsset: MainScene instance (mainSceneInstance field) is null. Cannot change drawing mode.");
			// Fallback logic as before
			if (asset.Category == AssetManager.AssetCategory.PlaceableObject) {
				this.currentObjectAssetToPlace = asset;
				this.currentTileSourceIdInMasterSet = -1;
				this.currentTileAtlasCoordsInSource = new Vector2I(-1,-1);
			} else {
				this.currentTileSourceIdInMasterSet = asset.SourceIdInTileSet;
				this.currentTileAtlasCoordsInSource = asset.AtlasCoordsInTileSet;
			}
			return;
		}

		if (asset.Category == AssetManager.AssetCategory.PlaceableObject)
		{
			this.currentObjectAssetToPlace = asset;
			this.currentTileSourceIdInMasterSet = -1;
			this.currentTileAtlasCoordsInSource = new Vector2I(-1,-1);

			mainSceneInstance.ChangeDrawingMode(MainScene.DrawingMode.ObjectPlacement);
			// GD.Print($"TileDrawer: Object '{asset.Name}' selected. MainScene mode requested: ObjectPlacement.");
		}
		else // It's a tile-based asset
		{
			this.currentTileSourceIdInMasterSet = asset.SourceIdInTileSet;
			this.currentTileAtlasCoordsInSource = asset.AtlasCoordsInTileSet;

			if (mainSceneInstance.GetCurrentDrawingMode() == MainScene.DrawingMode.ObjectPlacement ||
				mainSceneInstance.GetCurrentDrawingMode() == MainScene.DrawingMode.SelectObject) // Added SelectObject
			{
				 mainSceneInstance.ChangeDrawingMode(MainScene.DrawingMode.Tile);
				 mainSceneInstance.DeselectCurrentObject(); // Call new public method
				 GD.Print($"TileDrawer: Tile asset '{asset.Name}' selected. Switched to Tile mode, any selected object deselected.");
			}
			// GD.Print($"TileDrawer: Tile asset '{asset.Name}' selected. SourceID: {asset.SourceIdInTileSet}, Atlas: {asset.AtlasCoordsInTileSet}");
		}
	}


	public override void _UnhandledInput(InputEvent @event)
	{
		if (GetViewport().IsInputHandled()) return; // Added: respect if event was handled by MainScene (e.g. object selection)

		// 'this.currentMode' is updated by the OnDrawingModeChanged signal handler.

		if (this.currentMode == MainScene.DrawingMode.ObjectPlacement)
		{
			if (@event is InputEventMouseButton mouseButtonEvent &&
				mouseButtonEvent.ButtonIndex == MouseButton.Left &&
				mouseButtonEvent.Pressed)
			{
				if (currentObjectAssetToPlace != null)
				{
					Vector2 rawMouseGlobalPos = GetGlobalMousePosition();
					Vector2 finalPlacementPos;

					// Ensure globalSettings and mainSceneInstance are available (they should be from _Ready)
					// These null checks are good, but ideally, they should be guaranteed by _Ready() or have early outs.
					if (globalSettings == null) globalSettings = GetNode<GlobalSettings>("/root/GlobalSettings");
					if (mainSceneInstance == null) mainSceneInstance = GetNode<MainScene>("/root/MainScene");

					if (mainSceneInstance != null && globalSettings != null) // globalSettings check added for safety
					{
						// Prepare arguments for the new ApplySnappingRules signature
						PlacedObject.GlobalSnapPoints hypotheticalSnapPoints;
						if (currentObjectAssetToPlace != null && currentObjectAssetToPlace.PreviewTexture != null) {
							Texture2D objectTexture = currentObjectAssetToPlace.PreviewTexture;
							Vector2 textureSize = objectTexture.GetSize();
							// Assume initial scale of 1,1 for a new object for preview snapping
							float scaledHalfWidth = textureSize.X / 2.0f;
							float scaledHalfHeight = textureSize.Y / 2.0f;
							hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints {
								Center = rawMouseGlobalPos,
								LeftX = rawMouseGlobalPos.X - scaledHalfWidth, RightX = rawMouseGlobalPos.X + scaledHalfWidth,
								TopY = rawMouseGlobalPos.Y - scaledHalfHeight, BottomY = rawMouseGlobalPos.Y + scaledHalfHeight
							};
						} else {
							hypotheticalSnapPoints = new PlacedObject.GlobalSnapPoints { // Fallback if no texture
								Center = rawMouseGlobalPos, LeftX = rawMouseGlobalPos.X, RightX = rawMouseGlobalPos.X,
								TopY = rawMouseGlobalPos.Y, BottomY = rawMouseGlobalPos.Y
							};
						}

						List<PlacedObject> allObjectsInScene = new List<PlacedObject>();
						Node placedObjectsRootNode = mainSceneInstance.GetNodeOrNull("PlacedObjectsRoot");
						if (placedObjectsRootNode != null) {
							foreach (Node child in placedObjectsRootNode.GetChildren()) {
								if (child is PlacedObject po) {
									allObjectsInScene.Add(po);
								}
							}
						}

						finalPlacementPos = mainSceneInstance.ApplySnappingRules(
							rawMouseGlobalPos,
							hypotheticalSnapPoints,
							null, // objectBeingMovedOrNull is null for new placement
							allObjectsInScene,
							out bool xObjSnapped, out float xObjSnapLine,
							out bool yObjSnapped, out float yObjSnapLine,
							out bool xGridSn,   out float xGridLine,
							out bool yGridSn,   out float yGridLine
						);

						// Snap line display for placement
						SnapFeedbackDisplay feedbackDisplay = mainSceneInstance.GetSnapFeedbackDisplay();
						if (feedbackDisplay != null) {
							feedbackDisplay.ClearLines();
							Rect2 vpRect = mainSceneInstance.GetViewportRect();

							if (xObjSnapped) {
								feedbackDisplay.AddSnapLine(new Vector2(xObjSnapLine, vpRect.Position.Y), new Vector2(xObjSnapLine, vpRect.End.Y), Colors.Aqua);
							}
							if (yObjSnapped) {
								feedbackDisplay.AddSnapLine(new Vector2(vpRect.Position.X, yObjSnapLine), new Vector2(vpRect.End.X, yObjSnapLine), Colors.Aqua);
							}
							if (xGridSn) {
								feedbackDisplay.AddSnapLine(new Vector2(xGridLine, vpRect.Position.Y), new Vector2(xGridLine, vpRect.End.Y), Colors.LightGreen);
							}
							if (yGridSn) {
								feedbackDisplay.AddSnapLine(new Vector2(vpRect.Position.X, yGridLine), new Vector2(vpRect.End.X, yGridLine), Colors.LightGreen);
							}
						}
					}
					else
					{
						GD.PrintErr("TileDrawer: MainScene or GlobalSettings instance is null. Cannot apply snapping rules. Using raw mouse position.");
						finalPlacementPos = rawMouseGlobalPos;
					}
					// --- End Snapping Logic ---

					Node2D objectsRoot = GetNode<Node2D>("/root/MainScene/PlacedObjectsRoot");
					if (objectsRoot == null) {
						GD.PrintErr("TileDrawer: PlacedObjectsRoot node not found at /root/MainScene/PlacedObjectsRoot! Cannot place object.");
						return;
					}
					if (assetManager == null || undoRedoManager == null) {
						 GD.PrintErr("TileDrawer: AssetManager or UndoRedoManager not available! Cannot place object with undo/redo.");
						 return;
					}
					if (mainSceneInstance == null) { // Added check for mainSceneInstance
						GD.PrintErr("TileDrawer: MainScene instance not available! Cannot check scatter mode.");
						return;
					}

					float objectRotation = 0.0f;
					if (mainSceneInstance.IsScatterModeEnabled())
					{
						objectRotation = (float)GD.RandRange(-180.0, 180.0);
					}

					PlaceObjectAction action = new PlaceObjectAction(
						PlacedObjectScenePath,
						currentObjectAssetToPlace.Name,
						finalPlacementPos, // Use the (potentially snapped) position
						this.CurrentDrawingLayer,
						objectRotation,
						assetManager,
						objectsRoot
					);

					action.Execute(this);
					undoRedoManager.RecordAction(action);

					GD.Print($"TileDrawer: PlaceObjectAction recorded for '{currentObjectAssetToPlace.Name}' (Rot: {objectRotation}°) at {finalPlacementPos} on layer {this.CurrentDrawingLayer}.");
					GetViewport().SetInputAsHandled();
				}
				else
				{
					GD.PrintErr("TileDrawer: ObjectPlacement mode active, but no currentObjectAssetToPlace selected.");
				}
			}
			// No explicit line clearing needed on InputEventMouseMotion for ObjectPlacement mode
			// if SnapFeedbackDisplay self-clears via _Process, as lines are only added on click attempt.
		}
		else if (this.currentMode == MainScene.DrawingMode.Tile || this.currentMode == MainScene.DrawingMode.Room)
		{
			// Existing logic for Tile and Room modes
			if (this.currentMode == MainScene.DrawingMode.Tile) {
				if (@event is InputEventMouseButton eventMouseButton)
				{
					if (eventMouseButton.ButtonIndex == MouseButton.Left && eventMouseButton.Pressed)
					{
						// Allow eraser (SourceId -1 and its category is General)
						bool isEraser = currentTileSourceIdInMasterSet == -1 && currentSelectedAssetCategory == AssetManager.AssetCategory.General;
						if (currentTileSourceIdInMasterSet == -1 && !isEraser)
						{
							GD.Print("TileDrawer: No valid tile or eraser selected for drawing.");
							return;
						}

						Vector2I tileMapPosition = LocalToMap(GetLocalMousePosition());
					int currentLayer = this.CurrentDrawingLayer; // Use current drawing layer

					if (currentSelectedAssetCategory == AssetManager.AssetCategory.Floor)
					{
						// FloodFill will use this.CurrentDrawingLayer internally
						TileData newFillTile = new TileData(currentTileSourceIdInMasterSet, currentTileAtlasCoordsInSource);
						FloodFill(tileMapPosition, newFillTile); // Pass newFillTile and let FloodFill use CurrentDrawingLayer
					}
					else if (currentSelectedAssetCategory == AssetManager.AssetCategory.Door ||
						currentSelectedAssetCategory == AssetManager.AssetCategory.Window)
					{
						// Check if existing tile is a wall on the CurrentDrawingLayer
						int existingTileSourceId = GetCellSourceId(currentLayer, tileMapPosition);
						Vector2I existingTileAtlasCoords = GetCellAtlasCoords(currentLayer, tileMapPosition);

						bool isWall = false;
						if (existingTileSourceId != -1 && assetManager != null)
						{
							foreach (var asset in assetManager.GetAllAssets())
							{
								if (asset.SourceId == existingTileSourceId &&
									asset.AtlasCoords == existingTileAtlasCoords &&
									asset.Category == AssetManager.AssetCategory.Wall)
								{
									isWall = true;
									break;
								}
							}
						}

						if (isWall)
						{
							// ---- START REFACTOR for Undo/Redo for Door/Window ----
							// int layer = 0; // Now use currentLayer

							// 1. Capture Previous Tile Data (the Wall)
							// existingTileSourceId and existingTileAtlasCoords are from currentLayer.
							// We need to ensure they are correctly formed into previousWallTile.
							TileData previousWallTile = (existingTileSourceId == -1) ? TileData.Empty : new TileData(existingTileSourceId, existingTileAtlasCoords);
							// if(existingTileSourceId == -1) previousWallTile = TileData.Empty; // Already handled by GetTileDataAt effectively

							// 2. Determine New Tile Data (Door/Window)
							TileData newDoorWindowTile = new TileData(currentTileSourceIdInMasterSet, currentTileAtlasCoordsInSource);

							// 3. Check for No Actual Change
							if (AreTilesIdentical(previousWallTile, newDoorWindowTile) && !previousWallTile.IsEmpty())
							{
								// GD.Print("Door/Window placement skipped: new tile is same as the wall tile.");
							}
							else if (undoRedoManager != null)
							{
								PlaceDoorWindowAction action = new PlaceDoorWindowAction(tileMapPosition, newDoorWindowTile, previousWallTile, currentLayer);
								action.Execute(this);
								undoRedoManager.RecordAction(action);
								GD.Print($"Placed {currentSelectedAssetCategory} (action recorded) on a wall at {tileMapPosition} on layer {currentLayer}");
							}
							else
							{
								GD.PrintErr("UndoRedoManager not available, drawing Door/Window directly.");
								this.SetCell(currentLayer, tileMapPosition, newDoorWindowTile.SourceId, newDoorWindowTile.AtlasCoords);
							}
							// ---- END REFACTOR ----
							// Original direct call: DrawTile(tileMapPosition, currentTileSourceIdInMasterSet, currentTileAtlasCoordsInSource);
						}
						else
						{
							GD.Print($"Cannot place {currentSelectedAssetCategory}: No wall at {tileMapPosition}");
						}
					}
					else if (currentSelectedAssetCategory == AssetManager.AssetCategory.AutoTileWall)
					{
					else if (currentSelectedAssetCategory == AssetManager.AssetCategory.AutoTileWall)
					{
						// --- Auto-Tiling Wall Placement Logic (Simplified for this step, updates only clicked tile) ---
						Vector2I targetPos = tileMapPosition;
						// int currentLayer is already defined

						string autoTileSetName = GetAutoTileSetNameFromSelectedAsset();
						if (string.IsNullOrEmpty(autoTileSetName))
						{
							GD.PrintErr("TileDrawer: AutoTileWall selected, but could not determine auto-tile set name. Cannot place tile.");
							return;
						}

						TileData originalTile = GetTileDataAt(targetPos, currentLayer);

						// For placing a new wall piece, its own future state contributes to its neighbors' calculations,
						// but for its *own* calculation, its neighbors are what matters.
						// If we are "forcing" a wall piece here, we might initially place a generic piece
						// and then update it and its neighbors.
						// For this simplified step: calculate variant based on current neighbors only.
						byte bitmask = CalculateNeighborBitmask(targetPos, currentLayer);
						TileData newConfiguredTile = GetAutoTileWallVariant(bitmask, autoTileSetName);

						if (newConfiguredTile.IsEmpty() && AutoWallSet16_Mapping.ContainsKey(bitmask)) {
							GD.PrintErr($"TileDrawer: GetAutoTileWallVariant for set '{autoTileSetName}' returned Empty for bitmask {bitmask} on layer {currentLayer}. This might indicate an issue with the base tile lookup for the set or an incomplete mapping if bitmask 0 is not a valid tile.");
							// Do not proceed if we can't get a valid tile variant, unless Empty is valid (e.g. for an "empty" result of a rule)
							// For typical wall sets, even bitmask 0 (isolated) should be a visible tile.
							if(!AutoWallSet16_Mapping.ContainsKey(0) && bitmask == 0) { /* if bitmask 0 is intentionally not mapped */ }
							else return; // Prevent placing empty/error tile
						}

						if (!AreTilesIdentical(originalTile, newConfiguredTile))
						{
							if (undoRedoManager != null)
							{
								// The UpdateAutoTilesAction expects a list. For single tile, it's a list of one.
								PlaceTileAction centerAction = new PlaceTileAction(targetPos, newConfiguredTile, originalTile, currentLayer);
								UpdateAutoTilesAction compositeAction = new UpdateAutoTilesAction(new List<PlaceTileAction> { centerAction });

								compositeAction.Execute(this);
								undoRedoManager.RecordAction(compositeAction);
								GD.Print($"TileDrawer: AutoTileWall placed at {targetPos} on layer {currentLayer} with bitmask {bitmask}, variant {newConfiguredTile.AtlasCoords}. Action recorded.");
							}
							else
							{
								GD.PrintErr("TileDrawer: UndoRedoManager not available. Performing direct AutoTileWall placement.");
								if(newConfiguredTile.IsEmpty()) this.EraseCell(currentLayer, targetPos); // Should be guarded by above check
								else this.SetCell(currentLayer, targetPos, newConfiguredTile.SourceId, newConfiguredTile.AtlasCoords);
							}
						}
						// else: No change needed, tile is already correct.
					}
					else // Not a Door/Window/Floor/AutoTileWall or other special handling, just draw (General tile) OR ERASER
					{
						TileData originalTileAtTarget = GetTileDataAt(tileMapPosition, currentLayer);

						if (currentTileSourceIdInMasterSet == -1) // ERASER is active
						{
							if (originalTileAtTarget.IsEmpty())
							{
								return;
							}

							List<PlaceTileAction> allChanges = new List<PlaceTileAction>();
							allChanges.Add(new PlaceTileAction(tileMapPosition, TileData.Empty, originalTileAtTarget, currentLayer));

							AssetData erasedAssetData = assetManager.GetAssetBySourceAndAtlas(originalTileAtTarget.SourceId, originalTileAtTarget.AtlasCoords);
							if (erasedAssetData != null && erasedAssetData.Category == AssetManager.AssetCategory.AutoTileWall)
							{
								string autoTileSetNameForEraser = GetAutoTileSetNameFromErasedAsset(erasedAssetData); // Renamed to avoid conflict
								if (!string.IsNullOrEmpty(autoTileSetNameForEraser))
								{
									foreach (Vector2I neighborPos in GetNeighborPositions(tileMapPosition))
									{
										TileData currentNeighborTile = GetTileDataAt(neighborPos, currentLayer);
										if (currentNeighborTile.IsEmpty()) continue;

										AssetData neighborAsset = assetManager.GetAssetBySourceAndAtlas(currentNeighborTile.SourceId, currentNeighborTile.AtlasCoords);
										if (neighborAsset != null &&
											neighborAsset.Category == AssetManager.AssetCategory.AutoTileWall &&
											neighborAsset.Name.StartsWith(autoTileSetNameForEraser))
										{
											byte bitmaskForNeighbor = CalculateBitmaskWithContext(neighborPos, currentLayer, autoTileSetNameForEraser, tileMapPosition, false);
											TileData newConfiguredTileForNeighbor = GetAutoTileWallVariant(bitmaskForNeighbor, autoTileSetNameForEraser);

											if (!AreTilesIdentical(currentNeighborTile, newConfiguredTileForNeighbor))
											{
												allChanges.Add(new PlaceTileAction(neighborPos, newConfiguredTileForNeighbor, currentNeighborTile, currentLayer));
											}
										}
									}
								}
							}

							if (allChanges.Count > 0 && undoRedoManager != null)
							{
								UpdateAutoTilesAction compositeAction = new UpdateAutoTilesAction(allChanges);
								compositeAction.Execute(this);
								undoRedoManager.RecordAction(compositeAction);
							} else if (undoRedoManager == null && allChanges.Count > 0) { // Ensure fallback only if changes were made
								GD.PrintErr("UndoRedoManager not available for eraser action. Erasing central tile directly.");
								this.EraseCell(currentLayer, tileMapPosition); // Fallback only erases central
							}
						}
						else // REGULAR TILE PLACEMENT (non-eraser, non-autotile, non-door/window, non-floor)
						{
							TileData newTileToPlace = new TileData(currentTileSourceIdInMasterSet, currentTileAtlasCoordsInSource);
							if (AreTilesIdentical(originalTileAtTarget, newTileToPlace))
							{
								// No action
							}
							else if (undoRedoManager != null)
							{
								PlaceTileAction action = new PlaceTileAction(tileMapPosition, newTileToPlace, originalTileAtTarget, currentLayer);
								action.Execute(this);
								undoRedoManager.RecordAction(action);
							}
							else
							{
								GD.PrintErr("UndoRedoManager not available, drawing general tile directly.");
								this.SetCell(currentLayer, tileMapPosition, newTileToPlace.SourceId, newTileToPlace.AtlasCoords);
							}
						}
						// ---- END MODIFIED ERASER AND GENERAL TILE LOGIC ----
					}
				}
			}
		}
		else if (currentMode == MainScene.DrawingMode.Room)
		{
			if (@event is InputEventMouseButton eventMouseButton)
			{
				if (eventMouseButton.ButtonIndex == MouseButton.Left)
				{
					if (eventMouseButton.Pressed)
					{
						isDraggingRoom = true;
						roomDragStartPoint = LocalToMap(GetLocalMousePosition());
						GD.Print($"Room drag started at map coords: {roomDragStartPoint}");
					}
					else // Mouse button released
					{
						if (isDraggingRoom)
						{
							isDraggingRoom = false;
							Vector2I roomDragEndPoint = LocalToMap(GetLocalMousePosition());
							GD.Print($"Room drag ended. Start: {roomDragStartPoint}, End: {roomDragEndPoint}");
							DrawRoomWalls(roomDragStartPoint, roomDragEndPoint);
						}
					}
				}
			}
			else if (@event is InputEventMouseMotion eventMouseMotion)
			{
				if (isDraggingRoom)
				{
					// Optional: Preview drawing logic can go here.
					// For now, just log mouse motion in map coordinates if dragging.
					// Vector2I currentMapCoords = LocalToMap(GetLocalMousePosition());
					// GD.Print($"Room dragging, current map coords: {currentMapCoords}");
				}
			}
		}
	}

	private void DrawRoomWalls(Vector2I startPoint, Vector2I endPoint)
	{
		// Initial Setup & Checks
		if (assetManager == null || undoRedoManager == null)
		{
			GD.PrintErr("DrawRoomWalls: AssetManager or UndoRedoManager is null. Cannot proceed.");
			return;
		}
		int currentLayer = this.CurrentDrawingLayer; // Use current drawing layer

		string autoTileSetName = GetAutoTileSetNameFromSelectedAsset();
		if (string.IsNullOrEmpty(autoTileSetName))
		{
			AssetData currentSelAsset = assetManager.GetAssetBySourceAndAtlas(currentTileSourceIdInMasterSet, currentTileAtlasCoordsInSource);
			if (currentSelAsset == null || currentSelAsset.Category != AssetManager.AssetCategory.AutoTileWall) {
				GD.Print("DrawRoomWalls: The selected asset is not an AutoTileWall type. Room not drawn with auto-tiling logic.");
				return;
			}
			GD.PrintErr($"DrawRoomWalls: Could not determine auto-tile set name for: {currentSelAsset.Name}.");
			return;
		}

		AssetData representativeTileFromSet = assetManager.GetAsset(autoTileSetName + "_tile_0_0");
		if (representativeTileFromSet == null)
		{
			GD.PrintErr($"DrawRoomWalls: Cannot find representative tile for set '{autoTileSetName}'.");
			return;
		}
		TileData baseAutoTileData = new TileData(representativeTileFromSet.SourceIdInTileSet, representativeTileFromSet.AtlasCoordsInTileSet);

		// int layer = 0; // Use currentLayer

		List<(Vector2I position, TileData newTile, TileData originalTile)> pass1PotentialChanges =
			new List<(Vector2I, TileData, TileData)>();
		Dictionary<Vector2I, TileData> previewState = new Dictionary<Vector2I, TileData>();

		int minX = Mathf.Min(startPoint.X, endPoint.X);
		int maxX = Mathf.Max(startPoint.X, endPoint.X);
		int minY = Mathf.Min(startPoint.Y, endPoint.Y);
		int maxY = Mathf.Max(startPoint.Y, endPoint.Y);

		// Top and Bottom rows
		for (int x = minX; x <= maxX; x++)
		{
			Vector2I topPos = new Vector2I(x, minY);
			TileData originalTop = GetTileDataAt(topPos, currentLayer);
			if (!AreTilesIdentical(originalTop, baseAutoTileData))
			{
				pass1PotentialChanges.Add((topPos, baseAutoTileData, originalTop));
			}
			previewState[topPos] = baseAutoTileData;

			if (minY != maxY)
			{
				Vector2I bottomPos = new Vector2I(x, maxY);
				TileData originalBottom = GetTileDataAt(bottomPos, currentLayer);
				if (!AreTilesIdentical(originalBottom, baseAutoTileData))
				{
					pass1PotentialChanges.Add((bottomPos, baseAutoTileData, originalBottom));
				}
				previewState[bottomPos] = baseAutoTileData;
			}
		}

		// Left and Right columns
		for (int y = minY + 1; y < maxY; y++)
		{
			Vector2I leftPos = new Vector2I(minX, y);
			TileData originalLeft = GetTileDataAt(leftPos, currentLayer);
			if (!AreTilesIdentical(originalLeft, baseAutoTileData))
			{
				pass1PotentialChanges.Add((leftPos, baseAutoTileData, originalLeft));
			}
			previewState[leftPos] = baseAutoTileData;

			if (minX != maxX)
			{
				Vector2I rightPos = new Vector2I(maxX, y);
				TileData originalRight = GetTileDataAt(rightPos, currentLayer);
				if (!AreTilesIdentical(originalRight, baseAutoTileData))
				{
					pass1PotentialChanges.Add((rightPos, baseAutoTileData, originalRight));
				}
				previewState[rightPos] = baseAutoTileData;
			}
		}

		// For this subtask, we stop here. Pass 1 data is collected.
		GD.Print($"DrawRoomWalls Pass 1: Collected {pass1PotentialChanges.Count} potential changes. Preview state has {previewState.Count} entries.");

		// --- PASS 2: Calculate Final Variants & Create Actions ---
		List<PlaceTileAction> finalActions = new List<PlaceTileAction>();

		foreach (Vector2I perimeterPos in previewState.Keys)
		{
			TileData originalTileOnMap = GetTileDataAt(perimeterPos, currentLayer);
			byte bitmask = CalculateBitmaskWithPreviewState(perimeterPos, currentLayer, autoTileSetName, previewState);
			TileData finalCorrectVariant = GetAutoTileWallVariant(bitmask, autoTileSetName);

			if (finalCorrectVariant.IsEmpty())
			{
				if (!originalTileOnMap.IsEmpty()) {
					GD.Print($"Warning: Auto-tile variant for {perimeterPos} on layer {currentLayer} (bitmask {bitmask}) resolved to Empty. Erasing original.");
					finalActions.Add(new PlaceTileAction(perimeterPos, finalCorrectVariant, originalTileOnMap, currentLayer));
				}
			}
			else if (!AreTilesIdentical(originalTileOnMap, finalCorrectVariant))
			{
				finalActions.Add(new PlaceTileAction(perimeterPos, finalCorrectVariant, originalTileOnMap, currentLayer));
			}
		}

		GD.Print($"DrawRoomWalls Pass 2 (Layer {currentLayer}): Created {finalActions.Count} final actions.");

		if (finalActions.Count > 0)
		{
			UpdateAutoTilesAction overallRoomAction = new UpdateAutoTilesAction(finalActions);
			if (undoRedoManager != null)
			{
				overallRoomAction.Execute(this);
				undoRedoManager.RecordAction(overallRoomAction);
				GD.Print($"DrawRoomWalls: Executed and recorded auto-tile room action on layer {currentLayer} with {finalActions.Count} changes.");
			}
			else
			{
				GD.PrintErr("DrawRoomWalls: UndoRedoManager not available. Executing changes directly.");
				foreach(var action in finalActions) { action.Execute(this); }
			}
		}
		else
		{
			GD.Print("DrawRoomWalls: No effective changes to apply for the room construction on layer {currentLayer}.");
		}
	}

	private void FloodFill(Vector2I startPosition, TileData newFillTile) // Updated signature
	{
		int currentLayer = this.CurrentDrawingLayer; // Use current drawing layer
		// TileData newFillTile = new TileData(targetMasterSourceId, targetAtlasCoordsInSource); // Passed in directly

		int originalStartNodeSourceId = GetCellSourceId(currentLayer, startPosition);
		Vector2I originalStartNodeAtlasCoords = GetCellAtlasCoords(currentLayer, startPosition);
		TileData originalStartNodeTile = (originalStartNodeSourceId == -1) ? TileData.Empty : new TileData(originalStartNodeSourceId, originalStartNodeAtlasCoords);


		if (AreTilesIdentical(originalStartNodeTile, newFillTile)) // Use AreTilesIdentical
		{
			GD.Print("FloodFill: Start node is already the target tile. Nothing to do.");
			return;
		}

		// Check if the start node is a wall.
		if (assetManager != null && !originalStartNodeTile.IsEmpty())
		{
			AssetData startNodeAsset = assetManager.GetAssetBySourceAndAtlas(originalStartNodeTile.SourceId, originalStartNodeTile.AtlasCoords);
			if (startNodeAsset != null && startNodeAsset.Category == AssetManager.AssetCategory.Wall)
			{
				GD.Print($"FloodFill: Cannot start fill on a Wall tile on layer {currentLayer}.");
				return;
			}
		}

		List<PlaceTileAction> fillElementaryActions = new List<PlaceTileAction>();
		Queue<Vector2I> queue = new Queue<Vector2I>();
		HashSet<Vector2I> visited = new HashSet<Vector2I>();

		queue.Enqueue(startPosition);
		visited.Add(startPosition);

		// int fillCount = 0; // For debugging or limiting, now managed by list count
		int maxFillOperations = 10000; // Safety break for very large areas

		while (queue.Count > 0)
		{
			if (fillElementaryActions.Count >= maxFillOperations) {
				GD.PrintErr("FloodFill: Reached max fill operations. Stopping to prevent freeze.");
				break;
			}

			Vector2I currentNode = queue.Dequeue();

			int prevNodeSourceId = GetCellSourceId(currentLayer, currentNode);
			Vector2I prevNodeAtlasCoords = GetCellAtlasCoords(currentLayer, currentNode);
			TileData previousNodeTile = (prevNodeSourceId == -1) ? TileData.Empty : new TileData(prevNodeSourceId, prevNodeAtlasCoords);

			fillElementaryActions.Add(new PlaceTileAction(currentNode, newFillTile, previousNodeTile, currentLayer));

			Vector2I[] neighbors = new Vector2I[]
			{
				new Vector2I(currentNode.X + 1, currentNode.Y), // Right
				new Vector2I(currentNode.X - 1, currentNode.Y), // Left
				new Vector2I(currentNode.X, currentNode.Y + 1), // Down
				new Vector2I(currentNode.X, currentNode.Y - 1)  // Up
			};

			foreach (var neighborPos in neighbors)
			{
				// TileMap handles bounds implicitly; GetCellSourceId returns -1 for out-of-bounds.
				// However, for extremely large maps, explicit bounds might be desired earlier.

				if (!visited.Contains(neighborPos))
				{
					int neighborSourceId = GetCellSourceId(currentLayer, neighborPos);
					Vector2I neighborAtlasCoords = GetCellAtlasCoords(currentLayer, neighborPos);

					// Check against originalStartNodeTile for matching type to fill
					if (neighborSourceId == originalStartNodeTile.SourceId && neighborAtlasCoords == originalStartNodeTile.AtlasCoords)
					{
						visited.Add(neighborPos);
						queue.Enqueue(neighborPos);
					}
					else if (originalStartNodeTile.IsEmpty() && neighborSourceId == -1)
					{
						visited.Add(neighborPos);
						queue.Enqueue(neighborPos);
					}
				}
			}
		}

		if (fillElementaryActions.Count > 0)
		{
			if (undoRedoManager != null)
			{
				FillAction overallAction = new FillAction(fillElementaryActions);
				overallAction.Execute(this);
				undoRedoManager.RecordAction(overallAction);
				GD.Print($"FillAction recorded on layer {currentLayer} with {fillElementaryActions.Count} changes.");
			}
			else
			{
				GD.PrintErr($"FloodFill: UndoRedoManager not available. Performing direct fill on layer {currentLayer}.");
				foreach(var action in fillElementaryActions) { action.Execute(this); }
			}
		}
		else
		{
			GD.Print("FloodFill: No tiles needed to be changed.");
		}
	}

	// --- Auto-Tiling Helper Methods ---

	private static readonly Dictionary<byte, Vector2I> AutoWallSet16_Mapping = new Dictionary<byte, Vector2I>
	{
		// Bitmask: NESW (Bit0:N, Bit1:E, Bit2:S, Bit3:W)
		// AtlasCoords are (column, row) for a 4x4 sheet
		// Assuming N=1, E=2, S=4, W=8
		{0,  new Vector2I(0,0)}, // ---- (Isolated)
		{1,  new Vector2I(1,0)}, // N--- (End S) -> Atlas (1,0) assuming this is "wall above"
		{2,  new Vector2I(2,0)}, // -E-- (End W) -> Atlas (2,0) assuming "wall right"
		{3,  new Vector2I(3,0)}, // NE-- (Corner SW)
		{4,  new Vector2I(0,1)}, // --S- (End N)
		{5,  new Vector2I(1,1)}, // N-S- (Vertical)
		{6,  new Vector2I(2,1)}, // -ES- (Corner NW)
		{7,  new Vector2I(3,1)}, // NES- (T-Junction, open W)
		{8,  new Vector2I(0,2)}, // ---W (End E)
		{9,  new Vector2I(1,2)}, // N--W (Corner SE)
		{10, new Vector2I(2,2)}, // -E-W (Horizontal)
		{11, new Vector2I(3,2)}, // NE-W (T-Junction, open S)
		{12, new Vector2I(0,3)}, // --SW (Corner NE)
		{13, new Vector2I(1,3)}, // N-SW (T-Junction, open E)
		{14, new Vector2I(2,3)}, // -ESW (T-Junction, open N)
		{15, new Vector2I(3,3)}  // NESW (Cross)
	};

	private const byte BIT_N = 1;  // North
	private const byte BIT_E = 2;  // East
	private const byte BIT_S = 4;  // South
	private const byte BIT_W = 8;  // West

	private bool IsConsideredWallForAutoTiling(Vector2I position, int layer)
	{
		if (assetManager == null)
		{
			GD.PrintErr("IsConsideredWallForAutoTiling: AssetManager not available.");
			return false; // Cannot determine without asset manager
		}

		int sourceId = GetCellSourceId(layer, position);
		if (sourceId == -1) return false; // Empty cell is not a wall

		Vector2I atlasCoords = GetCellAtlasCoords(layer, position);

		// Get the AssetData for the tile at the given position using its source and atlas coords from the MasterTileSet
		AssetData tileAssetData = assetManager.GetAssetBySourceAndAtlas(sourceId, atlasCoords);

		if (tileAssetData != null && tileAssetData.Category == AssetManager.AssetCategory.AutoTileWall)
		{
			// Further refinement could be to check if tileAssetData.Name starts with a specific autotile set name
			// if multiple autotile wall sets should not connect. For now, any AutoTileWall connects.
			return true;
		}
		return false;
	}

	private byte CalculateNeighborBitmask(Vector2I position, int layer)
	{
		byte bitmask = 0;

		// North
		if (IsConsideredWallForAutoTiling(position + Vector2I.Up, layer)) // position.Y - 1
		{
			bitmask |= BIT_N;
		}
		// East
		if (IsConsideredWallForAutoTiling(position + Vector2I.Right, layer)) // position.X + 1
		{
			bitmask |= BIT_E;
		}
		// South
		if (IsConsideredWallForAutoTiling(position + Vector2I.Down, layer)) // position.Y + 1
		{
			bitmask |= BIT_S;
		}
		// West
		if (IsConsideredWallForAutoTiling(position + Vector2I.Left, layer)) // position.X - 1
		{
			bitmask |= BIT_W;
		}
		// GD.Print($"Bitmask for {position}: {System.Convert.ToString(bitmask, 2).PadLeft(4, '0')}");
		return bitmask;
	}

	private TileData GetAutoTileWallVariant(byte bitmask, string autoTileSetName)
	{
		if (assetManager == null)
		{
			GD.PrintErr("GetAutoTileWallVariant: AssetManager not available.");
			return TileData.Empty;
		}

		Vector2I atlasCoordsForVariant;
		bool foundMappingInSelectedSet = false;

		// Select the correct mapping based on the tile set name
		if (autoTileSetName == "AutoWallSet16")
		{
			if (AutoWallSet16_Mapping.TryGetValue(bitmask, out atlasCoordsForVariant))
			{
				foundMappingInSelectedSet = true;
			}
			else
			{
				// Fallback to a default tile (e.g., isolated) if bitmask not in this specific mapping
				atlasCoordsForVariant = AutoWallSet16_Mapping[0]; // Assuming 0 is always a valid key (isolated tile)
				GD.PrintErr($"Bitmask {bitmask} not found in {autoTileSetName} mapping. Using default atlas coord {atlasCoordsForVariant}.");
			}
		}
		// Example for future expansion:
		// else if (autoTileSetName == "AnotherAutoTileSet")
		// {
		//     if (AnotherAutoTileSet_Mapping.TryGetValue(bitmask, out atlasCoordsForVariant))
		//     {
		//         foundMappingInSelectedSet = true;
		//     }
		//     // ... else handle fallback for AnotherAutoTileSet
		// }
		else
		{
			GD.PrintErr($"Auto-tile set name '{autoTileSetName}' not recognized or its mapping is not implemented.");
			return TileData.Empty;
		}

		// Now, find the AssetData for this specific tile variant to get its SourceIdInMasterSet.
		// All tiles from the same imported auto-tile spritesheet (e.g., "AutoWallSet16_tile_0_0", "..._0_1")
		// share the same SourceIdInMasterSet and the same base name part ("AutoWallSet16").
		// We can retrieve any tile from this set to find the SourceIdInMasterSet.
		// A common way is to use the asset representing the (0,0) tile of this specific auto-tile set.

		// Construct the name of one of the tiles from the set, e.g., the one at atlas (0,0) for that set
		string representativeTileName = $"{autoTileSetName}_tile_0_0";
		AssetData baseTileOfSet = assetManager.GetAsset(representativeTileName);

		if (baseTileOfSet == null)
		{
			GD.PrintErr($"Could not find representative tile '{representativeTileName}' for auto-tile set '{autoTileSetName}' in AssetManager.");
			// This could happen if AssetManager hasn't loaded it, or naming convention changed.
			// As a more robust fallback, one could iterate all assets and find the first one
			// that belongs to the category AutoTileWall and whose name starts with autoTileSetName,
			// but GetAssetByName is more direct if the naming convention is reliable.
			return TileData.Empty;
		}

		if (baseTileOfSet.Category != AssetManager.AssetCategory.AutoTileWall)
		{
			GD.PrintErr($"Representative tile '{representativeTileName}' for set '{autoTileSetName}' is not categorized as AutoTileWall.");
			return TileData.Empty;
		}

		if (baseTileOfSet.SourceIdInTileSet == -1)
		{
			GD.PrintErr($"Representative tile '{representativeTileName}' for set '{autoTileSetName}' has an invalid SourceIdInTileSet (-1).");
			return TileData.Empty;
		}

		// The SourceIdInTileSet from any tile of this auto-tile set is the one we need for the MasterTileSet.
		// The atlasCoordsForVariant is specific to this auto-tile set's layout.
		return new TileData(baseTileOfSet.SourceIdInTileSet, atlasCoordsForVariant);
	}

	private TileData GetTileDataAt(Vector2I position, int layer)
	{
		int sourceId = GetCellSourceId(layer, position);
		if (sourceId == -1)
		{
			// Check atlas coords too, as per TileData.Empty definition
			Vector2I atlasCoordsCheck = GetCellAtlasCoords(layer, position);
			if (atlasCoordsCheck == new Vector2I(-1,-1)) return TileData.Empty;
			// If sourceId is -1 but atlas coords are not (-1,-1) (e.g. (0,0) by default for empty cells in some contexts),
			// still treat as empty for consistency with TileData.Empty.
			return TileData.Empty;
		}
		return new TileData(sourceId, GetCellAtlasCoords(layer, position));
	}

	private bool AreTilesIdentical(TileData tileA, TileData tileB)
	{
		// Also considers if both are functionally "empty" even if atlas coords differ for one due to engine defaults
		if (tileA.IsEmpty() && tileB.IsEmpty()) return true;
		return tileA.SourceId == tileB.SourceId && tileA.AtlasCoords == tileB.AtlasCoords;
	}

	private string GetAutoTileSetNameFromSelectedAsset()
	{
		if (assetManager == null || currentTileSourceIdInMasterSet == -1) return null;

		AssetData selectedAsset = assetManager.GetAssetBySourceAndAtlas(currentTileSourceIdInMasterSet, currentTileAtlasCoordsInSource);
		if (selectedAsset != null && selectedAsset.Category == AssetManager.AssetCategory.AutoTileWall)
		{
			if (selectedAsset.Name.Contains("_tile_"))
			{
				return selectedAsset.Name.Split("_tile_")[0];
			}
			else
			{
				// This might be a single-tile auto-tile asset that doesn't follow the _tile_X_Y convention
				// For now, assume the convention. If not, it might just be the asset's direct name.
				// However, our AssetManager creates them with _tile_X_Y.
				// This case should ideally not be hit if assets are named as expected.
				GD.PrintErr($"Selected AutoTileWall asset '{selectedAsset.Name}' does not follow expected '_tile_X_Y' naming convention for deriving set name.");
				return null;
			}
		}
		return null;
	}

	private string GetAutoTileSetNameFromErasedAsset(AssetData erasedAsset)
	{
		if (erasedAsset == null || erasedAsset.Category != AssetManager.AssetCategory.AutoTileWall ||
			string.IsNullOrEmpty(erasedAsset.Name) || !erasedAsset.Name.Contains("_tile_"))
		{
			return null;
		}
		return erasedAsset.Name.Split("_tile_")[0];
	}

	private List<Vector2I> GetNeighborPositions(Vector2I pos)
	{
		return new List<Vector2I>
		{
			pos + Vector2I.Up,
			pos + Vector2I.Down,
			pos + Vector2I.Left,
			pos + Vector2I.Right
		};
	}

	private byte CalculateBitmaskWithContext(Vector2I positionToCalcFor, int layer, string autoTileSetName, Vector2I contextPos, bool contextPosIsNowConsideredWall)
	{
		byte bitmask = 0;
		Vector2I[] neighborOffsets = { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left }; // N, E, S, W
		byte[]  bitValues         = { BIT_N, BIT_E, BIT_S, BIT_W };

		for (int i = 0; i < neighborOffsets.Length; i++)
		{
			Vector2I actualNeighborPos = positionToCalcFor + neighborOffsets[i];
			bool isConsideredWall;

			if (actualNeighborPos == contextPos)
			{
				isConsideredWall = contextPosIsNowConsideredWall;
			}
			else
			{
				// Standard check for other neighbors
				TileData tileAtActualNeighbor = GetTileDataAt(actualNeighborPos, layer);
				if (tileAtActualNeighbor.IsEmpty())
				{
					isConsideredWall = false;
				}
				else
				{
					if (assetManager == null) { // Should not happen if initialized
						GD.PrintErr("CalculateBitmaskWithContext: AssetManager is null!");
						isConsideredWall = false; // Or throw error
					} else {
						AssetData asset = assetManager.GetAssetBySourceAndAtlas(tileAtActualNeighbor.SourceId, tileAtActualNeighbor.AtlasCoords);
						isConsideredWall = asset != null &&
										   asset.Category == AssetManager.AssetCategory.AutoTileWall &&
										   (string.IsNullOrEmpty(autoTileSetName) || asset.Name.StartsWith(autoTileSetName)); // Check it's part of the same set if autoTileSetName is provided
					}
				}
			}

			if (isConsideredWall)
			{
				bitmask |= bitValues[i];
			}
		}
		// GD.Print($"Bitmask for {positionToCalcFor} with context {contextPos} (isWall={contextPosIsNowConsideredWall}): {System.Convert.ToString(bitmask, 2).PadLeft(4, '0')}");
		return bitmask;
	}

	private byte CalculateBitmaskWithPreviewState(
		Vector2I positionToCalculateFor,
		int layer,
		string autoTileSetName, // Should be a non-empty, valid set name for this function's purpose
		Dictionary<Vector2I, TileData> previewState)
	{
		byte bitmask = 0;
		Vector2I[] neighborOffsets = { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left }; // N, E, S, W
		byte[] bitValues = { BIT_N, BIT_E, BIT_S, BIT_W };

		if (assetManager == null)
		{
			GD.PrintErr("CalculateBitmaskWithPreviewState: AssetManager not available.");
			return 0;
		}
		if (string.IsNullOrEmpty(autoTileSetName))
		{
			GD.PrintErr("CalculateBitmaskWithPreviewState: autoTileSetName cannot be null or empty.");
			return 0;
		}

		for (int i = 0; i < neighborOffsets.Length; i++)
		{
			Vector2I neighborPos = positionToCalculateFor + neighborOffsets[i];
			bool isWallNeighbor = false;

			TileData neighborTileData;
			if (previewState.TryGetValue(neighborPos, out neighborTileData))
			{
				// Neighbor is part of the preview state
				if (!neighborTileData.IsEmpty())
				{
					AssetData neighborAsset = assetManager.GetAssetBySourceAndAtlas(neighborTileData.SourceId, neighborTileData.AtlasCoords);
					if (neighborAsset != null &&
						neighborAsset.Category == AssetManager.AssetCategory.AutoTileWall &&
						neighborAsset.Name.StartsWith(autoTileSetName)) // Check it belongs to the same set
					{
						isWallNeighbor = true;
					}
				}
				// If neighborTileData was Empty in preview, isWallNeighbor remains false.
			}
			else
			{
				// Neighbor is not in the preview state, so check the actual current TileMap state
				TileData mapTileData = GetTileDataAt(neighborPos, layer); // Uses existing helper
				if (!mapTileData.IsEmpty())
				{
					AssetData mapAsset = assetManager.GetAssetBySourceAndAtlas(mapTileData.SourceId, mapTileData.AtlasCoords);
					if (mapAsset != null &&
						mapAsset.Category == AssetManager.AssetCategory.AutoTileWall &&
						mapAsset.Name.StartsWith(autoTileSetName)) // Check if it belongs to the same set
					{
						isWallNeighbor = true;
					}
				}
			}

			if (isWallNeighbor)
			{
				bitmask |= bitValues[i];
			}
		}
		// GD.Print($"Bitmask for {positionToCalculateFor} with preview (set: {autoTileSetName}): {System.Convert.ToString(bitmask, 2).PadLeft(4, '0')}");
		return bitmask;
	}

	public void SetCurrentDrawingLayer(int layer)
	{
		// Basic validation, assuming layer indices must be valid within TileMap capabilities
		if (layer < 0) // Godot layers are non-negative. Could also check against GetLayersCount() if needed.
		{
			GD.PrintErr($"TileDrawer: Invalid layer index {layer} specified. Cannot set current drawing layer.");
			return;
		}
		CurrentDrawingLayer = layer;
		GD.Print($"TileDrawer: Active drawing layer set to {layer}");
	}
}
