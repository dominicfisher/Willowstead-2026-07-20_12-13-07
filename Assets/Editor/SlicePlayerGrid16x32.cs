#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click slicer for Assets/Sprites/Player.png into a 16x32 grid.
/// - Sets Multiple sprite mode
/// - PPU = 16, FilterMode = Point, MeshType = FullRect
/// - Bottom-center pivots (0.5, 0)
/// - Names: Player_r{row}_c{col}
///
/// Use via Unity menu: Tools/Willowstead/Slice Player 16x32 Grid
/// </summary>
public static class SlicePlayerGrid16x32
{
    private const string PlayerPath = "Assets/Sprites/Player.png";
    private const int CellW = 16;
    private const int CellH = 32;
    private const float PPU = 16f;

    [MenuItem("Tools/Willowstead/Slice Player 16x32 Grid")] 
    public static void Slice()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerPath);
        if (tex == null)
        {
            EditorUtility.DisplayDialog("Slice Player", $"Could not find texture at {PlayerPath}", "OK");
            return;
        }

        var importer = AssetImporter.GetAtPath(PlayerPath) as TextureImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Slice Player", "Could not get TextureImporter", "OK");
            return;
        }

        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PPU;
        importer.filterMode = FilterMode.Point;
        importer.textureType = TextureImporterType.Sprite;
        #if !UNITY_6000_0_OR_NEWER
        importer.spriteMeshType = SpriteMeshType.FullRect;
        #endif
        importer.mipmapEnabled = false;

        int texW = tex.width;
        int texH = tex.height;
        var metas = new List<SpriteMetaData>();

        int rows = texH / CellH;
        int cols = texW / CellW;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int x = c * CellW;
                int y = r * CellH; // Unity importer rect.y is from bottom
                var meta = new SpriteMetaData
                {
                    name = $"Player_r{r}_c{c}",
                    rect = new Rect(x, y, CellW, CellH),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0f), // bottom-center
                    border = new Vector4(0, 0, 0, 0)
                };
                metas.Add(meta);
            }
        }

        importer.spritesheet = metas.ToArray();

        // Apply and reimport
        EditorUtility.SetDirty(importer);
        try
        {
            AssetDatabase.StartAssetEditing();
            AssetDatabase.ImportAsset(PlayerPath, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        EditorUtility.DisplayDialog("Slice Player", $"Sliced {rows * cols} sprites ({cols}x{rows}) at {CellW}x{CellH}.", "OK");
    }
}
#endif
