using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraari.Content.Projectiles;

public class BigBubble : ModProjectile
{
    private static Effect bubbleEffect;
    private static Texture2D circleTexture;

    public override string Texture => "Terraria/Images/Projectile_0"; // We will use no texture

    private Player HomingTarget
    {
        get => Projectile.ai[0] == 0 ? null : Main.player[(int)Projectile.ai[0]];
        set => Projectile.ai[0] = value?.whoAmI ?? 0;
    }

    public ref float DelayTimer => ref Projectile.ai[1];

    public override void Load()
    {
        if (Main.dedServ)
            return;
        bubbleEffect = ModContent
            .Request<Effect>("terraari/Assets/Effects/BubbleWarp", AssetRequestMode.ImmediateLoad)
            .Value;
    }

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.CanHitPastShimmer[Projectile.type] = true;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Magic; // Damage class projectile uses
        Projectile.scale = 1f; // Projectile scale multiplier
        Projectile.penetrate = 3; // How many hits projectile have to make before it dies. 3 means projectile will die on 3rd enemy. Setting this to 0 will make projectile die instantly
        Projectile.aiStyle = 0; // AI style of a projectile. 0 is default bullet AI
        Projectile.width = Projectile.height = 10; // Hitbox of projectile in pixels
        Projectile.friendly = false; // Can hit enemies?
        Projectile.hostile = true; // Can hit player?
        Projectile.timeLeft = 90; // Time in ticks before projectile dies
        Projectile.light = 0.3f; // How much light projectile provides
        Projectile.ignoreWater = true; // Does the projectile ignore water (doesn't slow down in it)
        Projectile.tileCollide = false; // Does the projectile collide with tiles, like blocks?
        Projectile.alpha = 255; // Completely transparent
    }

    public void GenerateCircleTexture()
    {
        // Create circle texture on first use (lazy initialization)
        if (circleTexture == null)
        {
            int size = 64;
            Color[] colorData = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float dist = Vector2.Distance(pos, center);

                    if (dist <= size / 2f)
                        colorData[y * size + x] = Color.White;
                    else
                        colorData[y * size + x] = Color.Transparent;
                }
            }

            circleTexture = new Texture2D(Main.graphics.GraphicsDevice, size, size);
            circleTexture.SetData(colorData);
        }
    }

    public override void PostDraw(Color lightColor)
    {
        if (Main.dedServ || bubbleEffect == null)
            return;

        GenerateCircleTexture();

        // Capture current screen as texture for warping
        var screenTarget = Main.screenTarget;

        // Set shader params
        bubbleEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
        bubbleEffect.Parameters["uImageScreen"]?.SetValue(screenTarget);

        // Apply shader
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.NonPremultiplied,
            SamplerState.LinearClamp,
            DepthStencilState.Default,
            RasterizerState.CullNone,
            bubbleEffect,
            Main.GameViewMatrix.TransformationMatrix
        );

        float size = 50f;
        Main.spriteBatch.Draw(
            circleTexture,
            new Rectangle(
                (int)(Projectile.Center.X - Main.screenPosition.X - size / 2),
                (int)(Projectile.Center.Y - Main.screenPosition.Y - size / 2),
                (int)size,
                (int)size
            ),
            null,
            Color.White
        );

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.Default,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );
    }

    public override void Unload()
    {
        bubbleEffect = null;
        if (!Main.dedServ)
        {
            try
            {
                circleTexture?.Dispose();
            }
            catch (Exception _)
            {
                // do not allow exception in unload, else game will crash
            }
        }
        circleTexture = null;
    }

    public override void AI()
    {
        const float maxDetectRadius = 400f;
        if (Main.netMode != NetmodeID.Server)
        {
            Dust dust = Dust.NewDustPerfect(
                Position: Projectile.Center,
                Type: DustID.Electric,
                Velocity: Vector2.Zero,
                Alpha: 100,
                newColor: Color.White,
                Scale: 0.9f
            );
            dust.noGravity = true;
            dust.fadeIn = -1f;
        }

        // A short delay to homing behavior after being fired
        if (DelayTimer < 10)
        {
            DelayTimer += 1;
            return;
        }

        // First, we find a homing target if we don't have one
        HomingTarget ??= FindClosestPlayer(maxDetectRadius);

        // If we have a homing target, make sure it is still valid. If the NPC dies or moves away, we'll want to find a new target
        if (
            HomingTarget != null
            && (
                HomingTarget.dead
                || !HomingTarget.active
                || (HomingTarget.Center - Projectile.Center).LengthSquared()
                    > maxDetectRadius * maxDetectRadius
            )
        )
        {
            HomingTarget = null;
        }

        // If we don't have a target, don't adjust trajectory
        if (HomingTarget == null)
            return;

        // If found, we rotate the projectile velocity in the direction of the target.
        float length = Projectile.velocity.Length();
        float targetAngle = Projectile.AngleTo(HomingTarget.Center);
        Projectile.velocity =
            Projectile
                .velocity.ToRotation()
                .AngleTowards(targetAngle, MathHelper.ToRadians(20))
                .ToRotationVector2() * length;
    }

    public Player FindClosestPlayer(float maxDetectDistance)
    {
        Player closestPlayer = null;
        // Using squared values in distance checks will let us skip square root calculations, drastically improving this method's speed.
        float sqrMaxDetectDistance = maxDetectDistance * maxDetectDistance;

        foreach (Player target in Main.ActivePlayers)
        {
            if (
                closestPlayer == null
                || (target.Center - Projectile.Center).LengthSquared() < sqrMaxDetectDistance
            )
                closestPlayer = target;
        }

        return closestPlayer;
    }

    public override void OnHitPlayer(Player target, Player.HurtInfo info)
    {
        Projectile.Kill();
        target.AddBuff(BuffID.Shimmer, 10, false);
    }

    public override void OnKill(int timeLeft)
    {
        if (timeLeft > 0)
            return;
        if (Main.netMode != NetmodeID.Server)
        {
            int numDust = 20;
            for (int i = 0; i < numDust; i++) // Loop through code below numDust times
            {
                Vector2 velocity = Vector2.One.RotatedBy(MathHelper.ToRadians(360f / numDust * i)); // Circular velocity
                Dust.NewDustPerfect(Projectile.Center, DustID.ShimmerSpark, velocity).noGravity =
                    true; // Creating dust
            }
        }
        const int numBubbles = 32;
        int bubbleDamage = Projectile.damage / numBubbles * 2;
        if (Projectile.owner == Main.myPlayer)
        {
            for (int i = 0; i < numBubbles; i++)
            {
                Vector2 velocity = Vector2.One.RotatedBy(
                    MathHelper.ToRadians(360f / numBubbles * i)
                ); // Circular velocity
                var smallBubble = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromAI(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<SmallBubble>(),
                    bubbleDamage,
                    4.5f,
                    Main.myPlayer
                );
            }
        }
    }
}
