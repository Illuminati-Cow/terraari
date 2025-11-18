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

    /// <summary>
    /// Gets called when your projectiles spawns in world.
    /// <para /> Called on the client or server spawning the projectile via Projectile.NewProjectile.
    /// </summary>
    public override void OnSpawn(IEntitySource source)
    {
        Timer = 0;
        Projectile.localAI[1] = 0f;
    }

    public override void AI()
    {
        Timer++;
        if (Timer % 2 == 0)
        {
            Color[] colors = [new Color(1, 1, 1)];
            var color = new Color(1, 1, 1);
            var debugDust = Dust.NewDustPerfect(
                Projectile.Center,
                DustID.Electric,
                Vector2.Zero,
                0,
                default,
                1.5f
            );
            debugDust.noGravity = true;
            Lighting.AddLight(Projectile.Center, 0.3f, 0.45f, 0.5f);
        }
        // What is this check for?
        if (Projectile.velocity == Vector2.Zero)
        {
            if (Timer >= Projectile.extraUpdates * 2)
            {
                Timer = 0;
                bool shouldKill = true;
                for (int i = 1; i < Projectile.oldPos.Length; i++)
                {
                    // If velocity is zero, and we have moved in the past, then don't kill the projectile
                    if (Projectile.oldPos[i] != Projectile.oldPos[0])
                    {
                        shouldKill = false;
                    }
                }

                if (shouldKill)
                {
                    Projectile.Kill();
                    Main.NewText("Killing Projectile");
                    return;
                }
            }

            // Spawn lightning dust along the path, with a guarantee to spawn it at the end
            if (Main.rand.Next(Projectile.extraUpdates) != 0)
                return;
            Main.NewText("Spawning Dust");
            for (int i = 0; i < 2; i++)
            {
                float dustDirection =
                    Projectile.rotation
                    + (Main.rand.NextBool(2) ? -1f : 1f) * ((float)Math.PI / 2f);
                float dustSpeed = (float)Main.rand.NextDouble() * 0.8f + 1f;
                var dustVelocity = new Vector2(
                    (float)Math.Cos(dustDirection) * dustSpeed,
                    (float)Math.Sin(dustDirection) * dustSpeed
                );
                var dust = Dust.NewDustDirect(
                    Projectile.Center,
                    10,
                    10,
                    DustID.Electric,
                    dustVelocity.X,
                    dustVelocity.Y
                );
                dust.noGravity = true;
                dust.scale = 1.2f;
            }

            // Spawn smoke particles with a fixed chance
            if (Main.rand.NextBool(5))
                return;

            Vector2 spawnDirection =
                Projectile.velocity.RotatedBy(1.5707963705062866)
                * ((float)Main.rand.NextDouble() - 0.5f)
                * Projectile.width;
            var smokeDust = Dust.NewDustDirect(
                Projectile.Center + spawnDirection - Vector2.One * 4f,
                8,
                8,
                DustID.Smoke,
                0f,
                0f,
                100,
                default(Color),
                1.5f
            );
            smokeDust.velocity *= 0.5f;
            smokeDust.velocity.Y = 0f - Math.Abs(smokeDust.velocity.Y);
        }
        else
        {
            if (Timer < Projectile.extraUpdates * 2)
            {
                return;
            }
            Timer = 0;
            float speed = Projectile.velocity.Length();
            var unifiedRandom = new UnifiedRandom((int)Projectile.ai[1]);
            int step = 0;
            Vector2 direction = -Vector2.UnitY;
            while (true)
            {
                int random = unifiedRandom.Next();
                Projectile.ai[1] = random;
                random %= 100;
                float randomAngle = random / 100f * ((float)Math.PI * 2f);
                Vector2 rotation = randomAngle.ToRotationVector2();
                // Flip vertical direction
                if (rotation.Y > 0f)
                {
                    rotation.Y *= -1f;
                }

                if (
                    rotation.Y > -0.02f
                    || rotation.X * (Projectile.extraUpdates + 1) * 2f * speed
                        + Projectile.localAI[0]
                        > 40f
                    || rotation.X * (Projectile.extraUpdates + 1) * 2f * speed
                        + Projectile.localAI[0]
                        < -40f
                )
                {
                    if (step++ >= 100)
                    {
                        Projectile.velocity = Vector2.Zero;
                        Projectile.localAI[1] = 1f;
                        break;
                    }
                    continue;
                }
                direction = rotation;
                break;
            }

            if (Projectile.velocity == Vector2.Zero)
                return;
            // If somehow velocity is not zero (loop-end state), then set the velocity
            Projectile.localAI[0] +=
                direction.X * (float)(Projectile.extraUpdates + 1) * 2f * speed;
            Projectile.velocity =
                direction.RotatedBy(Projectile.ai[0] + (float)Math.PI / 2f) * speed;
            Projectile.rotation = Projectile.velocity.ToRotation() + (float)Math.PI / 2f;
        }
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
        if (!(Projectile.localAI[1] < 1f))
            return true;
        Projectile.localAI[1] += 2f;
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
            DustID.Vortex,
            velocity.X,
            velocity.Y
        );
        dust.noGravity = true;
        dust.scale = 1.7f;
    }

    public override void OnKill(int timeLeft) { }
}
