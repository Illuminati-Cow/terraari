using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraari.Content.Projectiles;

public class SmallBubble : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Bubble}";

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Magic; // Damage class projectile uses
        Projectile.scale = 1f; // Projectile scale multiplier
        Projectile.penetrate = 3; // How many hits projectile have to make before it dies. 3 means projectile will die on 3rd enemy. Setting this to 0 will make projectile die instantly
        Projectile.aiStyle = ProjAIStyleID.Bubble; // AI style of a projectile. 0 is default bullet AI
        Projectile.width = Projectile.height = 10; // Hitbox of projectile in pixels
        Projectile.friendly = false; // Can hit enemies?
        Projectile.hostile = true; // Can hit player?
        Projectile.timeLeft = 180; // Time in ticks before projectile dies
        Projectile.light = 0.3f; // How much light projectile provides
        Projectile.ignoreWater = true; // Does the projectile ignore water (doesn't slow down in it)
        Projectile.tileCollide = true; // Does the projectile collide with tiles, like blocks?
        Projectile.alpha = 255; // Completely transparent
    }

    public override void AI()
    {
        // This hook updates every tick
        if (Main.netMode == NetmodeID.Server)
            return;
        Dust dust = Dust.NewDustPerfect(
            Position: Projectile.Center,
            Type: DustID.Electric,
            Velocity: Vector2.Zero,
            Alpha: 100,
            newColor: Color.White,
            Scale: 0.9f
        );
        dust.noGravity = true; // Dust don't have gravity
        dust.fadeIn = -1f;
    }

    public override void OnKill(int timeLeft)
    {
        const int numDust = 20;
        for (int i = 0; i < numDust; i++)
        {
            Vector2 velocity = Vector2.One.RotatedBy(
                MathHelper.ToRadians(360 / (float)numDust * i)
            ); // Circular velocity
            Dust.NewDustPerfect(Projectile.Center, DustID.Electric, velocity).noGravity = true;
        }
    }
}
