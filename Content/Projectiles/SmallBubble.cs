using System;
using System.Collections.Generic;
using System.Security.Principal;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace Terraari.Content.Projectiles;

public class SmallBubble : ModProjectile
{
    // public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Bubble}";
    public override string GlowTexture => "Terraari/Content/Projectiles/SmallBubble_e";

    public const int GOAL_ALPHA = 40;

    private static readonly Dictionary<int, Color> DustColors = new()
    {
        [1] = Color.LightYellow,
        [2] = Color.LightCyan,
        [3] = Color.LightGoldenrodYellow,
        [4] = Color.LightPink,
        [5] = Color.LightPink,
        [6] = Color.LightSalmon
    };

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
    }


    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Magic; // Damage class projectile uses
        Projectile.penetrate = 1; // How many hits projectile have to make before it dies. 3 means projectile will die on 3rd enemy. Setting this to 0 will make projectile die instantly
        Projectile.width = Projectile.height = 1; // Size of the projectile in pixels.
        Projectile.scale = (Random.Shared.NextSingle() * 0.7f) + 2.5f;
        Projectile.friendly = false; // Can hit enemies?
        Projectile.hostile = true; // Can hit player?
        Projectile.timeLeft = ((int)(Random.Shared.NextSingle() * 30)) + 180; // Time in ticks before projectile dies
        Projectile.light = 0.0f; // How much light projectile provides
        Projectile.ignoreWater = false; // Does the projectile ignore water (doesn't slow down in it)
        Projectile.tileCollide = true; // Does the projectile collide with tiles, like blocks?
        Projectile.alpha = 255; // 255 = Completely transparent
    }

    private static Vector2 RandomInUnitCircle()
    {
        Vector2 vector = new(Random.Shared.NextSingle() - .5f, Random.Shared.NextSingle() - .5f);
        return vector.SafeNormalize(Vector2.Zero) * Random.Shared.NextSingle();
    }

    public override void AI()
    { // This hook updates every tick
        if (Projectile.shimmerWet)
        {
            // Force upwards motion when in shimmer to bounce off the surface
            Projectile.velocity = Projectile.oldVelocity;
            Projectile.velocity.Y = -Math.Abs(Projectile.velocity.Y);
        }
        else if (Projectile.wet) 
        {
            // Destroy bubbles in all other fluids
            Projectile.Kill();
            return;
        }
        if (Main.netMode != NetmodeID.Server) // Do not spawn dust on server!
        {
            if (Random.Shared.NextSingle() < 0.05)
            {
                int scale = (255 - Projectile.alpha) / (255 - GOAL_ALPHA);
                Dust dust = Dust.NewDustPerfect(
                    Position: Projectile.Center + Projectile.Size / 2,
                    Type: DustID.ShimmerSpark,
                    Velocity: Vector2.Zero,
                    newColor: DustColors[(int)Math.Floor(Random.Shared.NextSingle() * DustColors.Count) + 1],
                    Scale: scale
                );
                dust.velocity = Projectile.velocity / 4f;
                dust.position += RandomInUnitCircle() * Projectile.Size.Length() / 2;
                dust.noGravity = true;
                dust.fadeIn = -1f;
            }
        }
        // Randomly variate in velocity
        if (Random.Shared.NextSingle() < 0.1f)
        {
            Projectile.scale += 0.003f; // Slowly increase in size
            Projectile.velocity += new Vector2(Random.Shared.NextSingle()-.5f, Random.Shared.NextSingle()-.5f)*3f;
        }
        Projectile.velocity *= 0.995f; // Slowly drop in velocity
        Projectile.alpha = Math.Max(GOAL_ALPHA, Projectile.alpha - 15);
        Projectile.light = Math.Min(0.3f, Projectile.light + 0.04f);
    }

    public override void OnKill(int timeLeft)
    {
        if (Main.netMode != NetmodeID.Server) // Do not spawn dust on server!
        {
            const int numDust = 20;
            for (int i = 0; i < numDust; i++)
            {
                Vector2 velocity = Vector2.One.RotatedBy(
                    MathHelper.ToRadians(360 / (float)numDust * i)
                ); // Circular velocity
                int scale = (255 - Projectile.alpha) / (255 - GOAL_ALPHA);
                Dust dust = Dust.NewDustPerfect(
                    Position: Projectile.Center + Projectile.Size / 2,
                    Type: DustID.ShimmerSpark,
                    Velocity: velocity,
                    newColor: DustColors[(int)Math.Floor(Random.Shared.NextSingle() * DustColors.Count) + 1],
                    Scale: scale
                );
                dust.position += velocity * Projectile.Size.Length() / 2;
                dust.noGravity = true;
            }
        }
    }
}
