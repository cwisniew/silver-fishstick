using Godot;
using System; // For Enum
using System.IO; // For Path.GetFileNameWithoutExtension

public partial class AssetLibraryPanel : PanelContainer
{
	[Signal] public delegate void AssetsChangedEventHandler();

	private Button importButton;
	private TextureRect imagePreview;
	private LineEdit assetNameLineEdit;
	private OptionButton categoryOptionButton;
	private FileDialog importFileDialog;
	private CheckBox isSpritesheetCheckBox;
	private VBoxContainer spritesheetSettingsContainer;
	private GlobalSettings globalSettings; // Added
	private SpinBox tileWidthSpinBox;
	private SpinBox tileHeightSpinBox;
	private SpinBox separationXSpinBox;
	private SpinBox separationYSpinBox;
	private SpinBox marginXSpinBox;
	private SpinBox marginYSpinBox;
	private Button confirmImportButton;


	private string selectedImageFilePath; // To store the path of the selected image

	public override void _Ready()
	{
		// Get GlobalSettings singleton instance
		globalSettings = GetNode<GlobalSettings>("/root/GlobalSettings");
		if (globalSettings == null)
		{
			GD.PrintErr("AssetLibraryPanel: GlobalSettings singleton not found! Asset import will use fallback paths or fail.");
			// Optionally, disable import functionality if GlobalSettings is critical
		}

		// Get node references
		importButton = GetNode<Button>("VBoxContainer/ImportButton");
		imagePreview = GetNode<TextureRect>("VBoxContainer/HSplitContainer/ImagePreview");
		assetNameLineEdit = GetNode<LineEdit>("VBoxContainer/HSplitContainer/MetadataInputContainer/HBoxName/AssetNameLineEdit");
		categoryOptionButton = GetNode<OptionButton>("VBoxContainer/HSplitContainer/MetadataInputContainer/HBoxCategory/CategoryOptionButton");

		isSpritesheetCheckBox = GetNode<CheckBox>("VBoxContainer/HSplitContainer/MetadataInputContainer/HBoxIsSpritesheet/IsSpritesheetCheckBox");
		spritesheetSettingsContainer = GetNode<VBoxContainer>("VBoxContainer/HSplitContainer/MetadataInputContainer/SpritesheetSettingsContainer");
		tileWidthSpinBox = GetNode<SpinBox>("VBoxContainer/HSplitContainer/MetadataInputContainer/SpritesheetSettingsContainer/HBoxTileWidth/TileWidthSpinBox");
		tileHeightSpinBox = GetNode<SpinBox>("VBoxContainer/HSplitContainer/MetadataInputContainer/SpritesheetSettingsContainer/HBoxTileHeight/TileHeightSpinBox");
		separationXSpinBox = GetNode<SpinBox>("VBoxContainer/HSplitContainer/MetadataInputContainer/SpritesheetSettingsContainer/HBoxSeparationX/SeparationXSpinBox");
		separationYSpinBox = GetNode<SpinBox>("VBoxContainer/HSplitContainer/MetadataInputContainer/SpritesheetSettingsContainer/HBoxSeparationY/SeparationYSpinBox");
		marginXSpinBox = GetNode<SpinBox>("VBoxContainer/HSplitContainer/MetadataInputContainer/SpritesheetSettingsContainer/HBoxMarginX/MarginXSpinBox");
		marginYSpinBox = GetNode<SpinBox>("VBoxContainer/HSplitContainer/MetadataInputContainer/SpritesheetSettingsContainer/HBoxMarginY/MarginYSpinBox");
		confirmImportButton = GetNode<Button>("VBoxContainer/ConfirmImportButton");


		// Null checks for verification
		if (importButton == null) GD.PrintErr("ImportButton not found!");
		if (imagePreview == null) GD.PrintErr("ImagePreview not found!");
		if (assetNameLineEdit == null) GD.PrintErr("AssetNameLineEdit not found!");
		if (categoryOptionButton == null) GD.PrintErr("CategoryOptionButton not found!");
		if (isSpritesheetCheckBox == null) GD.PrintErr("IsSpritesheetCheckBox not found!");
		if (spritesheetSettingsContainer == null) GD.PrintErr("SpritesheetSettingsContainer not found!");
		if (tileWidthSpinBox == null) GD.PrintErr("TileWidthSpinBox not found!");
		// ... (could add more null checks for all new SpinBoxes and ConfirmImportButton)
		if (confirmImportButton == null) GD.PrintErr("ConfirmImportButton not found!");

		// Create and configure FileDialog
		importFileDialog = new FileDialog();
		importFileDialog.Title = "Import Image Asset";
		importFileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
		importFileDialog.AddFilter("*.png ; PNG Images");
		importFileDialog.AddFilter("*.jpg ; JPG Images");
		importFileDialog.AddFilter("*.jpeg ; JPEG Images"); // Added .jpeg as well
		importFileDialog.FileSelected += _OnImportFileDialog_FileSelected;
		AddChild(importFileDialog); // Add it to the scene tree so it can be displayed

		// Populate CategoryOptionButton
		// Assuming AssetManager is autoloaded and its enum is accessible
		// If AssetManager is not an autoload or the enum is nested, this might need adjustment.
		// For this example, direct access to the enum is assumed.
		var assetCategories = Enum.GetNames(typeof(AssetManager.AssetCategory));
		foreach (string categoryName in assetCategories)
		{
			categoryOptionButton.AddItem(categoryName);
		}
		if (categoryOptionButton.ItemCount > 0)
		{
			categoryOptionButton.Selected = 0; // Select the first item by default
		}

		// Connect import button signal
		importButton.Pressed += () => importFileDialog.PopupCentered();

		// Spritesheet settings visibility
		spritesheetSettingsContainer.Visible = false;
		isSpritesheetCheckBox.Toggled += _OnIsSpritesheetCheckBox_Toggled;

		// Connect ConfirmImportButton
		confirmImportButton.Pressed += _OnConfirmImportButtonPressed;
	}

	private void _OnConfirmImportButtonPressed()
	{
		// --- Get Global Path ---
		if (globalSettings == null) {
			OS.Alert("GlobalSettings not available! Cannot determine asset storage path.", "Import Error");
			GD.PrintErr("AssetLibraryPanel: GlobalSettings singleton instance is null in _OnConfirmImportButtonPressed.");
			return;
		}
		string globalAssetRootPath = globalSettings.GetGlobalAssetPath();
		if (string.IsNullOrEmpty(globalAssetRootPath)) {
			OS.Alert("Global asset root path is not configured in GlobalSettings!", "Import Error");
			GD.PrintErr("AssetLibraryPanel: Global asset root path is null or empty.");
			return;
		}
		// GlobalSettings.EnsureDefaultAssetDirectoryExists() is called in its _Ready and SetGlobalAssetPath.
		// So, we can assume the base path and its subdirectories should exist if GlobalSettings initialized correctly.
		// However, direct MakeDirRecursiveAbsolute calls here add robustness if the path was changed
		// and something failed, or if called before GlobalSettings._Ready somehow (unlikely for UI action).

		// 1. Input Validation
		if (string.IsNullOrEmpty(selectedImageFilePath))
		{
			OS.Alert("Please select an image first.", "Import Error");
			return;
		}
		if (string.IsNullOrWhiteSpace(assetNameLineEdit.Text))
		{
			OS.Alert("Please enter an asset name.", "Import Error");
			return;
		}

		string rawAssetName = assetNameLineEdit.Text;
		string safeAssetName = rawAssetName.Replace(" ", "_").Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "").Replace(".", "");
		if (string.IsNullOrWhiteSpace(safeAssetName))
		{
			OS.Alert("Asset name is invalid after sanitization. Please use alphanumeric characters and underscores.", "Import Error");
			return;
		}

		// --- Update Target Path Definitions ---
		string imageExtension = System.IO.Path.GetExtension(selectedImageFilePath);
		string targetImageFileName = safeAssetName + imageExtension; // e.g., MyAsset.png

		string targetImageDir = globalAssetRootPath.PlusFile("images");
		string metadataDir = globalAssetRootPath.PlusFile("metadata");
		string tileSetDir = globalAssetRootPath.PlusFile("tilesets");

		string targetImagePath = targetImageDir.PlusFile(targetImageFileName);
		string metadataFilePath = metadataDir.PlusFile(safeAssetName + ".json");

		// Path stored in metadata should be relative to the *specific subdirectory type* for clarity,
		// or relative to the global root. The plan suggests "tilesets/MySet.tres" relative to global root.
		string tileSetResourcePathForMeta = $"tilesets/{safeAssetName}.tres";
		string absoluteTileSetResourceSavePath = tileSetDir.PlusFile(safeAssetName + ".tres");


		// --- Ensure Directories for Saving ---
		// These ensure the specific subdirectories exist. GlobalSettings ensures the root.
		DirAccess.MakeDirRecursiveAbsolute(targetImageDir);
		DirAccess.MakeDirRecursiveAbsolute(metadataDir);
		DirAccess.MakeDirRecursiveAbsolute(tileSetDir);


		// 3. Check for Overwrites (using new absolute paths)
		if (FileAccess.FileExists(targetImagePath) ||
			FileAccess.FileExists(metadataFilePath) ||
			(isSpritesheetCheckBox.ButtonPressed && FileAccess.FileExists(absoluteTileSetResourceSavePath)))
		{
			OS.Alert("Asset with this name (image, metadata, or tileset) already exists. Please choose a different name or delete existing files.", "Import Error");
			return;
		}

		// 4. Copy Image File
		Error copyError = DirAccess.CopyAbsolute(selectedImageFilePath, targetImagePath);
		if (copyError != Error.Ok)
		{
			OS.Alert($"Failed to copy image file: {copyError}", "Import Error");
			return;
		}
		GD.Print($"Image copied to: {targetImagePath}");

		// 5. Create Metadata Object
		AssetMetadata metadata = new AssetMetadata
		{
			AssetName = assetNameLineEdit.Text.Trim(),
			ImageFileName = targetImageFileName, // Just the filename, e.g., "MyAsset.png"
			Category = categoryOptionButton.GetItemText(categoryOptionButton.Selected),
			IsSpritesheet = isSpritesheetCheckBox.ButtonPressed
		};

		if (metadata.IsSpritesheet)
		{
			metadata.TileWidth = (int)tileWidthSpinBox.Value;
			metadata.TileHeight = (int)tileHeightSpinBox.Value;
			metadata.SeparationX = (int)separationXSpinBox.Value;
			metadata.SeparationY = (int)separationYSpinBox.Value;
			metadata.MarginX = (int)marginXSpinBox.Value;
			metadata.MarginY = (int)marginYSpinBox.Value;
			metadata.TileSetResourcePath = tileSetResourcePathForMeta; // e.g., "tilesets/MyAsset.tres"
		}
		else
		{
			metadata.TileSetResourcePath = null;
		}

		// 6. Save Metadata JSON
		string jsonString = Json.Stringify(metadata, "\t"); // Use "\t" for pretty print
		using (var file = FileAccess.Open(metadataFilePath, FileAccess.ModeFlags.Write))
		{
			if (file == null)
			{
				OS.Alert($"Failed to open metadata file for writing: {FileAccess.GetOpenError()}", "Import Error");
				// Consider deleting the copied image if metadata saving fails
				// DirAccess.RemoveAbsolute(targetImagePath);
				return;
			}
			file.StoreString(jsonString);
		}
		GD.Print($"Metadata saved to: {metadataFilePath}");

		// 7. Process Spritesheet and Create TileSet
		if (metadata.IsSpritesheet)
		{
			Texture2D importedTexture = ResourceLoader.Load<Texture2D>(targetImagePath);
			if (importedTexture == null)
			{
				OS.Alert($"Failed to load copied texture from {targetImagePath} for TileSet creation.", "Import Error");
				return;
			}

			TileSet tileSet = new TileSet();
			tileSet.TileShape = TileSet.TileShapeEnum.Square;
			tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;
			tileSet.TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal;
			tileSet.TileSize = new Vector2I(metadata.TileWidth, metadata.TileHeight);

			TileSetAtlasSource atlasSource = new TileSetAtlasSource();
			atlasSource.Texture = importedTexture;
			atlasSource.TextureRegionSize = new Vector2I(metadata.TileWidth, metadata.TileHeight); // Should be redundant if TileSize is set on TileSet? Check docs.
			atlasSource.Margins = new Vector2I(metadata.MarginX, metadata.MarginY);
			atlasSource.Separation = new Vector2I(metadata.SeparationX, metadata.SeparationY);

			int sourceId = tileSet.AddSource(atlasSource);

			Vector2I gridSize = atlasSource.GetAtlasGridSize();

			GD.Print($"Texture size for TileSet: {importedTexture.GetSize()}, TileSize: {tileSet.TileSize}, Margins: {atlasSource.Margins}, Separation: {atlasSource.Separation}");
			GD.Print($"Calculated Atlas Grid Size for TileSet: {gridSize}");

			if (gridSize.X <= 0 || gridSize.Y <= 0) {
				OS.Alert($"Calculated grid size for TileSet is invalid ({gridSize}). Check TileSet parameters and image dimensions.", "TileSet Error");
				return;
			}

			for (int y = 0; y < gridSize.Y; y++)
			{
				for (int x = 0; x < gridSize.X; x++)
				{
					Vector2I atlasCoords = new Vector2I(x, y);
					atlasSource.CreateTile(atlasCoords);
				}
			}

			Error saveTresError = ResourceSaver.Save(tileSet, absoluteTileSetResourceSavePath); // Use absolute path for saving
			if (saveTresError != Error.Ok)
			{
				OS.Alert($"Failed to save TileSet resource to '{absoluteTileSetResourceSavePath}': {saveTresError}", "Import Error");
				return;
			}
			GD.Print($"TileSet resource saved to: {tileSetResourcePath}");
		}

		// 8. Post-Import
		OS.Alert($"Asset '{metadata.AssetName}' imported successfully!", "Import Success");
		// Clear form (optional, good UX)
		assetNameLineEdit.Text = "";
		imagePreview.Texture = null;
		selectedImageFilePath = null;
		isSpritesheetCheckBox.ButtonPressed = false; // This will also hide settings via _OnIsSpritesheetCheckBox_Toggled
		categoryOptionButton.Selected = 0;
		tileWidthSpinBox.Value = 16;
		tileHeightSpinBox.Value = 16;
		separationXSpinBox.Value = 0;
		separationYSpinBox.Value = 0;
		marginXSpinBox.Value = 0;
		marginYSpinBox.Value = 0;

		// Trigger AssetManager refresh
		AssetManager assetManager = GetNode<AssetManager>("/root/AssetManager");
		if (assetManager != null)
		{
			assetManager.RefreshAssets();
			GD.Print("AssetManager refreshed.");
			// TODO: Optionally, signal MainScene to refresh its UI if needed
		}
		else
		{
			GD.PrintErr("Could not find AssetManager to refresh.");
		}
		EmitSignal(SignalName.AssetsChanged);
	}


	private void _OnIsSpritesheetCheckBox_Toggled(bool toggledOn)
	{
		spritesheetSettingsContainer.Visible = toggledOn;
	}

	private void _OnImportFileDialog_FileSelected(string path)
	{
		selectedImageFilePath = path;
		GD.Print("Image file selected: " + path);

		Image image = new Image();
		Error err = image.Load(path); // Use Load directly for Godot 4 Image

		if (err == Error.Ok)
		{
			ImageTexture texture = ImageTexture.CreateFromImage(image);
			imagePreview.Texture = texture;
			GD.Print("Image loaded and preview set.");

			// Pre-fill AssetNameLineEdit with the file name without extension
			assetNameLineEdit.Text = Path.GetFileNameWithoutExtension(path);
		}
		else
		{
			GD.PrintErr("Error loading image: " + err.ToString());
			selectedImageFilePath = null; // Clear path if loading failed
			imagePreview.Texture = null; // Clear preview
		}
	}
}
