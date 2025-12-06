using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ShaderHelper = TheHydrolysist.Common.Helpers.ShaderHelper;

namespace TheHydrolysist.Content.Projectiles;

public class SmallBubble : ModProjectile
{
    private Effect shader;
    private float seed;

    public override string GlowTexture => "TheHydrolysist/Content/Projectiles/SmallBubble_e";

    public const int GOAL_ALPHA = 40;

    private static readonly Dictionary<int, Color> DustColors = new()
    {
        [1] = Color.LightYellow,
        [2] = Color.LightCyan,
        [3] = Color.LightGoldenrodYellow,
        [4] = Color.LightPink,
        [5] = Color.LightPink,
        [6] = Color.LightSalmon,
    };

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Magic; // Damage class projectile uses
        Projectile.penetrate = 1; // How many hits projectile have to make before it dies. 3 means projectile will die on 3rd enemy. Setting this to 0 will make projectile die instantly
        Projectile.width = Projectile.height = 30; // Size of the projectile in pixels (hitbox only).
        Projectile.scale = 1f; // Keep logical scale at 1; visual 3x handled in PreDraw.
        Projectile.friendly = false; // Can hit enemies?
        Projectile.hostile = true; // Can hit player?
        Projectile.timeLeft = 200; // Time in ticks before projectile dies
        Projectile.light = 0.2f; // How much light projectile provides
        Projectile.ignoreWater = false; // Does the projectile ignore water (doesn't slow down in it)
        Projectile.tileCollide = true; // Does the projectile collide with tiles, like blocks?
        Projectile.alpha = 255; // 255 = Completely transparent

        shader = ShaderHelper.SetUpShimmerShader();
        seed = (float)Random.Shared.NextDouble();
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
                    newColor: DustColors[
                        (int)Math.Floor(Random.Shared.NextSingle() * DustColors.Count) + 1
                    ],
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
            Projectile.scale += 0.003f; // Slowly increase logical scale (will be multiplied when drawing)
            Projectile.velocity +=
                new Vector2(Random.Shared.NextSingle() - .5f, Random.Shared.NextSingle() - .5f)
                * 3f;
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
                    newColor: DustColors[
                        (int)Math.Floor(Random.Shared.NextSingle() * DustColors.Count) + 1
                    ],
                    Scale: scale
                );
                dust.position += velocity * Projectile.Size.Length() / 2;
                dust.noGravity = true;
            }
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        // Ensure projectile texture is loaded
        Main.instance.LoadProjectile(Projectile.type);

        Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
        Rectangle frame = texture.Bounds; // Single frame (not animated)
        Vector2 origin = frame.Size() / 2f;
        Vector2 drawPos = Projectile.Center - Main.screenPosition;

        // Visual scale multiplier of 3 applied on top of logical scale growth.
        float visualScale = Projectile.scale * 3f;

        // Draw glow texture (if available) with same scaling, using alpha factor.
        if (!string.IsNullOrEmpty(GlowTexture))
        {
            var glowTex = ModContent.Request<Texture2D>(GlowTexture).Value;
            Rectangle glowFrame = glowTex.Bounds;
            Vector2 glowOrigin = glowFrame.Size() / 2f;
            Main.EntitySpriteDraw(
                glowTex,
                drawPos,
                glowFrame,
                Color.White * ((255 - Projectile.alpha) / 255f),
                Projectile.rotation,
                glowOrigin,
                visualScale,
                SpriteEffects.None,
                0
            );
        }

        // Draw base sprite centered.
        ShaderHelper.DrawShimmerShader(
            shader,
            texture,
            drawPos,
            frame,
            lightColor * ((255 - Projectile.alpha) / 255f),
            Projectile.rotation,
            origin,
            visualScale,
            SpriteEffects.None,
            0,
            seed * 1000f
        );

        // We've manually drawn; skip default drawing.
        return false;
    }
}
