using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraari.Content.Projectiles;

public class ShimmerLightning : ModProjectile
{
    public override string Texture => $"{Mod.Name}/Content/Projectiles/{nameof(ShimmerLightning)}";

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
    }

    public override void AI()
    {
        this.Projectile.frameCounter++;
        Lighting.AddLight(Projectile.Center, 0.3f, 0.45f, 0.5f);
        // What is this check for?
        if (Projectile.velocity != Vector2.Zero)
            return;
        if (Projectile.frameCounter >= Projectile.extraUpdates * 2)
        {
            Projectile.frameCounter = 0;
            bool shouldKill = true;
            for (int i = 1; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] != Projectile.oldPos[0])
                {
                    shouldKill = false;
                }
            }

            if (shouldKill)
            {
                Projectile.Kill();
                return;
            }
        }

        // Spawn lightning dust less frequently the more extra updates occur
        // Not sure why
        if (Main.rand.Next(Projectile.extraUpdates) != 0)
            return;

        for (int i = 0; i < 2; i++)
        {
            float dustDirection =
                Projectile.rotation + (Main.rand.NextBool(2) ? -1f : 1f) * ((float)Math.PI / 2f);
            float dustSpeed = (float)Main.rand.NextDouble() * 0.8f + 1f;
            var dustVelocity = new Vector2(
                (float)Math.Cos(dustDirection) * dustSpeed,
                (float)Math.Sin(dustDirection) * dustSpeed
            );
            int dust = Dust.NewDust(
                Projectile.Center,
                0,
                0,
                DustID.Electric,
                dustVelocity.X,
                dustVelocity.Y
            );
            Main.dust[dust].noGravity = true;
            Main.dust[dust].scale = 1.2f;
        }

        // Spawn smoke particles with a fixed chance
        if (Main.rand.NextBool(5))
            return;

        Vector2 spawnDirection =
            Projectile.velocity.RotatedBy(1.5707963705062866)
            * ((float)Main.rand.NextDouble() - 0.5f)
            * Projectile.width;
        int smokeDustId = Dust.NewDust(
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
        Dust smokeDust = Main.dust[smokeDustId];
        smokeDust.velocity *= 0.5f;
        Main.dust[smokeDustId].velocity.Y = 0f - Math.Abs(Main.dust[smokeDustId].velocity.Y);
    }

    public override void OnKill(int timeLeft) { }
}
