using Godot;
using System.Collections.Generic;

public class AssetData
{
	public string Name { get; set; } // User-defined name for the asset or individual tile
	public AssetCategory Category { get; set; }

	// For TileSet-based assets (spritesheets)
	public string TileSetResourcePath { get; set; } // Path to .tres file
	public int SourceIdInTileSet { get; set; }     // The Source ID within the .tres file (usually 0)
	public Vector2I AtlasCoordsInTileSet { get; set; } // Coordinates of the tile within the atlas source

	// For single image assets (not part of a TileSet)
	public string OriginalImageFilePath { get; set; } // e.g., res://imported_assets/images/my_object.png

	public Texture2D PreviewTexture { get; set; } // Optional: loaded texture for UI previews (can be heavy)

	// Helper to distinguish single image assets from tileset tiles
	public bool IsSingleImage() => string.IsNullOrEmpty(TileSetResourcePath);
}

public enum AssetCategory
{
	General,
	Wall,
	Door,
	Window,
	Floor,
	EnvironmentObject,
	AutoTileWall,
	PlaceableObject // New category
}

public partial class AssetManager : Node
{
	private List<AssetData> assets = new List<AssetData>();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Autoload singletons are typically not added to the scene tree manually,
	private GlobalSettings globalSettings;
	private const string ProjectMasterTileSetPath = "res://ProjectMasterTileSet.tres"; // Project specific
	private TileSet projectMasterTileSet;


	public override void _Ready()
	{
		globalSettings = GetNode<GlobalSettings>("/root/GlobalSettings");
		if (globalSettings == null)
		{
			GD.PrintErr("AssetManager: GlobalSettings singleton not found! Cannot function correctly.");
			return;
		}
		LoadOrCreateProjectMasterTileSet();
		RefreshAssets();
	}

	private void LoadOrCreateProjectMasterTileSet()
	{
		if (FileAccess.FileExists(ProjectMasterTileSetPath))
		{
			projectMasterTileSet = ResourceLoader.Load<TileSet>(ProjectMasterTileSetPath);
			if (projectMasterTileSet == null)
			{
				GD.PrintErr($"AssetManager: Failed to load ProjectMasterTileSet at {ProjectMasterTileSetPath}. Creating a new one.");
				projectMasterTileSet = new TileSet();
				// No need to save here, will be saved after RefreshAssets if changes are made or if it's new.
			}
		}
		else
		{
			GD.Print($"AssetManager: ProjectMasterTileSet not found at {ProjectMasterTileSetPath}. Creating a new one.");
			projectMasterTileSet = new TileSet();
		}
		// Ensure basic configuration
		projectMasterTileSet.TileShape = TileSet.TileShapeEnum.Square;
		projectMasterTileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;
		// Don't save here yet; RefreshAssets will populate and then save.
	}

	private void SaveProjectMasterTileSet()
	{
		if (projectMasterTileSet != null)
		{
			Error err = ResourceSaver.Save(projectMasterTileSet, ProjectMasterTileSetPath);
			if (err != Error.Ok) GD.PrintErr($"AssetManager: Failed to save ProjectMasterTileSet: {err}");
			else GD.Print($"AssetManager: ProjectMasterTileSet saved to {ProjectMasterTileSetPath}");
		}
	}


	public void RefreshAssets()
	{
		if (globalSettings == null) {
			GD.PrintErr("AssetManager: GlobalSettings not available for RefreshAssets.");
			return;
		}
		if (projectMasterTileSet == null)
		{
			GD.PrintErr("AssetManager: ProjectMasterTileSet is null. Attempting to load/create.");
			LoadOrCreateProjectMasterTileSet(); // Try to recover
			if (projectMasterTileSet == null) {
				GD.PrintErr("AssetManager: ProjectMasterTileSet recovery failed. Cannot refresh assets.");
				return;
			}
		}

		assets.Clear();
		projectMasterTileSet.ClearSources();
		GD.Print("AssetManager: Refreshing assets and rebuilding ProjectMasterTileSet sources...");

		string globalAssetRootPath = globalSettings.GetGlobalAssetPath();
		if (string.IsNullOrEmpty(globalAssetRootPath))
		{
			GD.PrintErr("AssetManager: Global asset root path not set. Cannot load assets.");
			SaveProjectMasterTileSet(); // Save empty or current state of master tileset
			return;
		}

		string globalMetadataFolderPath = globalAssetRootPath.PlusFile("metadata");
		using var dir = DirAccess.Open(globalMetadataFolderPath);

		if (dir == null)
		{
			GD.PrintErr($"AssetManager: Could not open global metadata directory: {globalMetadataFolderPath}. Error: {DirAccess.GetOpenError()}");
			// GlobalSettings should have created this path's root. If metadata/ is missing, it means no assets yet.
			SaveProjectMasterTileSet();
			return;
		}

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		int nextSourceIdInProjectMaster = 0;

		while (fileName != "")
		{
			if (dir.CurrentIsDir() || !fileName.EndsWith(".json"))
			{
				fileName = dir.GetNext();
				continue;
			}

			string fullMetadataPath = globalMetadataFolderPath.PlusFile(fileName);
			string jsonContent = FileAccess.GetFileAsString(metadataFilePath);

			if (string.IsNullOrEmpty(jsonContent))
			{
				GD.PrintErr($"AssetManager: Metadata file is empty: {metadataFilePath}");
				fileName = dir.GetNext();
				continue;
			}

			Variant parsedJson = Json.ParseString(jsonContent);
			if (parsedJson.VariantType == Variant.Type.Nil) { // Check if parsing failed (Json.ParseString returns Nil on error)
				GD.PrintErr($"AssetManager: Failed to parse JSON from: {metadataFilePath}. Content: {jsonContent}");
				fileName = dir.GetNext();
				continue;
			}

			// Godot's JSON parser returns a Dictionary. We need to manually map to AssetMetadata.
			// Or, if AssetMetadata was a Godot.GodotObject, we could use helpers, but it's a plain C# class.
			// For simplicity, directly accessing dictionary fields. A more robust solution would involve
			// checking for key existence and type.
			var metadataDict = parsedJson.AsGodotDictionary<string, Variant>();

			// Create an AssetMetadata instance from the dictionary
			// This assumes AssetMetadata.cs is now globally accessible (moved out of AssetLibraryPanel.cs)
			AssetMetadata parsedMetadata = new AssetMetadata();
			parsedMetadata.AssetName = metadataDict.GetValueOrDefault("AssetName", "").ToString();
			parsedMetadata.ImageFileName = metadataDict.GetValueOrDefault("ImageFileName", "").ToString();
			parsedMetadata.Category = metadataDict.GetValueOrDefault("Category", "General").ToString();
			parsedMetadata.IsSpritesheet = (bool)metadataDict.GetValueOrDefault("IsSpritesheet", false);
			parsedMetadata.TileSetResourcePath = metadataDict.GetValueOrDefault("TileSetResourcePath", "").ToString();
			// Spritesheet specific fields, only relevant if IsSpritesheet is true
			if (parsedMetadata.IsSpritesheet) {
				parsedMetadata.TileWidth = (int)metadataDict.GetValueOrDefault("TileWidth", 16);
				parsedMetadata.TileHeight = (int)metadataDict.GetValueOrDefault("TileHeight", 16);
				parsedMetadata.SeparationX = (int)metadataDict.GetValueOrDefault("SeparationX", 0);
				parsedMetadata.SeparationY = (int)metadataDict.GetValueOrDefault("SeparationY", 0);
				parsedMetadata.MarginX = (int)metadataDict.GetValueOrDefault("MarginX", 0);
				parsedMetadata.MarginY = (int)metadataDict.GetValueOrDefault("MarginY", 0);
			}


			string jsonContent = FileAccess.GetFileAsString(fullMetadataPath);
			if (string.IsNullOrEmpty(jsonContent)) { GD.PrintErr($"AssetManager: Metadata file empty: {fullMetadataPath}"); fileName = dir.GetNext(); continue; }

			Variant parsedJson = Json.ParseString(jsonContent);
			if (parsedJson.VariantType == Variant.Type.Nil) { GD.PrintErr($"AssetManager: Failed to parse JSON: {fullMetadataPath}"); fileName = dir.GetNext(); continue; }

			var metadataDict = parsedJson.AsGodotDictionary<string, Variant>();
			AssetMetadata parsedMetadata = new AssetMetadata(); // Assuming AssetMetadata.cs is globally accessible
			parsedMetadata.AssetName = metadataDict.GetValueOrDefault("AssetName", "").ToString();
			parsedMetadata.ImageFileName = metadataDict.GetValueOrDefault("ImageFileName", "").ToString();
			parsedMetadata.Category = metadataDict.GetValueOrDefault("Category", "General").ToString();
			parsedMetadata.IsSpritesheet = (bool)metadataDict.GetValueOrDefault("IsSpritesheet", false);
			parsedMetadata.TileSetResourcePath = metadataDict.GetValueOrDefault("TileSetResourcePath", "").ToString(); // Relative to global (e.g. "tilesets/MySet.tres")
			if (parsedMetadata.IsSpritesheet) {
				parsedMetadata.TileWidth = (int)metadataDict.GetValueOrDefault("TileWidth", 16);
				parsedMetadata.TileHeight = (int)metadataDict.GetValueOrDefault("TileHeight", 16);
				// ... (rest of spritesheet params)
			}

			string globalImageRelativePath = $"images/{parsedMetadata.ImageFileName}";
			string fullGlobalImagePath = globalAssetRootPath.PlusFile(globalImageRelativePath);
			Texture2D previewTexture = ResourceLoader.Load<Texture2D>(fullGlobalImagePath);
			if (previewTexture == null) GD.PrintErr($"AssetManager: Failed to load preview texture: {fullGlobalImagePath} for {parsedMetadata.AssetName}");


			if (parsedMetadata.IsSpritesheet)
			{
				string globalTileSetRelativePath = parsedMetadata.TileSetResourcePath; // e.g., "tilesets/MySet.tres"
				string fullGlobalTileSetPath = globalAssetRootPath.PlusFile(globalTileSetRelativePath);

				if (string.IsNullOrEmpty(globalTileSetRelativePath) || !FileAccess.FileExists(fullGlobalTileSetPath)) {
					GD.PrintErr($"AssetManager: Imported TileSet file not found for {parsedMetadata.AssetName} at {fullGlobalTileSetPath}");
					fileName = dir.GetNext(); continue;
				}
				TileSet importedTileSet = ResourceLoader.Load<TileSet>(fullGlobalTileSetPath);
				if (importedTileSet == null || importedTileSet.GetSourceCount() == 0) {
					GD.PrintErr($"AssetManager: Failed to load TileSet or no sources in TileSet for {parsedMetadata.AssetName} from {fullGlobalTileSetPath}");
					fileName = dir.GetNext(); continue;
				}

				TileSetAtlasSource originalAtlasSource = importedTileSet.GetSource(0) as TileSetAtlasSource;
				if (originalAtlasSource == null) {
					GD.PrintErr($"AssetManager: No TileSetAtlasSource found in imported TileSet {fullGlobalTileSetPath}");
					fileName = dir.GetNext(); continue;
				}
				// Ensure the texture path in the source is correct for the project master tileset.
				// The originalAtlasSource.Texture should point to the globally stored image.
				// If AddSource makes a deep copy or if paths are already correct (user:// or res://), this is fine.
				// Resource paths within imported .tres files should ideally be relative to that .tres or absolute (user://).
				// For TileSetAtlasSource, its texture is a direct Texture2D resource, not a path that needs resolving here usually.

				int newSourceId = projectMasterTileSet.AddSource(originalAtlasSource);
				if (newSourceId == -1) {
					GD.PrintErr($"AssetManager: Failed to add source from {fullGlobalTileSetPath} to ProjectMasterTileSet.");
					fileName = dir.GetNext(); continue;
				}

				int tileCount = originalAtlasSource.GetTilesCount();
				for (int i = 0; i < tileCount; i++)
				{
					Vector2I atlasCoords = originalAtlasSource.GetTileId(i);
					assets.Add(new AssetData {
						Name = $"{parsedMetadata.AssetName}_tile_{atlasCoords.X}_{atlasCoords.Y}",
						Category = (AssetCategory)Enum.Parse(typeof(AssetCategory), parsedMetadata.Category, true),
						TileSetResourcePath = globalTileSetRelativePath, // Store relative global path for reference
						SourceIdInTileSet = newSourceId,       // ID within ProjectMasterTileSet
						AtlasCoordsInTileSet = atlasCoords,
						OriginalImageFilePath = globalImageRelativePath, // Store relative global path
						PreviewTexture = previewTexture
					});
				}
				nextSourceIdInProjectMaster++;
			}
			else // Single image asset (either for TileMap or as PlaceableObject)
			{
				AssetCategory category = AssetCategory.General; // Default
				try {
					category = (AssetCategory)System.Enum.Parse(typeof(AssetCategory), parsedMetadata.Category, true);
				} catch (System.ArgumentException e) {
					GD.PrintErr($"AssetManager: Invalid category string '{parsedMetadata.Category}' for asset '{parsedMetadata.AssetName}'. Defaulting to General. Error: {e.Message}");
				}

				if (previewTexture == null) {
					GD.PrintErr($"AssetManager: Skipping single image {parsedMetadata.AssetName} (Category: {category}), texture failed to load from {fullGlobalImagePath}.");
					fileName = dir.GetNext(); continue;
				}

				if (category == AssetCategory.PlaceableObject)
				{
					// For PlaceableObjects, don't add to ProjectMasterTileSet.
					// SourceIdInTileSet remains -1.
					assets.Add(new AssetData
					{
						Name = parsedMetadata.AssetName,
						Category = category,
						TileSetResourcePath = null,
						SourceIdInTileSet = -1,
						AtlasCoordsInTileSet = new Vector2I(-1, -1),
						OriginalImageFilePath = globalImageRelativePath, // Relative to global_assets_root
						PreviewTexture = previewTexture
					});
					GD.Print($"AssetManager: Loaded PlaceableObject '{parsedMetadata.AssetName}'.");
				}
				else // Single images to be used as regular tiles in the TileMap
				{
					TileSetAtlasSource singleImageSource = new TileSetAtlasSource();
					singleImageSource.Texture = previewTexture;
					singleImageSource.TextureRegionSize = previewTexture.GetSize(); // Whole image is one tile
					singleImageSource.CreateTile(Vector2I.Zero); // Create the single tile at (0,0) in this new source

					int newSourceIdInProjectMaster = projectMasterTileSet.AddSource(singleImageSource);
					if (newSourceIdInProjectMaster == -1) {
						GD.PrintErr($"AssetManager: Failed to add single image source for '{parsedMetadata.AssetName}' to ProjectMasterTileSet.");
						fileName = dir.GetNext(); continue;
					}

					assets.Add(new AssetData {
						Name = parsedMetadata.AssetName,
						Category = category, // Could be General, Floor, etc.
						TileSetResourcePath = null, // Not from an original .tres, but part of ProjectMasterTileSet
						SourceIdInTileSet = newSourceIdInProjectMaster,
						AtlasCoordsInTileSet = Vector2I.Zero,
						OriginalImageFilePath = globalImageRelativePath,
						PreviewTexture = previewTexture
					});
					nextSourceIdInProjectMaster++;
				}
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();

		// Add the special Eraser AssetData (code from previous step)
		Texture2D eraserIcon = null;
		string eraserIconPath = "res://assets/icons/eraser_icon.png"; // Standardized path
		if (FileAccess.FileExists(eraserIconPath)) // Use FileAccess.FileExists for checking actual files
		{
			eraserIcon = ResourceLoader.Load<Texture2D>(eraserIconPath);
			if (eraserIcon == null)
			{
				GD.PrintErr($"Eraser icon failed to load from {eraserIconPath}, though file exists. Will create placeholder.");
				// Fall through to placeholder creation
			}
		}
		else
		{
			GD.Print($"Eraser icon not found at {eraserIconPath}. Creating placeholder icon.");
			// Fall through to placeholder creation
		}

		if (eraserIcon == null) // If still null after trying to load or if file didn't exist
		{
			Image tempImage = Image.Create(16, 16, false, Image.Format.Rgba8);
			tempImage.Fill(new Color(0.9f, 0.9f, 0.9f, 0.8f)); // Light gray, slightly transparent
			// Draw a simple red 'X' or diagonal line
			for(int i = 0; i < 16; i++) {
				if (i > 3 && i < 12) { // Avoid corners for a cleaner X
					tempImage.SetPixel(i, i, Colors.Red);
					tempImage.SetPixel(i, 15 - i, Colors.Red);
				}
			}
			eraserIcon = ImageTexture.CreateFromImage(tempImage);
		}

		AssetData eraserAsset = new AssetData
		{
			Name = "Eraser", // User-friendly name for UI
			Category = AssetCategory.General, // Or a new AssetCategory.Tool
			TileSetResourcePath = null,
			SourceIdInTileSet = -1, // Key identifier for eraser logic in TileDrawer
			AtlasCoordsInTileSet = new Vector2I(-1, -1),
			OriginalImageFilePath = null, // Not a file-based asset in the usual sense
			PreviewTexture = eraserIcon
		};
		assets.Add(eraserAsset);
		GD.Print($"Added virtual Eraser asset. Total assets: {assets.Count}");

		SaveProjectMasterTileSet(); // Save the populated or modified ProjectMasterTileSet
		GD.Print($"AssetManager: Refresh complete. Loaded {assets.Count} assets. ProjectMasterTileSet has {projectMasterTileSet.GetSourceCount()} sources.");
	}


	public AssetData GetAsset(string name)
	{
		foreach (var asset in assets)
		{
			if (asset.Name == name)
			{
				return asset;
			}
		}
		return null;
	}

	public List<AssetData> GetAllAssets()
	{
		return assets;
	}

	public AssetData GetAssetBySourceAndAtlas(int sourceIdInMasterSet, Vector2I atlasCoordsInSource)
	{
		foreach (AssetData asset in assets) // 'assets' is the List<AssetData>
		{
			// We are looking for specific tiles within a tileset, not single images used as tiles.
			// And also not the "Eraser" which has SourceIdInTileSet = -1
			if (asset.SourceIdInTileSet == -1 || asset.IsSingleImage())
			{
				// The check asset.IsSingleImage() might be true if TileSetResourcePath is null.
				// However, after the MasterTileSet change, all drawable assets (including former single images)
				// should have a valid TileSetResourcePath (MasterTileSetPath) and a SourceIdInTileSet.
				// The main distinction is SourceIdInTileSet == -1 for non-tile tools like Eraser.
				// For auto-tiling, we are interested in tiles that are part of an atlas.
				if (asset.SourceIdInTileSet == -1 && sourceIdInMasterSet == -1 && asset.Name == "Eraser") {
					// Special case if we ever need to "get" the eraser this way, but generally not needed for wall checks.
					// return asset;
				}
				// Skip if it's a tool like eraser or if it's a single image that wasn't processed into the master tileset
				// (though all displayable assets should be in master tileset now).
				// The crucial check is asset.SourceIdInTileSet == sourceIdInMasterSet.
			}

			if (asset.SourceIdInTileSet == sourceIdInMasterSet &&
				asset.AtlasCoordsInTileSet == atlasCoordsInSource)
			{
				return asset;
			}
		}
		// GD.PrintErr($"AssetManager: Asset not found for SourceID {sourceIdInMasterSet} and AtlasCoords {atlasCoordsInSource} in MasterTileSet.");
		return null;
	}

	public bool DeleteGlobalAsset(AssetData assetDataToDelete)
	{
		if (assetDataToDelete == null)
		{
			GD.PrintErr("DeleteGlobalAsset: assetDataToDelete is null.");
			return false;
		}

		if (assetDataToDelete.Name == "Eraser" && string.IsNullOrEmpty(assetDataToDelete.OriginalImageFilePath)) {
			GD.PrintWarn("DeleteGlobalAsset: Attempted to delete the virtual Eraser asset. Operation skipped.");
			return true;
		}

		if (globalSettings == null) {
			GD.PrintErr("DeleteGlobalAsset: GlobalSettings not available.");
			return false;
		}
		string globalRoot = globalSettings.GetGlobalAssetPath();
		if (string.IsNullOrEmpty(globalRoot)) {
			GD.PrintErr("DeleteGlobalAsset: Global asset root path is not configured.");
			return false;
		}

		(string imagePath, string metadataPath, string tileSetTresPath) =
			GetGlobalFilePathsForAssetDeletion(assetDataToDelete, globalRoot);

		bool allDeletionsSuccessful = true;
		// bool anyFileExisted = false; // Not strictly needed for return logic but useful for more nuanced feedback

		// Delete Image File
		if (!string.IsNullOrEmpty(imagePath))
		{
			if (FileAccess.FileExists(imagePath))
			{
				// anyFileExisted = true;
				Error err = DirAccess.RemoveAbsolute(imagePath);
				if (err == Error.Ok)
				{
					GD.Print($"Successfully deleted image file: {imagePath}");
				}
				else
				{
					GD.PrintErr($"Failed to delete image file: {imagePath}. Error: {err}");
					allDeletionsSuccessful = false;
				}
			}
			else { GD.Print($"DeleteGlobalAsset: Image file not found, skipping: {imagePath}"); }
		} else {
			GD.PrintWarn($"DeleteGlobalAsset: Image path was null or empty for {assetDataToDelete.Name}, cannot delete image. This implies invalid AssetData.");
			allDeletionsSuccessful = false;
		}

		// Delete Metadata File
		if (!string.IsNullOrEmpty(metadataPath))
		{
			if (FileAccess.FileExists(metadataPath))
			{
				// anyFileExisted = true;
				Error err = DirAccess.RemoveAbsolute(metadataPath);
				if (err == Error.Ok)
				{
					GD.Print($"Successfully deleted metadata file: {metadataPath}");
				}
				else
				{
					GD.PrintErr($"Failed to delete metadata file: {metadataPath}. Error: {err}");
					allDeletionsSuccessful = false;
				}
			}
			else { GD.Print($"DeleteGlobalAsset: Metadata file not found, skipping: {metadataPath}"); }
		} else {
			GD.PrintWarn($"DeleteGlobalAsset: Metadata path was null or empty for {assetDataToDelete.Name}, cannot delete metadata. This implies invalid AssetData.");
			allDeletionsSuccessful = false;
		}

		// Delete TileSet .tres File (if it exists)
		if (!string.IsNullOrEmpty(tileSetTresPath))
		{
			if (FileAccess.FileExists(tileSetTresPath))
			{
				// anyFileExisted = true;
				Error err = DirAccess.RemoveAbsolute(tileSetTresPath);
				if (err == Error.Ok)
				{
					GD.Print($"Successfully deleted TileSet .tres file: {tileSetTresPath}");
				}
				else
				{
					GD.PrintErr($"Failed to delete TileSet .tres file: {tileSetTresPath}. Error: {err}");
					allDeletionsSuccessful = false;
				}
			}
			else { GD.Print($"DeleteGlobalAsset: TileSet .tres file not found, skipping: {tileSetTresPath}"); }
		}
		// If tileSetTresPath was null (not a spritesheet asset), it's not an error, so allDeletionsSuccessful remains unchanged.

		return allDeletionsSuccessful;
	}

	private (string imagePath, string metadataPath, string tileSetTresPath) GetGlobalFilePathsForAssetDeletion(AssetData assetData, string globalAssetRootPath)
	{
		if (assetData == null || string.IsNullOrEmpty(assetData.OriginalImageFilePath))
		{
			GD.PrintErr($"GetGlobalFilePathsForAssetDeletion: AssetData is null or has no OriginalImageFilePath. Cannot determine file paths for asset: {assetData?.Name}");
			return (null, null, null);
		}

		// assetData.OriginalImageFilePath is stored relative to globalAssetRootPath, e.g., "images/MySheet.png"
		string imageFileNameWithExt = System.IO.Path.GetFileName(assetData.OriginalImageFilePath);
		string baseName = System.IO.Path.GetFileNameWithoutExtension(imageFileNameWithExt);

		if (string.IsNullOrEmpty(baseName))
		{
			GD.PrintErr($"GetGlobalFilePathsForAssetDeletion: Could not determine base name from OriginalImageFilePath: {assetData.OriginalImageFilePath} for asset: {assetData.Name}");
			return (null, null, null);
		}

		string imagePath = globalAssetRootPath.PlusFile(assetData.OriginalImageFilePath);
		string metadataPath = globalAssetRootPath.PlusFile("metadata").PlusFile(baseName + ".json");

		string tileSetTresPath = null;
		// assetData.TileSetResourcePath is stored relative to globalAssetRootPath, e.g., "tilesets/MySheet.tres"
		if (!string.IsNullOrEmpty(assetData.TileSetResourcePath))
		{
			tileSetTresPath = globalAssetRootPath.PlusFile(assetData.TileSetResourcePath);
		}

		return (imagePath, metadataPath, tileSetTresPath);
	}
}
