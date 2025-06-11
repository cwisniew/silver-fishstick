using Godot;
using System;

public partial class ChatLog : RichTextLabel
{
	private const int MaxLines = 100; // Example limit

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		this.BbcodeEnabled = true;
		this.ScrollFollowing = true; // Ensure this is on, though also set in .tscn
	}

	public void AddMessage(string text, Color? color = null, bool isCombatLog = false)
	{
		string message = text;
		if (isCombatLog)
		{
			message = "[COMBAT] " + message;
		}

		string hexColor = color?.ToHtml(false) ?? Colors.White.ToHtml(false); // Default to white, no alpha for BBCode color

		// Append new message
		// Note: For strict line limiting, managing the BBCode string or Text directly gets complex.
		// A simpler approach for RichTextLabel if performance becomes an issue with huge text
		// would be to periodically clear and re-add a subset of messages, or use a different node.
		// For now, just append. If GetLineCount() is available and works well, that's an option.
		// RichTextLabel doesn't directly expose lines as children to remove.
		// This.Text += ... is not ideal as it replaces. AppendBbcode is correct.

		if (GetLineCount() > MaxLines) // A basic way to attempt to limit lines
		{
			// This is tricky with RichTextLabel's internal handling of BBCode.
			// A robust solution would involve parsing existing BbcodeText or keeping a separate list of messages.
			// For now, we'll just clear and mention this limitation.
			// GD.Print("ChatLog reached max lines. Ideally, oldest messages would be removed.");
			// A very naive way (loses history, but prevents infinite growth):
			// if (GetLineCount() > MaxLines + 20) { Clear(); AddText("Log trimmed...\n"); }
		}

		// Adding timestamp (simple format)
		string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");

		AppendText(timestamp); // AppendText for plain part
		PushColor(Color.FromHtml(hexColor)); // Push color for the message part
		AppendText(message);
		Pop(); // Pop color
		AppendText("\n"); // Newline after the colored message
	}
}
