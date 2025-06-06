# TokenTool.gd
# Main script for the Token Creation Tool.
# Handles UI interactions, image loading (including drag & drop),
# base image manipulation (zoom/pan), overlay selection,
# and exporting final token and portrait images.
extends Control

# --- Node References ---
@onready var base_image_display: TextureRect = $BaseImageDisplay
@onready var overlay_display: TextureRect = $OverlayDisplay
@onready var load_image_button: Button = $ButtonContainer/LoadImageButton
@onready var load_overlay_button: Button = $ButtonContainer/LoadOverlayButton
@onready var export_token_button: Button = $ButtonContainer/ExportTokenButton
@onready var image_file_dialog: FileDialog = $ImageFileDialog
@onready var overlay_file_dialog: FileDialog = $OverlayFileDialog
@onready var export_file_dialog: FileDialog = $ExportFileDialog
@onready var choose_overlay_button: Button = $ButtonContainer/ChooseOverlayButton
@onready var export_portrait_button: Button = $ButtonContainer/ExportPortraitButton
@onready var overlay_library_instance: Control = $OverlayLibraryInstance

#@onready var zoom_slider: HSlider = $ControlsContainer/ZoomSlider
#@onready var pan_x_slider: HSlider = $ControlsContainer/PanXSlider
#@onready var pan_y_slider: HSlider = $ControlsContainer/PanYSlider # Slider controls (alternative to mouse)

# --- Base Image Manipulation Variables ---
var is_panning: bool = false # True if left mouse button is held down for panning
var pan_start_position: Vector2 = Vector2.ZERO # Mouse position when panning started
var pan_start_image_position: Vector2 = Vector2.ZERO # Image position when panning started

const ZOOM_FACTOR_MOUSE: float = 1.1 # Multiplier for mouse wheel zoom
const MIN_ZOOM: float = 0.1 # Minimum scale for base image
const MAX_ZOOM: float = 10.0 # Maximum scale for base image

# --- Export State ---
var current_export_mode: String = "token" # Determines export type: "token" or "portrait"

# Called when the node enters the scene tree for the first time.
# Connects signals from UI elements to their respective handler functions.
func _ready():
    load_image_button.pressed.connect(_on_LoadImage_pressed)
    load_overlay_button.pressed.connect(_on_LoadOverlay_pressed)
    image_file_dialog.file_selected.connect(_on_ImageFileDialog_file_selected)
    overlay_file_dialog.file_selected.connect(_on_OverlayFileDialog_file_selected)

    #zoom_slider.value_changed.connect(_on_ZoomSlider_value_changed)
    #pan_x_slider.value_changed.connect(_on_PanXSlider_value_changed)
    #pan_y_slider.value_changed.connect(_on_PanYSlider_value_changed)

    export_token_button.pressed.connect(_on_ExportToken_pressed)
    export_file_dialog.file_selected.connect(_on_ExportFileDialog_file_selected)

    base_image_display.gui_input.connect(_on_BaseImageDisplay_gui_input)
    choose_overlay_button.pressed.connect(_on_ChooseOverlayButton_pressed)
    overlay_library_instance.overlay_selected.connect(_on_OverlayLibrary_overlay_selected)
    export_portrait_button.pressed.connect(_on_ExportPortrait_pressed)

    # Set initial slider values from BaseImageDisplay (if needed, though default is fine)
    #_on_ZoomSlider_value_changed(zoom_slider.value)
    #_on_PanXSlider_value_changed(pan_x_slider.value) # For slider controls
    #_on_PanYSlider_value_changed(pan_y_slider.value) # For slider controls

# --- UI Button Handlers ---

# Called when "Load Image" button is pressed. Shows the FileDialog for base images.
func _on_LoadImage_pressed():
    image_file_dialog.popup_centered()

# Called when "Load Overlay" button is pressed. Shows the FileDialog for overlay images.
func _on_LoadOverlay_pressed():
    overlay_file_dialog.popup_centered()

# --- Image Loading Logic (Dialogs & Drag/Drop Helpers) ---

# Loads an image from the given path and sets it as the base image.
# Resets scale, position, and pivot for the new image.
func _load_base_image(path: String):
    var image = Image.new()
    var err = image.load(path)
    if err == OK:
        var texture = ImageTexture.create_from_image(image)
        base_image_display.texture = texture
        base_image_display.size = texture.get_size()
        base_image_display.pivot_offset = base_image_display.size / 2
        base_image_display.scale = Vector2.ONE
        base_image_display.position = Vector2.ZERO # Or center it for new images
        print("Base image loaded from: " + path)
    else:
        printerr("Error loading base image: " + str(err))

# Loads an image from the given path and sets it as the overlay image.
# Updates the overlay display size based on the new texture.
func _load_overlay_image(path: String):
    var image = Image.new()
    var err = image.load(path)
    if err == OK:
        var texture = ImageTexture.create_from_image(image)
        overlay_display.texture = texture
        overlay_display.size = texture.get_size()
        print("Overlay image loaded from: " + path)
    else:
        printerr("Error loading overlay image: " + str(err))

# Called when a file is selected in the ImageFileDialog (for base image).
func _on_ImageFileDialog_file_selected(path: String):
    _load_base_image(path)

# Called when a file is selected in the OverlayFileDialog (for overlay image).
func _on_OverlayFileDialog_file_selected(path: String):
    _load_overlay_image(path)
# --- End Image Loading Logic ---

# --- Slider Control Handlers (Alternative to Mouse) ---
# These functions are currently commented out as mouse controls are primary.
# func _on_ZoomSlider_value_changed(value: float):
#func _on_ZoomSlider_value_changed(value: float):
    #if base_image_display.texture: # Only apply if texture exists
        #base_image_display.pivot_offset = base_image_display.size / 2 # Zoom from center
        #base_image_display.scale = Vector2(value, value)

#func _on_PanXSlider_value_changed(value: float):
    #if base_image_display.texture:
        #base_image_display.position.x = value

#func _on_PanYSlider_value_changed(value: float):
    #if base_image_display.texture:
        #base_image_display.position.y = value
# --- End Slider Control Handlers ---

# --- Mouse Input Handling for Base Image (Zoom/Pan) ---

# Handles GUI input events on the BaseImageDisplay node (e.g., mouse clicks, motion).
# Used for implementing mouse-based panning and zooming of the base image.
func _on_BaseImageDisplay_gui_input(event: InputEvent):
    if not base_image_display.texture:
        return

    if event is InputEventMouseButton:
        var mouse_event = event as InputEventMouseButton
        if mouse_event.button_index == MOUSE_BUTTON_LEFT:
            if mouse_event.pressed:
                is_panning = true
                pan_start_position = get_global_mouse_position() # Store initial global mouse pos
                pan_start_image_position = base_image_display.position # Store initial image pos
                get_tree().get_root().set_input_as_handled() # Consume the event to prevent further propagation
            else: # Mouse button released
                is_panning = false
                get_tree().get_root().set_input_as_handled()
        # Mouse Wheel Up: Zoom In
        elif mouse_event.button_index == MOUSE_BUTTON_WHEEL_UP:
            if mouse_event.pressed: # Process only on the initial press of the wheel scroll
                var zoom_point = base_image_display.get_local_mouse_position() # Point to zoom towards (local to TextureRect)
                var current_scale = base_image_display.scale.x
                var new_scale = clamp(current_scale * ZOOM_FACTOR_MOUSE, MIN_ZOOM, MAX_ZOOM)
                if current_scale != new_scale: # Apply zoom if scale changes
                    base_image_display.scale = Vector2(new_scale, new_scale)
                    # Adjust position to keep the zoom_point stationary relative to the screen
                    # The pivot_offset is crucial here; it's the point around which scaling happens.
                    # By default, pivot_offset is (0,0). We set it to image center when loading.
                    # The formula re-calculates the top-left position based on the zoom_point,
                    # the pivot_offset, and the change in scale.
                    base_image_display.position = zoom_point - (zoom_point - base_image_display.pivot_offset) * (new_scale / current_scale) + base_image_display.pivot_offset
                get_tree().get_root().set_input_as_handled()
        # Mouse Wheel Down: Zoom Out
        elif mouse_event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
            if mouse_event.pressed:
                var zoom_point = base_image_display.get_local_mouse_position()
                var current_scale = base_image_display.scale.x
                var new_scale = clamp(current_scale / ZOOM_FACTOR_MOUSE, MIN_ZOOM, MAX_ZOOM)
                if current_scale != new_scale:
                    base_image_display.scale = Vector2(new_scale, new_scale)
                    # Same position adjustment logic as zoom in
                    base_image_display.position = zoom_point - (zoom_point - base_image_display.pivot_offset) * (new_scale / current_scale) + base_image_display.pivot_offset
                get_tree().get_root().set_input_as_handled()

    # Mouse Motion: Pan if currently panning
    elif event is InputEventMouseMotion:
        if is_panning:
            #var mouse_motion_event = event as InputEventMouseMotion # Direct access to event properties not needed here
            var mouse_delta = get_global_mouse_position() - pan_start_position # Difference from start of pan
            base_image_display.position = pan_start_image_position + mouse_delta # Apply delta to initial image position
            get_tree().get_root().set_input_as_handled()
# --- End Mouse Input Handling ---

# --- Export Button Handlers ---

# Called when "Export Token" button is pressed.
# Sets export mode and shows the FileDialog for saving the token.
func _on_ExportToken_pressed():
    if not base_image_display.texture or not overlay_display.texture:
        printerr("Base image or overlay not loaded. Cannot export.")
        return
    export_file_dialog.title = "Save Token As..."
    current_export_mode = "token"
    export_file_dialog.popup_centered()

# Called when "Export Portrait" button is pressed.
# Sets export mode and shows the FileDialog for saving the portrait.
func _on_ExportPortrait_pressed():
    if not base_image_display.texture:
        printerr("Base image not loaded. Cannot export portrait.")
        # Optionally, show a warning dialog to the user
        return

    export_file_dialog.title = "Save Portrait As..."
    current_export_mode = "portrait"
    export_file_dialog.popup_centered()

# Called when a file is selected in the ExportFileDialog (for either token or portrait).
# Dispatches to the appropriate export function based on `current_export_mode`.
func _on_ExportFileDialog_file_selected(save_path: String):
    if current_export_mode == "token":
        if not base_image_display.texture or not overlay_display.texture:
            printerr("Token Export: Base image or overlay not loaded.")
            return
        export_token(save_path)
    elif current_export_mode == "portrait":
        if not base_image_display.texture:
            printerr("Portrait Export: Base image not loaded.")
            return
        export_portrait(save_path)
    else:
        printerr("Unknown export mode: " + current_export_mode)
# --- End Export Button Handlers ---

# --- Export Logic ---

# Exports the final token image by compositing the (transformed) base image
# and the overlay image within a temporary Viewport.
func export_token(save_path: String):
    var capture_viewport = Viewport.new()

    # Ensure overlay texture is loaded to define the capture size.
    if not overlay_display.texture:
        printerr("Overlay texture not available for token export size.")
        return
    capture_viewport.size = overlay_display.size # Capture at the overlay's native resolution.
    capture_viewport.transparent_bg = true # Ensure transparency for PNG.

    # Clone BaseImageDisplay to manipulate its properties for capture without affecting the on-screen version.
    var cloned_base_image = base_image_display.duplicate() as TextureRect
    # Calculate the base image's position relative to the overlay's top-left corner.
    # This ensures the base image is positioned correctly "under" the overlay in the capture.
    cloned_base_image.position = base_image_display.global_position - overlay_display.global_position

    # Clone OverlayDisplay to position it at (0,0) within the capture viewport.
    var cloned_overlay = overlay_display.duplicate() as TextureRect
    cloned_overlay.position = Vector2.ZERO # Overlay is the frame of reference.

    capture_viewport.add_child(cloned_base_image)
    capture_viewport.add_child(cloned_overlay) # Add overlay on top.

    # Temporarily add the viewport to the scene tree to enable rendering.
    add_child(capture_viewport)

    # Wait for two frames to ensure rendering is complete before capturing.
    # One frame might sometimes not be enough for complex scenes or certain updates.
    await get_tree().process_frame
    await get_tree().process_frame

    # Get the rendered image from the viewport's texture.
    var img = capture_viewport.get_texture().get_image()

    # Clean up by removing the temporary viewport from the scene and freeing it.
    remove_child(capture_viewport)
    capture_viewport.queue_free()

    if img:
        var err = img.save_png(save_path)
        if err == OK:
            print("Token saved to: " + save_path)
        else:
            printerr("Error saving token: " + str(err))
    else:
        printerr("Failed to capture image for token from viewport.")

# Exports the current view of the base image as a portrait.
# The dimensions are typically defined by the overlay size if an overlay is loaded,
# otherwise, it uses the unscaled base image size.
func export_portrait(save_path: String):
    var portrait_viewport = Viewport.new()
    var portrait_viewport = Viewport.new()

    # Determine capture size: Use overlay size if available, otherwise base image's unscaled size.
    if overlay_display.texture and overlay_display.size != Vector2.ZERO:
         portrait_viewport.size = overlay_display.size
    else:
         # If base_image_display has non-unit scale, divide its current size by scale to get original pixel size.
         # This aims to capture the image at its native resolution if not scaled, or the equivalent if scaled.
         if base_image_display.scale != Vector2.ONE and base_image_display.scale != Vector2.ZERO :
            portrait_viewport.size = base_image_display.size / base_image_display.scale
         else:
            portrait_viewport.size = base_image_display.size

    portrait_viewport.transparent_bg = true # Typically portraits might have opaque background, but transparency is flexible.
                                           # User can add a ColorRect as first child if opaque BG is needed.

    var cloned_base_image = base_image_display.duplicate() as TextureRect

    # Determine the reference point for positioning the cloned base image.
    # If an overlay is actively used and part of the scene, use its top-left global position.
    # This aligns the portrait capture with how the base image appears under the overlay.
    # Otherwise, use the BaseImageDisplay's direct parent's top-left as a fallback reference.
    var reference_position = Vector2.ZERO
    if overlay_display.texture and overlay_display.is_inside_tree() and overlay_display.get_parent():
         reference_position = overlay_display.global_position
    elif base_image_display.is_inside_tree() and base_image_display.get_parent() is Control:
         reference_position = (base_image_display.get_parent() as Control).global_position
    # If no suitable reference, cloned_base_image.position will effectively be base_image_display.global_position,
    # meaning it's captured relative to the screen's top-left.

    cloned_base_image.position = base_image_display.global_position - reference_position

    portrait_viewport.add_child(cloned_base_image)

    add_child(portrait_viewport)

    await get_tree().process_frame
    await get_tree().process_frame

    var img = portrait_viewport.get_texture().get_image()

    remove_child(portrait_viewport)
    portrait_viewport.queue_free()

    if img:
        var err = img.save_png(save_path)
        if err == OK:
            print("Portrait saved to: " + save_path)
        else:
            printerr("Error saving portrait: " + str(err))
    else:
        printerr("Failed to capture image for portrait from viewport.")
# --- End Export Logic ---

# --- Overlay Library Interaction ---

# Called when "Choose Overlay" button is pressed.
# Toggles visibility of the OverlayLibrary panel and reloads its contents if it becomes visible.
func _on_ChooseOverlayButton_pressed():
    overlay_library_instance.visible = not overlay_library_instance.visible
    if overlay_library_instance.visible:
        # Call the library's reload method to ensure it shows the latest overlays.
        if overlay_library_instance.has_method("reload_overlays"):
            overlay_library_instance.call("reload_overlays")
        # else: printerr("OverlayLibraryInstance does not have reload_overlays method.") # For debugging

# Called when an overlay is selected from the OverlayLibraryInstance.
# Sets the selected texture as the current overlay and hides the library panel.
func _on_OverlayLibrary_overlay_selected(texture: Texture2D):
    overlay_display.texture = texture
    overlay_display.size = texture.get_size() # Update display size to match new overlay.
    overlay_library_instance.visible = false
    print("Overlay selected from library.")
# --- End Overlay Library Interaction ---

# --- Drag and Drop Functions ---

# Determines if the control can accept the dropped data.
# Called by the engine when data is dragged over this control.
# Checks if the data is a file path ending with a supported image extension.
func _can_drop_data(at_position: Vector2, data) -> bool:
    # Data is expected to be a PackedStringArray of file paths from the OS.
    if data is PackedStringArray and data.size() > 0:
        var file_path = data[0] # Process the first file path in the array.
        var file_ext = file_path.get_extension().to_lower()
        if file_ext in ["png", "jpg", "jpeg"]: # Supported extensions.
            return true
    return false

# Processes the dropped data.
# Called by the engine when data is dropped onto this control after _can_drop_data returned true.
# Determines target (base or overlay) based on drop position and file type.
func _drop_data(at_position: Vector2, data) -> void:
    if data is PackedStringArray and data.size() > 0:
        var file_path = data[0]
        var file_ext = file_path.get_extension().to_lower()

        if not (file_ext in ["png", "jpg", "jpeg"]):
            return # Should have been caught by _can_drop_data but double check

        # Determine if dropped on BaseImageDisplay or OverlayDisplay using global mouse position.
        var global_mouse_pos = get_global_mouse_position() # More reliable than at_position for global rect checks.

        # Prioritize overlay if dropped within its bounds AND it's a PNG file (overlays are typically PNGs for transparency).
        if overlay_display.get_global_rect().has_point(global_mouse_pos) and file_ext == "png":
            print("Dropped on Overlay: " + file_path)
            _load_overlay_image(file_path)
        # Otherwise, if dropped on the base image display area.
        elif base_image_display.get_global_rect().has_point(global_mouse_pos):
            print("Dropped on BaseImage: " + file_path)
            _load_base_image(file_path)
        # Fallback: If not specifically on overlay or base image (but still on the main control),
        # or if it's a non-PNG dropped on the overlay area, assign to base image.
        else:
            print("Dropped in general area or non-PNG on overlay area, assigning to BaseImage: " + file_path)
            _load_base_image(file_path)
# --- End Drag and Drop Functions ---
