using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace Terraari.Content.Projectiles;

public class ShimmerLightning : ModProjectile
{
    public override string Texture => $"{Mod.Name}/Content/Projectiles/{nameof(ShimmerLightning)}";
    public override string GlowTexture =>
        $"{Mod.Name}/Content/Projectiles/{nameof(ShimmerLightning)}_e";

    public float Timer
    {
        get => Projectile.ai[2];
        set => Projectile.ai[2] = value;
    }

    // Configuration constants – adjust to tune lightning behavior
    private const int MaxDirectionAttempts = 100; // Attempts before giving up and stopping
    private const float MinVerticalComponent = -0.02f; // Require Y <= this (mostly upward)
    private const float MaxLateralDisplacement = 40f; // Horizontal extent clamp
    private const int CollisionTrailLength = 20;

    private int seed = -1;
    private float xVelocityOffset;
    private bool FailedToBranch;
    private readonly List<(Vector2 pos, float rot)> oldTransforms = new(100);

    // Derived segment tick length (how long before changing direction)
    private int SegmentTicks => Projectile.extraUpdates * 3;
    private bool IsSegmentReady => Timer >= SegmentTicks;
    private float InitialDirection => Projectile.ai[0];

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = CollisionTrailLength;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Magic;
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.hostile = true;
        Projectile.alpha = 255;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = true;
        Projectile.extraUpdates = 4;
        Projectile.timeLeft = 240 * (Projectile.extraUpdates + 1);
        Projectile.penetrate = 1;
    }

    public override void OnSpawn(IEntitySource source)
    {
        Timer = 0;
    }

    public override void AI()
    {
        if (seed == -1)
            seed = (int)Projectile.ai[1];
        Timer++;

        if (Projectile.velocity == Vector2.Zero)
        {
            if (ShouldKillIfStationary())
            {
                Projectile.Kill();
                return;
            }
            SpawnCollisionSparks();
            SpawnCollisionSmoke();
            return;
        }
        if (Projectile.shimmerWet)
            Main.NewText("Shimmered lightning");
        if (Projectile.owner == Main.myPlayer && Projectile.shimmerWet)
        {
            Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                Projectile.Center,
                -Vector2.UnitY,
                ModContent.ProjectileType<BigBubble>(),
                50,
                4.5f,
                Projectile.owner
            );
            Projectile.Kill();
            return;
        }

        if (Timer % Projectile.extraUpdates == 0)
        {
            if (Projectile.oldPos.Length > 1)
            {
                oldTransforms.Add(new(Projectile.oldPos[0], Projectile.oldRot[0]));
            }
            if (oldTransforms.Count > 100)
            {
                oldTransforms.RemoveAt(0);
            }
        }

        if (!IsSegmentReady)
            return;

        Timer = 0;
        float speed = Projectile.velocity.Length();
        if (!TryChooseNextDirection(speed, out Vector2 dir) || Projectile.velocity == Vector2.Zero)
            return;
        xVelocityOffset += dir.X * (Projectile.extraUpdates + 1) * 2f * speed;
        Projectile.velocity = dir.RotatedBy(InitialDirection + MathF.PI / 2f) * speed;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathF.PI / 2f;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        for (
            int n = 0;
            n < Projectile.oldPos.Length
                && (Projectile.oldPos[n].X != 0f || Projectile.oldPos[n].Y != 0f);
            n++
        )
        {
            projHitbox.X = (int)Projectile.oldPos[n].X;
            projHitbox.Y = (int)Projectile.oldPos[n].Y;
            if (projHitbox.Intersects(targetHitbox))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Allows you to determine what happens when this projectile collides with a tile. OldVelocity is the velocity before tile collision. The velocity that takes tile collision into account can be found with Projectile.velocity. Return true to allow the vanilla tile collision code to take place (which normally kills the projectile). Returns true by default.
    /// <para /> Called on local, server, and remote clients.
    /// </summary>
    /// <param name="oldVelocity">The velocity of the projectile upon collision.</param>
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        // Despawn the projectile on tile collision only if it failed to branch
        if (FailedToBranch)
            return true;
        Projectile.position += Projectile.velocity;
        Projectile.velocity = Vector2.Zero;

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (timeLeft != 0)
            return;
        for (int i = 0; i < 4; i++)
        {
            SpawnCollisionSparks(true);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D insideTexture = TextureAssets.Projectile[Projectile.type].Value;
        Texture2D outsideTexture = ModContent.Request<Texture2D>(GlowTexture).Value;
        float size = 10f;
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.Default,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );
        int age = 1;
        foreach ((Vector2 pos, float rot) in oldTransforms)
        {
            Rectangle frame = new(
                (int)(pos.X - Main.screenPosition.X - size / 2),
                (int)(pos.Y - Main.screenPosition.Y - size / 2),
                (int)size,
                (int)size
            );
            int trailLength = CollisionTrailLength / Projectile.extraUpdates;
            float alpha = Utils.Remap(
                age,
                oldTransforms.Count - trailLength * 10f,
                oldTransforms.Count,
                0,
                1
            );
            Main.spriteBatch.Draw(
                outsideTexture,
                frame,
                null,
                Main.quickAlpha(Color.White, alpha / 2)
            );
            alpha = Utils.Remap(age, oldTransforms.Count - trailLength, oldTransforms.Count, 0, 1);
            Main.spriteBatch.Draw(outsideTexture, frame, null, Main.quickAlpha(Color.White, alpha));
            Main.spriteBatch.Draw(insideTexture, frame, null, Main.quickAlpha(Color.White, alpha));
            age++;
        }
        Main.spriteBatch.End();
        Main.spriteBatch.Begin();
        return false;
    }

    private void SpawnCoreDust()
    {
        var d = Dust.NewDustPerfect(
            Projectile.Center,
            DustID.ShimmerSpark,
            Vector2.Zero,
            0,
            default,
            1.5f
        );
        d.noGravity = true;
        Lighting.AddLight(Projectile.Center, 1f, 1f, 1f);
    }

    private bool ShouldKillIfStationary()
    {
        if (!IsSegmentReady)
            return false;
        Timer = 0;
        for (int i = 1; i < Projectile.oldPos.Length; i++)
        {
            if (Projectile.oldPos[i] != Projectile.oldPos[0])
                return false; // Has moved previously – keep alive
        }
        return true;
    }

    private void SpawnCollisionSparks(bool forceSpawn = false)
    {
        if (Main.rand.Next(Projectile.extraUpdates) != 0 && !forceSpawn)
            return;
        for (int i = 0; i < 2; i++)
        {
            float dustDirection =
                Projectile.rotation + (Main.rand.NextBool() ? -1f : 1f) * MathF.PI / 2f;
            float dustSpeed = (float)Main.rand.NextDouble() * 0.8f + 1f;
            Vector2 vel = new(
                MathF.Cos(dustDirection) * dustSpeed,
                MathF.Sin(dustDirection) * dustSpeed
            );
            var dust = Dust.NewDustDirect(
                Projectile.Center,
                10,
                10,
                DustID.ShimmerSpark,
                vel.X,
                vel.Y
            );
            dust.noGravity = true;
            dust.scale = 1.2f;
        }
    }

    private void SpawnCollisionSmoke()
    {
        if (Main.rand.NextBool(5))
            return;
        Vector2 spawnDirection =
            Projectile.velocity.RotatedBy(MathF.PI / 2f)
            * ((float)Main.rand.NextDouble() - 0.5f)
            * Projectile.width;
        var smoke = Dust.NewDustDirect(
            Projectile.Center + spawnDirection - Vector2.One * 4f,
            8,
            8,
            DustID.Smoke,
            0f,
            0f,
            100,
            default,
            1.5f
        );
        smoke.velocity *= 0.5f;
        smoke.velocity.Y = -Math.Abs(smoke.velocity.Y);
    }

    private bool TryChooseNextDirection(float speed, out Vector2 chosen)
    {
        chosen = -Vector2.UnitY;
        var rng = new UnifiedRandom(seed);
        for (int attempt = 0; attempt < MaxDirectionAttempts; attempt++)
        {
            int random = rng.Next();
            seed = random;
            float angle = (random % 100) / 100f * MathF.Tau;
            Vector2 candidate = angle.ToRotationVector2();
            if (candidate.Y > 0f)
                candidate.Y *= -1f; // Force upward tendency
            float projectedLateral =
                candidate.X * (Projectile.extraUpdates + 1) * 2f * speed + xVelocityOffset;
            if (
                candidate.Y <= MinVerticalComponent
                && projectedLateral is > -MaxLateralDisplacement and < MaxLateralDisplacement
            )
            {
                chosen = candidate;
                Projectile.netUpdate = true;
                return true;
            }
        }
        // Failed – stop movement
        Projectile.velocity = Vector2.Zero;
        FailedToBranch = true;
        Projectile.netUpdate = true;
        return false;
    }
}
