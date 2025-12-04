using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using ShaderHelper = Terraari.Common.Helpers.ShaderHelper;

namespace Terraari.Content.Projectiles;

public class BigBubble : ModProjectile
{
    private Effect shader;
    private float seed;

    private Player HomingTarget
    {
        get => Projectile.ai[0] < 0 ? null : Main.player[(int)Projectile.ai[0]];
        set => Projectile.ai[0] = value?.whoAmI ?? -1;
    }

    private bool ShouldSpawnBubbles
    {
        get => Projectile.ai[2] == 0;
        set => Projectile.ai[2] = value ? 1 : 0;
    }

    public ref float DelayTimer => ref Projectile.ai[1];

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.CanHitPastShimmer[Projectile.type] = true;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Magic; // Damage class projectile uses
        Projectile.scale = 1f; // Projectile scale multiplier
        Projectile.penetrate = 1; // How many hits projectile have to make before it dies. 3 means projectile will die on 3rd enemy. Setting this to 0 will make projectile die instantly
        Projectile.aiStyle = 0; // AI style of a projectile. 0 is default bullet AI
        Projectile.width = Projectile.height = 10; // Hitbox of projectile in pixels
        Projectile.friendly = false; // Can hit enemies?
        Projectile.hostile = true; // Can hit player?
        Projectile.timeLeft = 150; // Time in ticks before projectile dies
        Projectile.light = 0.3f; // How much light projectile provides
        Projectile.ignoreWater = true; // Does the projectile ignore water (doesn't slow down in it)
        Projectile.tileCollide = false; // Does the projectile collide with tiles, like blocks?
        Projectile.alpha = 255; // Completely transparent

        shader = ShaderHelper.SetUpShimmerShader();
        seed = (float)Random.Shared.NextDouble();
    }

    public override void OnSpawn(IEntitySource source)
    {
        HomingTarget = null; // Reset homing target
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        Rectangle frame = new(0, 0, texture.Width, texture.Height);
        Vector2 origin = texture.Size() / 2f;
        Vector2 mainPos = Projectile.Center - Main.screenPosition;

        ShaderHelper.DrawShimmerShader(
            shader,
            texture,
            mainPos,
            frame,
            Color.White,
            Projectile.rotation,
            origin,
            Projectile.scale,
            SpriteEffects.None,
            0,
            seed * 1000f
        );

        // Return false to prevent default drawing
        return false;
    }

    public override void AI()
    {
        const float maxDetectRadius = 400f;
        if (Main.netMode != NetmodeID.Server)
        {
            Dust dust = Dust.NewDustPerfect(
                Position: Projectile.Center,
                Type: DustID.ShimmerSpark,
                Velocity: Vector2.Zero,
                Alpha: 100,
                newColor: Color.White,
                Scale: 0.9f
            );
            dust.scale = Main.rand.NextFloat(1f, 1.4f);
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
                .AngleTowards(targetAngle, MathHelper.ToRadians(0.5f))
                .ToRotationVector2() * length;
    }

    public Player FindClosestPlayer(float maxDetectDistance)
    {
        Player closestPlayer = null;
        float closestDistanceSquared = maxDetectDistance * maxDetectDistance;

        foreach (Player target in Main.ActivePlayers)
        {
            if (
                closestPlayer == null
                || (target.Center - Projectile.Center).LengthSquared() < closestDistanceSquared
            )
            {
                closestPlayer = target;
                closestDistanceSquared = (target.Center - Projectile.Center).LengthSquared();
            }
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

        if (Projectile.owner != Main.myPlayer || !ShouldSpawnBubbles)
            return;

        const int numBubbles = 32;
        int bubbleDamage = Projectile.damage / numBubbles * 2;
        for (int i = 0; i < numBubbles; i++)
        {
            Vector2 velocity = Vector2.One.RotatedBy(MathHelper.ToRadians(360f / numBubbles * i)); // Circular velocity
            Projectile.NewProjectileDirect(
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
