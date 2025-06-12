using Godot;

public class AssetMetadata
{
	public string AssetName { get; set; }
	public string ImageFileName { get; set; } // Just the filename, not the full path
	public string Category { get; set; } // Store as string from OptionButton
	public bool IsSpritesheet { get; set; }
	public int TileWidth { get; set; }
	public int TileHeight { get; set; }
	public int SeparationX { get; set; }
	public int SeparationY { get; set; }
	public int MarginX { get; set; }
	public int MarginY { get; set; }
	public string TileSetResourcePath { get; set; } // Only if IsSpritesheet is true, otherwise null or empty

	public static AssetMetadata FromJsonDictionary(Godot.Collections.Dictionary data)
	{
		AssetMetadata meta = new AssetMetadata();
		if (data == null) return meta; // Return empty meta if data is null

		meta.AssetName = data.GetValueOrDefault("AssetName", "").ToString();
		meta.ImageFileName = data.GetValueOrDefault("ImageFileName", "").ToString();
		meta.Category = data.GetValueOrDefault("Category", AssetManager.AssetCategory.General.ToString()).ToString();

		Variant isSpritesheetVariant;
		data.TryGetValue("IsSpritesheet", out isSpritesheetVariant);
		meta.IsSpritesheet = isSpritesheetVariant.VariantType == Variant.Type.Bool ? (bool)isSpritesheetVariant : false;

		if (meta.IsSpritesheet)
		{
			Variant tileWidthVariant;
			data.TryGetValue("TileWidth", out tileWidthVariant);
			meta.TileWidth = tileWidthVariant.VariantType == Variant.Type.Int64 ? (int)tileWidthVariant : 16; // Default 16

			Variant tileHeightVariant;
			data.TryGetValue("TileHeight", out tileHeightVariant);
			meta.TileHeight = tileHeightVariant.VariantType == Variant.Type.Int64 ? (int)tileHeightVariant : 16; // Default 16

			Variant separationXVariant;
			data.TryGetValue("SeparationX", out separationXVariant);
			meta.SeparationX = separationXVariant.VariantType == Variant.Type.Int64 ? (int)separationXVariant : 0;

			Variant separationYVariant;
			data.TryGetValue("SeparationY", out separationYVariant);
			meta.SeparationY = separationYVariant.VariantType == Variant.Type.Int64 ? (int)separationYVariant : 0;

			Variant marginXVariant;
			data.TryGetValue("MarginX", out marginXVariant);
			meta.MarginX = marginXVariant.VariantType == Variant.Type.Int64 ? (int)marginXVariant : 0;

			Variant marginYVariant;
			data.TryGetValue("MarginY", out marginYVariant);
			meta.MarginY = marginYVariant.VariantType == Variant.Type.Int64 ? (int)marginYVariant : 0;

			meta.TileSetResourcePath = data.GetValueOrDefault("TileSetResourcePath", "").ToString();
		}
		return meta;
	}
}
