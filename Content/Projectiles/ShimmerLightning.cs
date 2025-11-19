using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace Terraari.Content.Projectiles;

public class ShimmerLightning : ModProjectile
{
    public override string Texture => $"{Mod.Name}/Content/Projectiles/{nameof(ShimmerLightning)}";
    public float Timer
    {
        get => Projectile.ai[2];
        set => Projectile.ai[2] = value;
    }

    // Configuration constants – adjust to tune lightning behavior
    private const int MaxDirectionAttempts = 100; // Attempts before giving up and stopping
    private const float MinVerticalComponent = -0.02f; // Require Y <= this (mostly upward)
    private const float MaxLateralDisplacement = 40f; // Horizontal extent clamp

    // Derived segment tick length (how long before changing direction)
    private int SegmentTicks => Projectile.extraUpdates * 2;

    private bool IsSegmentReady => Timer >= SegmentTicks;

    private int seed = -1;
    private float xVelocityOffset;
    private bool FailedToBranch;

    private float InitialDirection => Projectile.ai[0];

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

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
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
        Projectile.timeLeft = 120 * (Projectile.extraUpdates + 1);
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
        if (Timer % 2 == 0)
            SpawnCoreDust();

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

    public override void PostAI()
    {
        if (Projectile.velocity != Vector2.Zero)
            return;
        float angle =
            Projectile.rotation
            + (float)Math.PI / 2f
            + (Main.rand.NextBool(2) ? -1f : 1f) * ((float)Math.PI / 2f);
        float speed = (float)Main.rand.NextDouble() * 2f + 2f;
        var velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
        var dust = Dust.NewDustDirect(
            Projectile.oldPos[^1],
            0,
            0,
            DustID.ShimmerSplash,
            velocity.X,
            velocity.Y
        );
        dust.noGravity = true;
        dust.scale = 1.7f;
    }

    public override void OnKill(int timeLeft)
    {
        Main.NewText($"Time Left: {timeLeft}");
        if (timeLeft != 0)
            return;
        for (int i = 0; i < 4; i++)
        {
            SpawnCollisionSparks(true);
        }
    }
}
