using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheHydrolysist.Common.Systems;

public class DebugOverlaySystem : ModSystem
{
    public static bool Enabled;
    public static bool ShowTileGrid;

    private enum ItemKind
    {
        Line,
        RectOutline,
        RectFill,
        Text,
    }

    private struct DrawItem
    {
        public ItemKind Kind;
        public Vector2 A;
        public Vector2 B;
        public Rectangle Rect;
        public string Text;
        public Color Color;
        public float Thickness;
        public int Ticks; // remaining frames to display
    }

    private struct TileHighlight
    {
        public Point Tile;
        public Color Color;
        public int Ticks;
    }

    private static readonly List<DrawItem> _items = new();
    private static readonly List<TileHighlight> _tileHighlights = new();

    // Public API: schedule world-space drawings (duration in ticks)
    public static void DrawLine(
        Vector2 worldA,
        Vector2 worldB,
        Color color,
        int durationTicks = 60,
        float thickness = 2f
    ) =>
        _items.Add(
            new DrawItem
            {
                Kind = ItemKind.Line,
                A = worldA,
                B = worldB,
                Color = color,
                Thickness = thickness,
                Ticks = durationTicks,
            }
        );

    public static void DrawRect(
        Rectangle worldRect,
        Color color,
        int durationTicks = 60,
        float thickness = 1f
    ) =>
        _items.Add(
            new DrawItem
            {
                Kind = ItemKind.RectOutline,
                Rect = worldRect,
                Color = color,
                Thickness = thickness,
                Ticks = durationTicks,
            }
        );

    public static void FillRect(Rectangle worldRect, Color color, int durationTicks = 60) =>
        _items.Add(
            new DrawItem
            {
                Kind = ItemKind.RectFill,
                Rect = worldRect,
                Color = color,
                Ticks = durationTicks,
            }
        );

    public static void DrawText(
        Vector2 worldPos,
        string text,
        Color color,
        int durationTicks = 60
    ) =>
        _items.Add(
            new DrawItem
            {
                Kind = ItemKind.Text,
                A = worldPos,
                Text = text,
                Color = color,
                Ticks = durationTicks,
            }
        );

    // Public API: highlight specific tiles for a duration
    public static void HighlightTile(Point tilePos, Color? color = null, int durationTicks = 60) =>
        _tileHighlights.Add(
            new TileHighlight
            {
                Tile = tilePos,
                Color = color ?? new Color(255, 255, 0, 120),
                Ticks = durationTicks,
            }
        );

    public static void HighlightTiles(
        IEnumerable<Point> tiles,
        Color? color = null,
        int durationTicks = 60
    )
    {
        Color c = color ?? new Color(255, 255, 0, 120);
        foreach (var t in tiles)
            _tileHighlights.Add(
                new TileHighlight
                {
                    Tile = t,
                    Color = c,
                    Ticks = durationTicks,
                }
            );
    }

    // Public API: clear all scheduled drawings and highlights
    public static void ClearAll()
    {
        _items.Clear();
        _tileHighlights.Clear();
    }

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (!Enabled)
            return;

        Player p = Main.LocalPlayer;
        if (p == null)
            return;
        spriteBatch.End();
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );
        // World -> screen helper
        Vector2 ToScreen(Vector2 world) => world - Main.screenPosition;

        // 0) Optional tile grid overlay
        if (ShowTileGrid)
            DrawTileGrid(spriteBatch, new Color(255, 255, 255, 10));

        // 1) Player hitbox outline
        var hb = p.Hitbox;
        DrawWorldRect(spriteBatch, hb, Color.LimeGreen, 2f);

        // 2) Next-frame predicted hitbox (simple: current + velocity)
        var next = hb;
        next.Offset(p.velocity.ToPoint());
        DrawWorldRect(spriteBatch, next, Color.Orange, 2f);

        // 3) Velocity vector
        DrawWorldLine(spriteBatch, p.Center, p.Center + p.velocity * 20f, Color.Cyan, 2f);

        // 4) Mark nearby solid tiles
        Point min = new((int)(hb.Left / 16f) - 2, (int)(hb.Top / 16f) - 2);
        Point max = new((int)(hb.Right / 16f) + 2, (int)(hb.Bottom / 16f) + 2);
        for (int x = min.X; x <= max.X; x++)
        {
            for (int y = min.Y; y <= max.Y; y++)
            {
                if (!WorldGen.InWorld(x, y, 10))
                    continue;
                var tile = Framing.GetTileSafely(x, y);
                if (!tile.HasTile)
                    continue;
                bool solid = Main.tileSolid[tile.TileType];
                bool platform = TileID.Sets.Platforms[tile.TileType];
                if (!solid && !platform)
                    continue;
                var rect = new Rectangle(x * 16, y * 16, 16, 16);
                var color = platform ? new Color(0, 120, 255, 90) : new Color(255, 0, 0, 90);
                FillWorldRect(spriteBatch, rect, color);
                DrawWorldRect(spriteBatch, rect, platform ? Color.DeepSkyBlue : Color.Red, 1f);
            }
        }

        // 5) Show SolidCollision result as text
        bool solidCollision = Collision.SolidCollision(p.position, p.width, p.height);
        var textPos = ToScreen(p.TopLeft + new Vector2(0, -20));
        Utils.DrawBorderString(
            spriteBatch,
            solidCollision ? "SOLID" : "FREE",
            textPos,
            solidCollision ? Color.Red : Color.LimeGreen
        );

        // 6) Timed drawing queue
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var it = _items[i];
            switch (it.Kind)
            {
                case ItemKind.Line:
                    DrawWorldLine(
                        spriteBatch,
                        it.A,
                        it.B,
                        it.Color,
                        it.Thickness <= 0 ? 1f : it.Thickness
                    );
                    break;
                case ItemKind.RectOutline:
                    DrawWorldRect(
                        spriteBatch,
                        it.Rect,
                        it.Color,
                        it.Thickness <= 0 ? 1f : it.Thickness
                    );
                    break;
                case ItemKind.RectFill:
                    FillWorldRect(spriteBatch, it.Rect, it.Color);
                    break;
                case ItemKind.Text:
                    Utils.DrawBorderString(
                        spriteBatch,
                        it.Text ?? string.Empty,
                        ToScreen(it.A),
                        it.Color
                    );
                    break;
            }
            if (--it.Ticks <= 0)
                _items.RemoveAt(i);
            else
                _items[i] = it;
        }

        // 7) Tile highlight queue
        for (int i = _tileHighlights.Count - 1; i >= 0; i--)
        {
            var th = _tileHighlights[i];
            var rect = new Rectangle(th.Tile.X * 16, th.Tile.Y * 16, 16, 16);
            FillWorldRect(spriteBatch, rect, th.Color);
            DrawWorldRect(spriteBatch, rect, Color.Black * 0.6f, 1f);
            if (--th.Ticks <= 0)
                _tileHighlights.RemoveAt(i);
            else
                _tileHighlights[i] = th;
        }
        spriteBatch.End();
        spriteBatch.Begin();
    }

    private static void DrawTileGrid(SpriteBatch sb, Color color)
    {
        int screenW = Main.screenWidth;
        int screenH = Main.screenHeight;
        Vector2 screenOriginWorld = Main.screenPosition;

        int startTileX = (int)(screenOriginWorld.X / 16f);
        int startTileY = (int)(screenOriginWorld.Y / 16f);
        int tilesWide = (int)(screenW / 16f) + 2;
        int tilesHigh = (int)(screenH / 16f) + 2;

        // Vertical lines
        for (int x = 0; x <= tilesWide; x++)
        {
            float wx = (startTileX + x) * 16f;
            var a = new Vector2(wx, screenOriginWorld.Y);
            var b = new Vector2(wx, screenOriginWorld.Y + screenH);
            DrawWorldLine(sb, a, b, color, 1f);
        }
        // Horizontal lines
        for (int y = 0; y <= tilesHigh; y++)
        {
            float wy = (startTileY + y) * 16f;
            var a = new Vector2(screenOriginWorld.X, wy);
            var b = new Vector2(screenOriginWorld.X + screenW, wy);
            DrawWorldLine(sb, a, b, color, 1f);
        }
    }

    public static void DrawWorldLine(
        SpriteBatch sb,
        Vector2 worldA,
        Vector2 worldB,
        Color color,
        float thickness = 2f
    )
    {
        Vector2 a = worldA - Main.screenPosition;
        Vector2 b = worldB - Main.screenPosition;
        float length = Vector2.Distance(a, b);
        float rotation = (b - a).ToRotation();
        sb.Draw(
            TextureAssets.MagicPixel.Value,
            a,
            null,
            color,
            rotation,
            Vector2.Zero,
            new Vector2(length, thickness / 1000f),
            SpriteEffects.None,
            0f
        );
    }

    public static void DrawWorldRect(
        SpriteBatch sb,
        Rectangle worldRect,
        Color color,
        float thickness = 1f
    )
    {
        var a = new Vector2(worldRect.Left, worldRect.Top);
        var b = new Vector2(worldRect.Right, worldRect.Top);
        var c = new Vector2(worldRect.Right, worldRect.Bottom);
        var d = new Vector2(worldRect.Left, worldRect.Bottom);
        DrawWorldLine(sb, a, b, color, thickness);
        DrawWorldLine(sb, b, c, color, thickness);
        DrawWorldLine(sb, c, d, color, thickness);
        DrawWorldLine(sb, d, a, color, thickness);
    }

    public static void FillWorldRect(SpriteBatch sb, Rectangle worldRect, Color color)
    {
        var screenRect = new Rectangle(
            worldRect.X - (int)Main.screenPosition.X,
            worldRect.Y - (int)Main.screenPosition.Y,
            worldRect.Width,
            worldRect.Height
        );
        sb.Draw(TextureAssets.MagicPixel.Value, screenRect, color);
    }
}

public class DebugKeybindsSystem : ModSystem
{
    public static ModKeybind ToggleDebug;
    public static ModKeybind ToggleGrid;
    public static ModKeybind ClearDrawings;

    public override void Load()
    {
        ToggleDebug = KeybindLoader.RegisterKeybind(Mod, "Toggle Debug Overlay", "F10");
        ToggleGrid = KeybindLoader.RegisterKeybind(Mod, "Toggle Tile Grid", "F9");
        ClearDrawings = KeybindLoader.RegisterKeybind(Mod, "Clear Debug Drawings", "F8");
    }

    public override void Unload()
    {
        ToggleDebug = null;
        ToggleGrid = null;
        ClearDrawings = null;
    }

    // public override void PostUpdateInput()
    // {
    //     if (ToggleDebug != null && ToggleDebug.JustPressed)
    //     {
    //         DebugOverlaySystem.Enabled = !DebugOverlaySystem.Enabled;
    //         Main.NewText(
    //             $"Debug overlay: {(DebugOverlaySystem.Enabled ? "ON" : "OFF")}",
    //             Color.Yellow
    //         );
    //     }
    //     if (ToggleGrid != null && ToggleGrid.JustPressed)
    //     {
    //         DebugOverlaySystem.ShowTileGrid = !DebugOverlaySystem.ShowTileGrid;
    //         Main.NewText(
    //             $"Tile grid: {(DebugOverlaySystem.ShowTileGrid ? "ON" : "OFF")}",
    //             Color.Yellow
    //         );
    //     }
    //     if (ClearDrawings != null && ClearDrawings.JustPressed)
    //     {
    //         DebugOverlaySystem.ClearAll();
    //         Main.NewText("Cleared debug drawings", Color.Yellow);
    //     }
    // }
}
