using Godot;

public partial class AppSettingsPanelController : PanelContainer
{
    [Signal]
    public delegate void GlobalAssetPathChangedAndRefreshedEventHandler();

    private LineEdit pathDisplayLineEdit;
    private LineEdit pathInputLineEdit;
    private Button browseButton;
    // Grid controls
    private CheckBox enableGridSnapCheckBox;
    private SpinBox gridSizeXSpinBox;
    private SpinBox gridSizeYSpinBox;

    // Action buttons & status
    private Button applyPathButton; // Retaining original name for button in .tscn
    private Button closeSettingsButton;
    private Label statusMessageLabel;
    private FileDialog directoryOpenDialog;

    private GlobalSettings globalSettings;
    private AssetManager assetManagerInstance;

    public override void _Ready()
    {
        // Get node references using the MarginContainer/SettingsVBox structure
        pathDisplayLineEdit = GetNode<LineEdit>("MarginContainer/SettingsVBox/PathDisplayLineEdit");
        pathInputLineEdit = GetNode<LineEdit>("MarginContainer/SettingsVBox/NewPathHBox/PathInputLineEdit");
        browseButton = GetNode<Button>("MarginContainer/SettingsVBox/NewPathHBox/BrowseButton");

        // Get Grid Setting Control References
        // Assuming HBox names from manual steps: EnableSnapHBox, GridSizeXHBox, GridSizeYHBox
        enableGridSnapCheckBox = GetNodeOrNull<CheckBox>("MarginContainer/SettingsVBox/EnableSnapHBox/EnableGridSnapCheckBox");
        gridSizeXSpinBox = GetNodeOrNull<SpinBox>("MarginContainer/SettingsVBox/GridSizeXHBox/GridSizeXSpinBox");
        gridSizeYSpinBox = GetNodeOrNull<SpinBox>("MarginContainer/SettingsVBox/GridSizeYHBox/GridSizeYSpinBox");

        if (enableGridSnapCheckBox == null) GD.PrintWarn("AppSettingsPanelController: EnableGridSnapCheckBox not found. Grid UI may be incomplete.");
        if (gridSizeXSpinBox == null) GD.PrintWarn("AppSettingsPanelController: GridSizeXSpinBox not found. Grid UI may be incomplete.");
        if (gridSizeYSpinBox == null) GD.PrintWarn("AppSettingsPanelController: GridSizeYSpinBox not found. Grid UI may be incomplete.");

        // Get action buttons and status label
        applyPathButton = GetNode<Button>("MarginContainer/SettingsVBox/ActionButtonsHBox/ApplyPathButton");
        closeSettingsButton = GetNode<Button>("MarginContainer/SettingsVBox/ActionButtonsHBox/CloseSettingsButton");
        statusMessageLabel = GetNode<Label>("MarginContainer/SettingsVBox/StatusMessageLabel");

        // Error checking for node paths (some already done by GetNodeOrNull warnings for grid)
        if (pathDisplayLineEdit == null) GD.PrintErr("AppSettingsPanelController: PathDisplayLineEdit not found.");
        if (pathInputLineEdit == null) GD.PrintErr("AppSettingsPanelController: PathInputLineEdit not found.");
        if (browseButton == null) GD.PrintErr("AppSettingsPanelController: BrowseButton not found.");
        if (applyPathButton == null) GD.PrintErr("AppSettingsPanelController: ApplyPathButton not found.");
        if (closeSettingsButton == null) GD.PrintErr("AppSettingsPanelController: CloseSettingsButton not found.");
        if (statusMessageLabel == null) GD.PrintErr("AppSettingsPanelController: StatusMessageLabel not found.");


        // Get singletons
        globalSettings = GetNode<GlobalSettings>("/root/GlobalSettings");
        assetManagerInstance = GetNode<AssetManager>("/root/AssetManager");

        if (globalSettings == null) GD.PrintErr("AppSettingsPanelController: GlobalSettings singleton not found.");
        if (assetManagerInstance == null) GD.PrintErr("AppSettingsPanelController: AssetManager singleton not found.");


        // Setup FileDialog for directory selection
        directoryOpenDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Title = "Select Global Asset Library Folder",
            Access = FileDialog.AccessEnum.Filesystem,
            Visible = false
        };
        AddChild(directoryOpenDialog);

        // Connect signals
        if (browseButton != null) browseButton.Pressed += _OnBrowseButtonPressed;
        if (directoryOpenDialog != null) directoryOpenDialog.DirSelected += _OnDirectorySelected;
        if (applyPathButton != null) applyPathButton.Pressed += _OnApplySettingsButtonPressed; // Renamed method
        if (closeSettingsButton != null) closeSettingsButton.Pressed += _OnCloseSettingsButtonPressed;

        this.VisibilityChanged += _OnVisibilityChanged;

        if (Visible)
        {
            _LoadCurrentSettingsToUI(); // Renamed method
        }
    }

    private void _OnVisibilityChanged()
    {
        if (Visible)
        {
            _LoadCurrentSettingsToUI(); // Renamed method
        }
    }

    private void _LoadCurrentSettingsToUI() // Renamed from _LoadCurrentPathToDisplay
    {
        if (globalSettings != null)
        {
            // Path
            if (pathDisplayLineEdit != null) pathDisplayLineEdit.Text = globalSettings.GetGlobalAssetPath();
            if (pathInputLineEdit != null) pathInputLineEdit.Text = globalSettings.GetGlobalAssetPath();

            // Grid Settings
            if (enableGridSnapCheckBox != null) enableGridSnapCheckBox.ButtonPressed = globalSettings.IsSnapToGridEnabled;

            if (gridSizeXSpinBox != null) {
                gridSizeXSpinBox.SetBlockSignals(true);
                gridSizeXSpinBox.Value = globalSettings.GridSize.X;
                gridSizeXSpinBox.SetBlockSignals(false);
            }
            if (gridSizeYSpinBox != null) {
                gridSizeYSpinBox.SetBlockSignals(true);
                gridSizeYSpinBox.Value = globalSettings.GridSize.Y;
                gridSizeYSpinBox.SetBlockSignals(false);
            }
        }
        if (statusMessageLabel != null) statusMessageLabel.Text = "";
    }

    private void _OnBrowseButtonPressed()
    {
        if (directoryOpenDialog == null) return;

        string initialDir = pathInputLineEdit.Text;
        if (!string.IsNullOrEmpty(initialDir) && DirAccess.DirExistsAbsolute(initialDir)) {
            directoryOpenDialog.CurrentDir = initialDir;
        } else if (globalSettings != null) {
             directoryOpenDialog.CurrentDir = globalSettings.GetGlobalAssetPath() ?? GlobalSettings.DefaultGlobalAssetPath;
        } else {
            directoryOpenDialog.CurrentDir = GlobalSettings.DefaultGlobalAssetPath;
        }
        directoryOpenDialog.PopupCentered();
    }

    private void _OnDirectorySelected(string dir)
    {
        if (pathInputLineEdit != null) pathInputLineEdit.Text = dir;
    }

    private void _OnApplySettingsButtonPressed() // Renamed from _OnApplyPathButtonPressed
    {
        if (pathInputLineEdit == null || statusMessageLabel == null || globalSettings == null) {
             if(statusMessageLabel!=null) statusMessageLabel.Text = "Error: Internal components missing.";
             else GD.PrintErr("AppSettingsPanelController: Critical components null in _OnApplySettingsButtonPressed.");
             return;
        }

        string newPathRaw = pathInputLineEdit.Text.Trim();
        bool pathSaveAttempted = false;
        bool pathSaveSuccess = true;
        bool pathActuallyChanged = false;
        string originalPath = globalSettings.GetGlobalAssetPath();

        if (!string.IsNullOrEmpty(newPathRaw))
        {
            pathSaveAttempted = true;
            string formattedNewPath = newPathRaw.EndsWith("/") ? newPathRaw : newPathRaw + "/";
            if (originalPath != formattedNewPath) {
                pathActuallyChanged = true;
                pathSaveSuccess = globalSettings.SetGlobalAssetPath(newPathRaw);
                 if (pathSaveSuccess) {
                    if (pathDisplayLineEdit != null) pathDisplayLineEdit.Text = globalSettings.GetGlobalAssetPath();
                 }
            }
        }

        bool gridSettingsActuallyChanged = false;
        if (enableGridSnapCheckBox != null && gridSizeXSpinBox != null && gridSizeYSpinBox != null)
        {
            Vector2I newGridSize = new Vector2I((int)gridSizeXSpinBox.Value, (int)gridSizeYSpinBox.Value);
            bool newSnapEnabled = enableGridSnapCheckBox.ButtonPressed;

            if (globalSettings.GridSize != newGridSize) {
                globalSettings.SetGridSize(newGridSize);
                gridSettingsActuallyChanged = true;
            }
            if (globalSettings.IsSnapToGridEnabled != newSnapEnabled) {
                globalSettings.SetSnapToGridEnabled(newSnapEnabled);
                gridSettingsActuallyChanged = true;
            }
        }

        string finalStatusMessage = "";
        if (pathSaveAttempted && pathActuallyChanged && !pathSaveSuccess) {
            finalStatusMessage = "Error: Failed to update asset path. Check console.";
        } else if (pathActuallyChanged && gridSettingsActuallyChanged) {
            finalStatusMessage = "Asset path and grid settings updated.";
        } else if (pathActuallyChanged) {
            finalStatusMessage = "Asset path updated.";
        } else if (gridSettingsActuallyChanged) {
            finalStatusMessage = "Grid settings updated.";
        } else if (pathSaveAttempted && !pathActuallyChanged && pathSaveSuccess) { // Path was valid but same as before
            finalStatusMessage = "Path unchanged. No changes applied if grid settings also same.";
        } else if (!pathSaveAttempted && !gridSettingsActuallyChanged) {
             finalStatusMessage = "No settings were changed.";
        } else {
             finalStatusMessage = "Settings checked, no changes applied.";
        }
        statusMessageLabel.Text = finalStatusMessage;

        if (pathActuallyChanged && pathSaveSuccess) {
            if (assetManagerInstance != null) {
                statusMessageLabel.Text += "\nReloading assets...";
                assetManagerInstance.RefreshAssets();
                EmitSignal(SignalName.GlobalAssetPathChangedAndRefreshed);
                statusMessageLabel.Text += "\nAssets reloaded!";
            } else {
                statusMessageLabel.Text += "\nError: AssetManager not found for asset reload.";
            }
        }
    }

    private void _OnCloseSettingsButtonPressed()
    {
        this.Hide();
    }
}
