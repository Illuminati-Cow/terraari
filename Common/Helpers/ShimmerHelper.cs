using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraari.Common.Helpers;

public static class ShimmerHelper
{
    private const bool DebugShimmer = true;

    private static bool IsShimmerAtTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
            return false;

        var t = Framing.GetTileSafely(x, y);
        // Shimmer is a liquid type; ensure there's any liquid present and that it's shimmer.
        return t.LiquidAmount > 0 && t.LiquidType == LiquidID.Shimmer;
    }

    // Return a tile rect (inclusive indices) covered by the entity if its bottom is placed at landingPosition.
    // Note: landingPosition is assumed to be the entity's bottom-center in world pixels.
    private static void ComputeEntityFootprintTiles(
        Entity entity,
        Vector2 landingPosition,
        out int left,
        out int right,
        out int top,
        out int bottom
    )
    {
        // Align in world space
        float halfWidth = entity.width * 0.5f;

        // Bottom-left world coordinate of the entity's hitbox at the "landing" spot
        float worldLeft = landingPosition.X - halfWidth;
        float worldRight = landingPosition.X + halfWidth;
        float worldTop = landingPosition.Y - entity.height;
        float worldBottom = landingPosition.Y;

        // Convert world->tile. Subtract a tiny epsilon on right/bottom so we don't cross into the next tile due to float->int truncation.
        const float epsilon = 0.0001f;
        left = (int)Math.Floor(worldLeft / 16f);
        right = (int)Math.Floor((worldRight - epsilon) / 16f);
        top = (int)Math.Floor(worldTop / 16f);
        bottom = (int)Math.Floor((worldBottom - epsilon) / 16f);

        // Clamp to world bounds
        left = Math.Clamp(left, 0, Main.maxTilesX - 1);
        right = Math.Clamp(right, 0, Main.maxTilesX - 1);
        top = Math.Clamp(top, 0, Main.maxTilesY - 1);
        bottom = Math.Clamp(bottom, 0, Main.maxTilesY - 1);
    }

    // Optional: quick printer
    private static void Log(string msg)
    {
        if (!DebugShimmer)
            return;
        // Print to chat in SP, to server log in MP
        // if (Main.netMode == NetmodeID.SinglePlayer)
        //     Main.NewText(msg);
        // else
        //     Logging.PublicLogger.Info($"[ShimmerHelper] {msg}");
    }

    public static Vector2? FindSpotWithoutShimmer(Entity entity, int expand, bool allowSolidTop)
    {
        if (entity is null)
            return null;
        int startX = (int)entity.Center.X / 16;
        int startY = (int)entity.Center.Y / 16;
        // Validate search origin is in TILE coordinates.
        // If you're passing world coords by mistake, you'll be clamped to an edge and never find anything.
        if (startX < 0 || startY < 0 || startX >= Main.maxTilesX || startY >= Main.maxTilesY)
        {
            Log($"FindSpot: start outside bounds (startX={startX}, startY={startY})");
            return null;
        }

        // A small expansion radius can easily fail if entity is large or area is liquid-heavy.
        expand = Math.Max(0, expand);
        Log(
            $"FindSpot: start=({startX},{startY}) expand={expand} allowSolidTop={allowSolidTop} entity=({entity.width}x{entity.height})"
        );

        // Spiral / diamond search out to 'expand'
        // Order: radius 0..expand, scan perimeter to keep it efficient.
        for (int r = 0; r <= expand; r++)
        {
            // Scan a diamond ring at radius r
            for (int dx = -r; dx <= r; dx++)
            {
                int dyTop = r - Math.Abs(dx);
                int dyBottom = -dyTop;

                // Top point of this column in the ring
                if (
                    TryCandidate(
                        entity,
                        startX + dx,
                        startY + dyTop,
                        allowSolidTop,
                        out var landing
                    )
                )
                    return landing;

                // Bottom point (avoid double-checking when dyTop == 0)
                if (dyTop != 0)
                {
                    if (
                        TryCandidate(
                            entity,
                            startX + dx,
                            startY + dyBottom,
                            allowSolidTop,
                            out landing
                        )
                    )
                        return landing;
                }
            }
        }

        Log("FindSpot: no valid spot found.");
        return null;
    }

    public static bool IsSpotShimmerFree(Entity entity, Vector2 landingPosition, bool allowSolidTop)
    {
        string reason;
        bool ok = IsSpotShimmerFreeWithReason(entity, landingPosition, allowSolidTop, out reason);
        if (!ok)
            Log($"IsSpotShimmerFree: REJECT ({reason}) at landing={landingPosition}");
        return ok;
    }

    // INTERNAL — adds diagnostics without changing the public signature
    private static bool IsSpotShimmerFreeWithReason(
        Entity entity,
        Vector2 landingPosition,
        bool allowSolidTop,
        out string reason
    )
    {
        reason = "";

        if (entity is null)
        {
            reason = "null-entity";
            return false;
        }

        // Compute tile footprint covered by the entity hitbox at the landing spot
        ComputeEntityFootprintTiles(
            entity,
            landingPosition,
            out int left,
            out int right,
            out int top,
            out int bottom
        );

        // Bounds guard
        if (left < 0 || right >= Main.maxTilesX || top < 0 || bottom >= Main.maxTilesY)
        {
            reason = "out-of-bounds";
            return false;
        }

        // 1) Liquid shimmer anywhere inside the entity's rectangle
        for (int x = left; x <= right; x++)
        {
            for (int y = top; y <= bottom; y++)
            {
                if (IsShimmerAtTile(x, y))
                {
                    reason = $"shimmer-in-footprint @({x},{y})";
                    return false;
                }
            }
        }

        // 2) Support check: ensure there is something to stand on (solid or platform if allowed)
        // This avoids "valid but falling straight into shimmer below" false positives.
        bool hasSupport = false;
        int supportY = bottom + 1;
        if (supportY < Main.maxTilesY)
        {
            for (int x = left; x <= right; x++)
            {
                var t = Framing.GetTileSafely(x, supportY);
                bool solid =
                    t.HasUnactuatedTile
                    && Main.tileSolid[t.TileType]
                    && !Main.tileSolidTop[t.TileType];
                bool platform = t.HasUnactuatedTile && Main.tileSolidTop[t.TileType];

                if (solid || (allowSolidTop && platform))
                {
                    hasSupport = true;
                    break;
                }
            }
        }

        if (!hasSupport)
        {
            reason = "no-support";
            return false;
        }

        // 3) Optional: peek immediately below support for shimmer to avoid stepping into it
        // If a platform is allowed, you can still have shimmer above/below it; reject to be safe.
        int belowSupportY = supportY + 1;
        if (belowSupportY < Main.maxTilesY)
        {
            for (int x = left; x <= right; x++)
            {
                if (IsShimmerAtTile(x, belowSupportY))
                {
                    reason = $"shimmer-below-support @({x},{belowSupportY})";
                    return false;
                }
            }
        }

        return true;
    }

    // Try a candidate TILE location (cx, cy) and, if valid, return a world-space landing position.
    private static bool TryCandidate(
        Entity entity,
        int cx,
        int cy,
        bool allowSolidTop,
        out Vector2? landingNullable
    )
    {
        landingNullable = null;

        if (cx < 0 || cy < 0 || cx >= Main.maxTilesX || cy >= Main.maxTilesY)
            return false;

        // Convert a candidate TILE cell into a bottom-center world position for the entity.
        // We snap the entity bottom to the bottom of the candidate tile.
        float worldBottomY = (cy + 1) * 16f;
        float worldCenterX = (cx + 0.5f) * 16f;
        var landing = new Vector2(worldCenterX, worldBottomY);

        if (IsSpotShimmerFreeWithReason(entity, landing, allowSolidTop, out var reason))
        {
            Log($"TryCandidate: ACCEPT at tile=({cx},{cy}) landing={landing}");
            landingNullable = landing;
            return true;
        }

        // Uncomment to see all rejections (very noisy):
        // Log($"TryCandidate: reject ({reason}) at tile=({cx},{cy})");
        return false;
    }
}
