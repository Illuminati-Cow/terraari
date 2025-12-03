using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace terraari.Content.Projectiles
{
    public class Bubble : ModProjectile
    {
        private Effect shader;
        
        public override void SetDefaults()
        {
            // Projectile dimensions
            Projectile.width = 48;
            Projectile.height = 48;
            
            // Projectile behavior
            Projectile.friendly = true; // Can hit enemies
            Projectile.hostile = false; // Cannot hit players
            Projectile.DamageType = DamageClass.Ranged; // Damage type (Melee, Ranged, Magic, Summon, etc.)
            Projectile.penetrate = 1; // How many enemies it can hit before dying (-1 = infinite)
            Projectile.timeLeft = 600; // How long the projectile lives (60 = 1 second)
            
            // Visual properties
            Projectile.alpha = 0; // Transparency (0 = opaque, 255 = invisible)
            Projectile.light = 0.5f; // Light emission (0-1)
            Projectile.ignoreWater = false; // Affected by water
            Projectile.tileCollide = true; // Collides with tiles
            
            // Physics
            Projectile.aiStyle = 0; // AI style (1 = arrow-like behavior)
            AIType = ProjectileID.WoodenArrowFriendly; // Mimics wooden arrow AI
        
            // Load the shader
            if (Main.netMode != NetmodeID.Server)
            {
                // Load your .xnb shader file (without the .xnb extension)
                // Place the .xnb file in: YourMod/Content/Effects/
                shader = ModContent.Request<Effect>("terraari/Assets/Effects/BubbleWarp",
                    ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
            }
        }

        public override void AI()
        {
            // Example: Spawn dust particles
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
                    DustID.Smoke, 0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // What happens when projectile hits a tile
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            
            return true; // true = destroy projectile, false = keep alive
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // What happens when projectile hits an NPC
            
            // Example: Apply debuff
            target.AddBuff(BuffID.OnFire, 180); // On Fire for 3 seconds
        }

        public override void Kill(int timeLeft)
        {
            // What happens when projectile dies
            
            // Example: Spawn dust on death
            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
                    DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                dust.velocity *= 1.4f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Don't draw on server
            if (Main.netMode == NetmodeID.Server || shader == null)
                return false;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            
            // Save the current spritebatch state
            spriteBatch.End();

            // Start spritebatch with shader
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, 
                SamplerState.LinearClamp, DepthStencilState.Default, 
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Set shader parameters (customize based on your shader)
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            
            shader.CurrentTechnique.Passes[0].Apply();

            // Draw the projectile with shader
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 origin = sourceRect.Size() / 2f;

            spriteBatch.Draw(texture, drawPosition, sourceRect, Color.White, 
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            // Restart spritebatch with default settings
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, 
                Main.DefaultSamplerState, DepthStencilState.None, 
                RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            // Return false to prevent default drawing
            return false;
        }
    }
}