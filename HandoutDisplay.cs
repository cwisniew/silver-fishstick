using Godot;
using System;

public partial class HandoutDisplay : PanelContainer
{
	[Export] private TextureRect handoutTextureRect;
	[Export] private Button closeButton;
	private SoundManager _soundManager;

	public override void _Ready()
	{
		if (closeButton == null)
		{
			GD.PrintErr("HandoutDisplay: CloseButton is not assigned.");
		}
		else
		{
			closeButton.Pressed += OnCloseButtonPressed;
		}

		if (handoutTextureRect == null)
		{
			GD.PrintErr("HandoutDisplay: HandoutTextureRect is not assigned.");
		}

		// Ensure it's not visible on load if instanced from scene tree directly (though usually hidden by MainScene)
		// Visible = false;
		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager");
		if (_soundManager == null) GD.PrintErr("HandoutDisplay: SoundManager Autoload not found!");
	}

	public void DisplayHandout(Texture2D imageTexture)
	{
		if (handoutTextureRect != null)
		{
			handoutTextureRect.Texture = imageTexture;
		}
		else
		{
			GD.PrintErr("HandoutDisplay: HandoutTextureRect is null, cannot display image.");
			return; // Don't show if no way to display image
		}
		Visible = true; // Make the panel visible
		GrabFocus(); // Optional: focus the panel or close button
		closeButton?.GrabFocus();
	}

	private void OnCloseButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		QueueFree(); // Remove the handout display from the scene
	}

	// Optional: Handle Escape key to close
	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode == Key.Escape)
			{
				OnCloseButtonPressed();
				GetViewport().SetInputAsHandled(); // Consume Escape key
			}
		}
	}
}
