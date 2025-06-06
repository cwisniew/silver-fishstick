# OverlayLibrary.gd
# This script manages a library of overlay images.
# It loads PNG images from a specified folder ("res://overlays"),
# displays them as selectable TextureButtons in a GridContainer,
# and emits a signal when an overlay is chosen.
extends Control

# Signal emitted when an overlay TextureButton is pressed.
# Passes the selected Texture2D as an argument.
signal overlay_selected(texture: Texture2D)

@onready var overlay_grid: GridContainer = $ScrollContainer/OverlayGrid # The grid to hold overlay buttons.

# Define path for overlay images within the project.
const OVERLAYS_DIR = "res://overlays"
# For user-specific overlays, one might use "user://overlays" and ensure the directory is created at runtime.

# Called when the node enters the scene tree for the first time.
# Initializes the library by loading overlays from the defined folder.
func _ready():
    load_overlays_from_folder(OVERLAYS_DIR)

# Scans the specified folder for PNG images and creates TextureButtons for each.
# Adds these buttons to the OverlayGrid.
func load_overlays_from_folder(folder_path: String):
    # Clear any existing overlay buttons from the grid before loading new ones.
    # This is important for reloading or preventing duplicates.
    for child in overlay_grid.get_children():
        overlay_grid.remove_child(child) # Remove from grid first
        child.queue_free() # Then free the child node

    var dir = DirAccess.open(folder_path)
    if dir:
        dir.list_dir_begin() # Start listing directory contents.
        var file_name = dir.get_next()
        while file_name != "": # Loop until no more files are found.
            # Ensure it's a file (not a directory) and ends with ".png".
            if not dir.current_is_dir() and file_name.ends_with(".png"):
                var image_path = folder_path.path_join(file_name) # Construct full path.

                # Load the image file.
                var image = Image.new()
                var err = image.load(image_path)

                if err == OK:
                    # Create a texture from the loaded image.
                    var texture = ImageTexture.create_from_image(image)

                    # Create a new TextureButton to display the overlay.
                    var button = TextureButton.new()
                    button.texture_normal = texture # Set its normal (default) texture.
                    button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED # Maintain aspect ratio.
                    button.custom_minimum_size = Vector2(100, 100) # Ensure buttons have a decent clickable size.
                                                                # Adjust as needed for visual appeal.

                    # Connect the button's pressed signal to _on_TextureButton_pressed.
                    # Use Callable.bind to pass the specific texture of this button to the handler.
                    button.pressed.connect(Callable(self, "_on_TextureButton_pressed").bind(texture))

                    overlay_grid.add_child(button) # Add the button to the grid.
                else:
                    printerr("Failed to load overlay: " + image_path + " Error: " + str(err))
            file_name = dir.get_next() # Move to the next file in the directory.
        dir.list_dir_end() # Finalize directory listing.
    else:
        printerr("Could not open overlays directory: " + folder_path)

# Called when one of the dynamically created TextureButtons is pressed.
# Emits the overlay_selected signal with the texture of the pressed button.
func _on_TextureButton_pressed(texture: Texture2D):
    emit_signal("overlay_selected", texture)
    # The main TokenTool script will handle hiding this panel if needed.

# Public method to allow reloading overlays, e.g., when the panel is shown.
func reload_overlays():
    load_overlays_from_folder(OVERLAYS_DIR)
