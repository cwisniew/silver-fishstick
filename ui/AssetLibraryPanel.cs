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

	// New UI Element References for Asset Display & Deletion
	private ItemList assetDisplayList;
	private Button deleteSelectedAssetButton;
	private AssetManager assetManagerInstance; // For accessing asset list
	private Label statusMessageLabel; // For user feedback, assumed to be added to .tscn

	// Deletion Confirmation
	private ConfirmationDialog deleteAssetConfirmationDialog;
	private AssetData assetToDeleteHolder; // To hold asset data while dialog is up

	// Edit Mode State
	private AssetData currentlyEditingAssetData = null;
	private bool isInEditMode = false;
	private Button editAssetButton;
	private Button cancelEditButton;
	// Assuming 'importButton' is the one to open FileDialog for new assets (already referenced)
	// Assuming 'confirmImportButton' is the one to confirm new import or apply edit changes (already referenced)

	// Need reference to pathInputLineEdit to make it read-only during edit and to get its value.
	// pathInputLineEdit is not currently a class member, it's used locally in _OnImportFileDialog_FileSelected.
	// Let's assume it's the LineEdit for the image path display, which is not explicitly named in _Ready yet.
	// For now, I'll assume it's named "PathInputLineEdit" and is a child of "HBoxPath" in the .tscn
	private LineEdit pathInputLineEdit;


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

		// Get AssetManager instance (used by RefreshAssetDisplayList and potentially delete)
		assetManagerInstance = GetNode<AssetManager>("/root/AssetManager");
		if (assetManagerInstance == null)
		{
			GD.PrintErr("AssetLibraryPanel: AssetManager not found! Asset display and deletion will not work.");
		}

		// Get references for new UI elements
		// Assuming the VBoxContainer is named "VBoxContainer" directly under the PanelContainer root.
		// Adjust paths if the .tscn structure is different (e.g. if SettingsVBox is a direct child of root)
		assetDisplayList = GetNodeOrNull<ItemList>("VBoxContainer/AssetListScroll/AssetDisplayList");
		deleteSelectedAssetButton = GetNodeOrNull<Button>("VBoxContainer/DeleteSelectedAssetButton");

		if (assetDisplayList == null) GD.PrintWarn("AssetLibraryPanel: AssetDisplayList not found at VBoxContainer/AssetListScroll/AssetDisplayList. Asset display will not work.");
		else
		{
			assetDisplayList.ItemSelected += _OnAssetDisplayListItemSelected;
			assetDisplayList.NothingSelected += _OnAssetDisplayListNothingSelected;
		}

		if (deleteSelectedAssetButton == null) GD.PrintWarn("AssetLibraryPanel: DeleteSelectedAssetButton not found at VBoxContainer/DeleteSelectedAssetButton. Asset deletion will not work.");
		else
		{
			deleteSelectedAssetButton.Disabled = true; // Initial state
			// Remove placeholder connection if previously set by tools, connect to actual logic
			// Note: Direct removal of lambda might not work if not identical. Safer to ensure it's not added multiple times if _Ready could re-run.
			// However, for _Ready, direct connection is fine.
			deleteSelectedAssetButton.Pressed -= _OnDeleteSelectedAssetPlaceholder; // Attempt to remove if it was placeholder
			deleteSelectedAssetButton.Pressed += _OnDeleteSelectedAssetButtonPressed; // Connect to the actual handler
		}

		statusMessageLabel = GetNodeOrNull<Label>("VBoxContainer/StatusMessageLabel"); // Assuming path in .tscn
		if (statusMessageLabel == null) GD.PrintWarn("AssetLibraryPanel: StatusMessageLabel not found. Status messages will not be displayed.");
		else statusMessageLabel.Text = ""; // Clear initially

		// Connect own AssetsChanged signal to refresh its list
		this.AssetsChanged += RefreshAssetDisplayList;

		// Setup Confirmation Dialog
		deleteAssetConfirmationDialog = new ConfirmationDialog
		{
			Title = "Confirm Deletion",
			Exclusive = true, // Modal behavior
			Visible = false // Initial state
			// DialogText will be set when used
		};
		Button confirmDeleteBtnInDialog = deleteAssetConfirmationDialog.GetOkButton(); // Godot names it "OK" by default
		confirmDeleteBtnInDialog.Text = "Delete"; // Customize text
		deleteAssetConfirmationDialog.Confirmed += _ConfirmDeleteAsset;
		// Optional: Handle cancel to clear assetToDeleteHolder if needed, but it's cleared in _ConfirmDeleteAsset anyway.
		// deleteAssetConfirmationDialog.Canceled += () => { assetToDeleteHolder = null; statusMessageLabel.Text = "Deletion cancelled."; };
		AddChild(deleteAssetConfirmationDialog);


		// Initial population of the asset list
		RefreshAssetDisplayList();

		// Try to get PathInputLineEdit - its path might need verification based on actual .tscn
		// Assuming it's alongside AssetNameLineEdit and CategoryOptionButton's parent HBoxes in structure.
		// If 'MetadataInputContainer' is the direct parent of HBoxName, HBoxCategory, etc.
		pathInputLineEdit = GetNodeOrNull<LineEdit>("VBoxContainer/HSplitContainer/MetadataInputContainer/HBoxPath/PathInputLineEdit");
		if (pathInputLineEdit == null) GD.PrintWarn("AssetLibraryPanel: PathInputLineEdit at VBoxContainer/HSplitContainer/MetadataInputContainer/HBoxPath/PathInputLineEdit not found. Edit mode might not correctly set it or make it read-only.");

		// Edit Mode Button Setup - paths need to match manual .tscn additions
		editAssetButton = GetNodeOrNull<Button>("VBoxContainer/EditAssetButton");
		cancelEditButton = GetNodeOrNull<Button>("VBoxContainer/CancelEditButton");

		if (editAssetButton != null) {
			editAssetButton.Disabled = true;
			editAssetButton.Pressed += _OnEditAssetButtonPressed;
		} else { GD.PrintWarn("AssetLibraryPanel: EditAssetButton not found (expected at VBoxContainer/EditAssetButton)."); }

		if (cancelEditButton != null) {
			cancelEditButton.Visible = false;
			cancelEditButton.Pressed += _OnCancelEditButtonPressed;
		} else { GD.PrintWarn("AssetLibraryPanel: CancelEditButton not found (expected at VBoxContainer/CancelEditButton)."); }

		_UpdateActionButtonsState(); // Call once to set initial states of all action buttons
	}

	private void _OnConfirmImportButtonPressed()
	{
		if (isInEditMode) {
			_ApplyEditChanges();
			return;
		}

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
		EmitSignal(SignalName.AssetsChanged); // This will trigger RefreshAssetDisplayList via the connected signal
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

	// --- Asset Display and Deletion Logic ---
	public void RefreshAssetDisplayList()
	{
		if (assetDisplayList == null || assetManagerInstance == null)
		{
			GD.PrintErr("AssetLibraryPanel: Cannot refresh asset display. ItemList or AssetManager not available.");
			return;
		}

		assetDisplayList.Clear();
		List<AssetData> allAssets = assetManagerInstance.GetAllAssets();

		if (allAssets == null)
		{
			GD.Print("AssetLibraryPanel: No assets returned from AssetManager.");
			return;
		}

		foreach (AssetData asset in allAssets)
		{
			if (asset.Name == "Eraser") continue; // Don't list the virtual Eraser tool

			int itemIndex = assetDisplayList.AddItem(asset.Name, asset.PreviewTexture);
			assetDisplayList.SetItemMetadata(itemIndex, asset.Name); // Store asset name for identification
		}
		_UpdateDeleteButtonState();
	}

	private void _OnAssetDisplayListItemSelected(long index)
	{
		_UpdateActionButtonsState();
	}

	private void _OnAssetDisplayListNothingSelected()
	{
		_UpdateActionButtonsState();
	}

	private void _UpdateActionButtonsState() // Renamed and expanded
	{
		if (assetDisplayList == null) return;

		int[] selectedIndices = assetDisplayList.GetSelectedItems();
		bool singleItemSelected = selectedIndices.Length == 1;
		bool itemsSelected = selectedIndices.Length > 0;

		string selectedAssetName = "";
		if (singleItemSelected) {
			selectedAssetName = assetDisplayList.GetItemMetadata((int)selectedIndices[0]).AsString();
		}

		if (deleteSelectedAssetButton != null) {
			// Disable delete if in edit mode, or if nothing/Eraser is selected
			deleteSelectedAssetButton.Disabled = isInEditMode || !itemsSelected || (singleItemSelected && selectedAssetName == "Eraser");
		}
		if (editAssetButton != null) {
			// Disable edit if in edit mode, or if not a single item / Eraser is selected
			editAssetButton.Disabled = isInEditMode || !singleItemSelected || (singleItemSelected && selectedAssetName == "Eraser");
		}

		// Disable asset list interaction and "Import New Asset..." button when in edit mode
		if (assetDisplayList != null) assetDisplayList.Disabled = isInEditMode;
		if (importButton != null) importButton.Disabled = isInEditMode;
		// ConfirmImportButton text and CancelEditButton visibility are handled by specific edit mode methods.
	}

	private void _OnDeleteSelectedAssetButtonPressed()
	{
		if (statusMessageLabel != null) statusMessageLabel.Text = "";

		if (assetDisplayList == null || assetManagerInstance == null)
		{
			if (statusMessageLabel != null) statusMessageLabel.Text = "Error: Asset list or manager not ready.";
			else GD.PrintErr("AssetLibraryPanel: AssetDisplayList or AssetManagerInstance is null in _OnDeleteSelectedAssetButtonPressed.");
			return;
		}

		int[] selectedIndices = assetDisplayList.GetSelectedItems();
		if (selectedIndices.Length == 0)
		{
			if (statusMessageLabel != null) statusMessageLabel.Text = "No asset selected to delete.";
			else GD.Print("AssetLibraryPanel: No asset selected to delete.");
			return;
		}
		if (selectedIndices.Length > 1)
		{
			if (statusMessageLabel != null) statusMessageLabel.Text = "Please select only one asset to delete at a time.";
			else GD.Print("AssetLibraryPanel: Multiple assets selected. Please select only one for deletion.");
			return;
		}

		string selectedAssetNameKey = assetDisplayList.GetItemMetadata((int)selectedIndices[0]).AsString();
		assetToDeleteHolder = assetManagerInstance.GetAssetByName(selectedAssetNameKey);

		if (assetToDeleteHolder == null)
		{
			string errorMsg = $"Error: Could not find asset data for '{selectedAssetNameKey}'.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg;
			else GD.PrintErr(errorMsg);
			return;
		}

		if (assetToDeleteHolder.Name == "Eraser")
		{
			string infoMsg = "The Eraser tool cannot be deleted from the library.";
			if (statusMessageLabel != null) statusMessageLabel.Text = infoMsg;
			else GD.Print(infoMsg);
			assetToDeleteHolder = null; // Clear holder as we won't proceed
			return;
		}

		string imagePathForDialog = string.IsNullOrEmpty(assetToDeleteHolder.OriginalImageFilePath) ? "[No Image File]" : assetToDeleteHolder.OriginalImageFilePath;
		deleteAssetConfirmationDialog.DialogText = $"Permanently delete asset '{assetToDeleteHolder.Name}' and its associated files?\n" +
												   $"(Image: {imagePathForDialog})\n" +
												   "This action cannot be undone from within the editor.";
		deleteAssetConfirmationDialog.PopupCentered();
	}

	private void _ConfirmDeleteAsset()
	{
		if (statusMessageLabel != null) statusMessageLabel.Text = ""; // Clear previous status

		if (assetToDeleteHolder == null || assetManagerInstance == null || globalSettings == null)
		{
			string errorMsg = "Error: Asset data, AssetManager, or GlobalSettings not available for deletion.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg;
			else GD.PrintErr(errorMsg);
			assetToDeleteHolder = null; // Clear if something critical is missing
			return;
		}

		GD.Print($"Confirmed deletion for asset: {assetToDeleteHolder.Name}");
		bool deleteSuccess = assetManagerInstance.DeleteGlobalAsset(assetToDeleteHolder);

		string assetName = assetToDeleteHolder.Name; // Store before clearing holder
		assetToDeleteHolder = null; // Clear the holder immediately after use

		if (deleteSuccess)
		{
			string successMsg = $"Asset '{assetName}' deleted successfully.";
			if (statusMessageLabel != null) statusMessageLabel.Text = successMsg;
			GD.Print(successMsg);

			assetManagerInstance.RefreshAssets(); // Reload all assets after deletion
			// RefreshAssetDisplayList(); // This will be called automatically because RefreshAssets should emit AssetsChanged if AssetManager is connected to its own signal, or if MainScene triggers it.
										// AssetManager.RefreshAssets() itself doesn't emit. AssetLibraryPanel's AssetsChanged is for *its own* changes.
										// So, after AssetManager is refreshed, we need to tell this panel to update its list.
										// The most straightforward way is to call it directly here, or ensure MainScene calls it after AssetManager refresh.
										// Since AssetManager.RefreshAssets() doesn't emit a signal, direct call is better here.
			RefreshAssetDisplayList(); // Explicitly refresh this panel's view.

			// Notify external listeners (like MainScene) that the asset library has changed.
			// This panel already has an AssetsChanged signal.
			EmitSignal(SignalName.AssetsChanged);
		}
		else
		{
			string errorMsg = $"Error: Failed to delete asset '{assetName}'. Check logs for details.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg;
			GD.PrintErr(errorMsg);
		}
	}
	// Removed _OnDeleteSelectedAssetPlaceholder as it's replaced by _OnDeleteSelectedAssetButtonPressed

	private void _OnEditAssetButtonPressed()
	{
		if (statusMessageLabel != null) statusMessageLabel.Text = "";
		if (assetDisplayList == null || assetManagerInstance == null || globalSettings == null)
		{
			if(statusMessageLabel != null) statusMessageLabel.Text = "Error: Cannot initiate edit mode. System not ready.";
			return;
		}

		int[] selectedIndices = assetDisplayList.GetSelectedItems();
		if (selectedIndices.Length != 1) {
			if (statusMessageLabel != null) statusMessageLabel.Text = "Select a single asset to edit.";
			return;
		}

		string selectedAssetNameKey = assetDisplayList.GetItemMetadata((int)selectedIndices[0]).AsString();
		AssetData assetToEdit = assetManagerInstance.GetAssetByName(selectedAssetNameKey);

		if (assetToEdit == null || assetToEdit.Name == "Eraser" || string.IsNullOrEmpty(assetToEdit.OriginalImageFilePath)) {
			if (statusMessageLabel != null) statusMessageLabel.Text = "This asset cannot be edited (no source image or is Eraser).";
			else GD.PrintWarn("AssetLibraryPanel: Attempted to edit an uneditable asset.");
			return;
		}

		currentlyEditingAssetData = assetToEdit;
		isInEditMode = true;

		string globalRoot = globalSettings.GetGlobalAssetPath();
		string imageFileNameOnly = System.IO.Path.GetFileName(assetToEdit.OriginalImageFilePath);
		string baseName = System.IO.Path.GetFileNameWithoutExtension(imageFileNameOnly);
		string metadataFilePath = globalRoot.PlusFile("metadata").PlusFile(baseName + ".json");

		AssetMetadata metadata = null;
		if (FileAccess.FileExists(metadataFilePath)) {
			using var file = FileAccess.Open(metadataFilePath, FileAccess.ModeFlags.Read);
			if (file != null) {
				string content = file.GetAsText();
				var parsedJson = Json.ParseString(content);
				if (parsedJson.VariantType != Variant.Type.Nil) {
					metadata = AssetMetadata.FromJsonDictionary(parsedJson.AsGodotDictionary());
				} else { GD.PrintErr($"AssetLibraryPanel: Failed to parse JSON from {metadataFilePath}"); }
			} else { GD.PrintErr($"AssetLibraryPanel: Failed to open metadata file {metadataFilePath} for reading. Error: {FileAccess.GetOpenError()}");}
		} else { GD.PrintErr($"AssetLibraryPanel: Metadata file not found at {metadataFilePath}"); }


		if (metadata == null) {
			if (statusMessageLabel != null) statusMessageLabel.Text = $"Error: Could not load metadata for {assetToEdit.Name}.";
			else GD.PrintErr($"AssetLibraryPanel: Could not load metadata for {assetToEdit.Name}.");
			_ResetToImportMode();
			return;
		}

		// Populate UI fields
		if (pathInputLineEdit != null) {
			 pathInputLineEdit.Text = globalRoot.PlusFile(assetToEdit.OriginalImageFilePath);
			 pathInputLineEdit.Editable = false;
		}

		if(imagePreview != null && !string.IsNullOrEmpty(assetToEdit.OriginalImageFilePath)) {
			string fullImgPath = globalRoot.PlusFile(assetToEdit.OriginalImageFilePath);
			if(FileAccess.FileExists(fullImgPath)) {
				Texture2D tex = ResourceLoader.Load<Texture2D>(fullImgPath);
				imagePreview.Texture = tex;
			} else { imagePreview.Texture = null; GD.PrintWarn($"Could not load image preview for edit: {fullImgPath}"); }
		} else if (imagePreview != null) { imagePreview.Texture = null; }


		if (assetNameLineEdit != null) {
			assetNameLineEdit.Text = metadata.AssetName;
			assetNameLineEdit.Editable = true; // Ensure Asset Name is editable in Edit Mode
		}
		if (categoryOptionButton != null) {
			bool categoryFound = false;
			for(int i=0; i < categoryOptionButton.ItemCount; ++i) {
				if (categoryOptionButton.GetItemText(i).Equals(metadata.Category, StringComparison.OrdinalIgnoreCase)) {
					categoryOptionButton.Selected = i;
					categoryFound = true;
					break;
				}
			}
			if (!categoryFound) {
				categoryOptionButton.Selected = (int)AssetManager.AssetCategory.General;
				GD.PrintWarn($"Category '{metadata.Category}' for asset '{metadata.AssetName}' not found in OptionButton. Defaulting.");
			}
			categoryOptionButton.Disabled = false;
		}

		if (isSpritesheetCheckBox != null) {
			isSpritesheetCheckBox.ButtonPressed = metadata.IsSpritesheet;
			isSpritesheetCheckBox.Disabled = true;
		}
		if (spritesheetSettingsContainer != null) spritesheetSettingsContainer.Visible = metadata.IsSpritesheet;

		if (tileWidthSpinBox != null) { tileWidthSpinBox.Value = metadata.TileWidth; tileWidthSpinBox.Editable = false; }
		if (tileHeightSpinBox != null) { tileHeightSpinBox.Value = metadata.TileHeight; tileHeightSpinBox.Editable = false; }
		if (separationXSpinBox != null) { separationXSpinBox.Value = metadata.SeparationX; separationXSpinBox.Editable = false; }
		if (separationYSpinBox != null) { separationYSpinBox.Value = metadata.SeparationY; separationYSpinBox.Editable = false; }
		if (marginXSpinBox != null) { marginXSpinBox.Value = metadata.MarginX; marginXSpinBox.Editable = false; }
		if (marginYSpinBox != null) { marginYSpinBox.Value = metadata.MarginY; marginYSpinBox.Editable = false; }

		if (confirmImportButton != null) confirmImportButton.Text = "Apply Metadata Changes";
		if (cancelEditButton != null) cancelEditButton.Visible = true;
		// importButton is handled by _UpdateActionButtonsState

		if (statusMessageLabel != null) statusMessageLabel.Text = $"Editing metadata for: {metadata.AssetName}";
		_UpdateActionButtonsState();
	}

	private void _OnCancelEditButtonPressed()
	{
		_ResetToImportMode();
		if (statusMessageLabel != null) statusMessageLabel.Text = "Edit cancelled.";
	}

	private void _ResetToImportMode()
	{
		isInEditMode = false;
		currentlyEditingAssetData = null;

		if (pathInputLineEdit != null) { pathInputLineEdit.Text = ""; pathInputLineEdit.Editable = true; }
		if (assetNameLineEdit != null) { assetNameLineEdit.Text = ""; assetNameLineEdit.Editable = true; }
		if (categoryOptionButton != null) { categoryOptionButton.Selected = 0; categoryOptionButton.Disabled = false; }
		if (imagePreview != null) imagePreview.Texture = null;
		selectedImageFilePath = ""; // Clear selected image path for new import

		if (isSpritesheetCheckBox != null) { isSpritesheetCheckBox.ButtonPressed = false; isSpritesheetCheckBox.Disabled = false; }
		if (spritesheetSettingsContainer != null) spritesheetSettingsContainer.Visible = false;

		if (tileWidthSpinBox != null) { tileWidthSpinBox.Value = 16; tileWidthSpinBox.Editable = true; }
		if (tileHeightSpinBox != null) { tileHeightSpinBox.Value = 16; tileHeightSpinBox.Editable = true; }
		if (separationXSpinBox != null) { separationXSpinBox.Value = 0; separationXSpinBox.Editable = true; }
		if (separationYSpinBox != null) { separationYSpinBox.Value = 0; separationYSpinBox.Editable = true; }
		if (marginXSpinBox != null) { marginXSpinBox.Value = 0; marginXSpinBox.Editable = true; }
		if (marginYSpinBox != null) { marginYSpinBox.Value = 0; marginYSpinBox.Editable = true; }

		if (confirmImportButton != null) confirmImportButton.Text = "Confirm Import";
		if (cancelEditButton != null) cancelEditButton.Visible = false;
		// importButton state handled by _UpdateActionButtonsState

		_UpdateActionButtonsState();
		if (statusMessageLabel != null && statusMessageLabel.Text.StartsWith("Editing metadata for:")) {
			statusMessageLabel.Text = "";
		}
	}

	private void _ApplyEditChanges()
	{
		if (statusMessageLabel != null) statusMessageLabel.Text = ""; // Clear previous status

		if (currentlyEditingAssetData == null || globalSettings == null || assetManagerInstance == null ||
			categoryOptionButton == null || assetNameLineEdit == null) // Add assetNameLineEdit null check
		{
			string errorMsg = "Error: Cannot apply changes. Internal state error or UI elements missing.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg; else GD.PrintErr(errorMsg);
			_ResetToImportMode();
			return;
		}

		// Get new proposed display name
        string newDisplayName = assetNameLineEdit.Text.Trim();

        // Validate new display name
        if (string.IsNullOrEmpty(newDisplayName))
        {
            if (statusMessageLabel != null) statusMessageLabel.Text = "Error: Asset display name cannot be empty.";
            else GD.PrintErr("Error: Asset display name cannot be empty.");
            assetNameLineEdit.GrabFocus(); // Focus the problematic field
            return;
        }

		string newCategoryString = categoryOptionButton.GetItemText(categoryOptionButton.Selected);

		// Determine metadata file path
		string globalRoot = globalSettings.GetGlobalAssetPath();
		if (string.IsNullOrEmpty(globalRoot) || string.IsNullOrEmpty(currentlyEditingAssetData.OriginalImageFilePath))
		{
			string errorMsg = "Error: Global root path or original image path for asset is invalid.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg; else GD.PrintErr(errorMsg);
			_ResetToImportMode();
			return;
		}
		string imageFileNameOnly = System.IO.Path.GetFileName(currentlyEditingAssetData.OriginalImageFilePath);
		string baseName = System.IO.Path.GetFileNameWithoutExtension(imageFileNameOnly);
		if (string.IsNullOrEmpty(baseName))
		{
			string errorMsg = $"Error: Could not derive base name for asset '{currentlyEditingAssetData.Name}'.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg; else GD.PrintErr(errorMsg);
			_ResetToImportMode();
			return;
		}
		string metadataFilePath = globalRoot.PlusFile("metadata").PlusFile(baseName + ".json");

		if (!FileAccess.FileExists(metadataFilePath))
		{
			string errorMsg = $"Error: Metadata file not found for '{baseName}'. Cannot apply changes.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg; else GD.PrintErr(errorMsg);
			_ResetToImportMode();
			return;
		}

		// Load existing metadata
		string content;
		using (var file = FileAccess.Open(metadataFilePath, FileAccess.ModeFlags.Read))
		{
			if (file == null || FileAccess.GetOpenError() != Error.Ok) {
				string errorMsg = $"Error reading metadata file '{metadataFilePath}': {FileAccess.GetOpenError()}";
				if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg; else GD.PrintErr(errorMsg);
				_ResetToImportMode();
				return;
			}
			content = file.GetAsText();
		}

		Variant parsedJsonVariant = Json.ParseString(content);
		if (parsedJsonVariant.VariantType == Variant.Type.Nil)
		{
			string errorMsg = $"Error parsing metadata JSON for '{baseName}'. File might be corrupt.";
			if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg; else GD.PrintErr(errorMsg);
			_ResetToImportMode();
			return;
		}

		Godot.Collections.Dictionary<string, Variant> metadataDict = parsedJsonVariant.AsGodotDictionary<string, Variant>();

		// Check for changes
        string oldDisplayName = metadataDict.GetValueOrDefault("AssetName", "").ToString();
        string oldCategoryString = metadataDict.GetValueOrDefault("Category", "").ToString();

        bool nameActuallyChanged = (oldDisplayName != newDisplayName);
        bool categoryActuallyChanged = (!oldCategoryString.Equals(newCategoryString, StringComparison.OrdinalIgnoreCase)); // Case-insensitive for category string

        if (!nameActuallyChanged && !categoryActuallyChanged) {
            if (statusMessageLabel != null) statusMessageLabel.Text = "No changes made to display name or category.";
            else GD.Print("No changes made to display name or category.");
            _ResetToImportMode();
            RefreshAssetDisplayList();
            return;
        }

        // Update metadata dictionary
        if (nameActuallyChanged) {
            metadataDict["AssetName"] = newDisplayName;
        }
        if (categoryActuallyChanged) {
            metadataDict["Category"] = newCategoryString;
        }

		// Save updated metadata
		string updatedJsonString = Json.Stringify(metadataDict, "\t");
		using (var file = FileAccess.Open(metadataFilePath, FileAccess.ModeFlags.Write))
		{
			 if (file == null || FileAccess.GetOpenError() != Error.Ok) {
				string errorMsg = $"Error writing updated metadata to '{metadataFilePath}': {FileAccess.GetOpenError()}";
				if (statusMessageLabel != null) statusMessageLabel.Text = errorMsg; else GD.PrintErr(errorMsg);
				_ResetToImportMode();
				return;
			}
			file.StoreString(updatedJsonString);
		}

		string changedSummary = "";
        if (nameActuallyChanged && categoryActuallyChanged) changedSummary = $"Display name for '{oldDisplayName}' and category updated.";
        else if (nameActuallyChanged) changedSummary = $"Display name for '{oldDisplayName}' updated to '{newDisplayName}'.";
        else if (categoryActuallyChanged) changedSummary = $"Category for '{oldDisplayName}' (asset file '{baseName}') updated to '{newCategoryString}'."; // Clarified baseName for log clarity

        GD.Print($"Metadata for '{baseName}' updated. {changedSummary}");
		string statusUpdateMsg = $"{changedSummary} Reloading assets...";
        if (statusMessageLabel != null) statusMessageLabel.Text = statusUpdateMsg;


		assetManagerInstance.RefreshAssets();
		EmitSignal(nameof(AssetsChanged));

		_ResetToImportMode();
		RefreshAssetDisplayList();  // Explicitly call to ensure list is updated after AssetManager refresh & mode reset

		if (statusMessageLabel != null) statusMessageLabel.Text = $"{changedSummary} Assets reloaded.";
	}
}
