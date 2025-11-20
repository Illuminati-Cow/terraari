using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace terraari.Content.Projectiles
{
    public class Bubble : ModProjectile
    {
        private static Effect bubbleEffect;
        private static Texture2D circleTexture;

        private static RenderTarget2D _screenCapture;

        public override string Texture => "Terraria/Images/Projectile_0";

        public override void Load()
        {
            if (!Main.dedServ)
            {
                bubbleEffect = ModContent.Request<Effect>("terraari/Assets/Effects/BubbleWarp", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
            }
            if (Main.netMode != NetmodeID.Server)
            {
                Main.QueueMainThreadAction(() =>
                {
                    _screenCapture = new RenderTarget2D(
                        Main.graphics.GraphicsDevice,
                        Main.screenWidth,
                        Main.screenHeight
                    );
                });
            }
        }

        public override void SetDefaults()
        {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.scale = 1f;
            Projectile.penetrate = 3;
            Projectile.aiStyle = 0;
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.timeLeft = 90;
            Projectile.light = 0.3f;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.alpha = 255;
        }

        public void GenerateCircleTexture()
        {
            if (circleTexture == null)
            {
                int size = 128;
                Color[] colorData = new Color[size * size];
                Vector2 center = new Vector2(size / 2f, size / 2f);
                float radius = size / 2f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 pos = new Vector2(x, y);
                        float dist = Vector2.Distance(pos, center);

                        // Smooth edge with gradient
                        if (dist <= radius)
                        {
                            float alpha = 1f - (dist / radius);
                            colorData[y * size + x] = new Color(alpha, alpha, alpha, alpha);
                        }
                        else
                        {
                            colorData[y * size + x] = Color.Transparent;
                        }
                    }
                }

                circleTexture = new Texture2D(Main.graphics.GraphicsDevice, size, size);
                circleTexture.SetData(colorData);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // Capture screen before drawing this projectile
            CaptureScreen();
            return true;
        }
        
        private void CaptureScreen() {
            var gd = Main.graphics.GraphicsDevice;

            // Save previous render targets
            var previousTargets = gd.GetRenderTargets();

            // Set our render target
            gd.SetRenderTarget(_screenCapture);
            gd.Clear(Color.Transparent);

            // Draw the current screen onto _screenCapture
            if (Main.screenTarget != null)
            {
                var spriteBatch = Main.spriteBatch;
                spriteBatch.Begin();
                spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                spriteBatch.End();
            }

            // Restore previous render target
            gd.SetRenderTargets(previousTargets);
        }

        public override void PostDraw(Color lightColor)
        {
            if (Main.dedServ || bubbleEffect == null) return;

            GenerateCircleTexture();

            // Use Terraria's main screen target which already contains the rendered scene
            var screenTarget = Main.screenTarget;
            if (screenTarget == null) return;

            // Calculate position on screen
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float size = 100f; // Size of the bubble effect

            // Main.graphics.GraphicsDevice.Textures[1] = _screenCapture;
            // bubbleEffect.Parameters["uImageSize1"].SetValue(new Vector2(Main.screenWidth, Main.screenHeight));

            // Set shader parameters
            bubbleEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            bubbleEffect.Parameters["uImageScreen"]?.SetValue(screenTarget);
            bubbleEffect.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            bubbleEffect.Parameters["uPosition"]?.SetValue(screenPos);
            bubbleEffect.Parameters["uSize"]?.SetValue(size);
            
            // Set the screen target to texture register 1 (for uImage1)
            Main.instance.GraphicsDevice.Textures[1] = Main.screenTarget;
        Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp; // or your preferred sampler state


            // Apply shader and draw
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, bubbleEffect, Main.GameViewMatrix.TransformationMatrix);
            

            // Draw the circle texture which will be warped by the shader
            Main.spriteBatch.Draw(circleTexture,
                new Rectangle((int)(screenPos.X - size / 2),
                            (int)(screenPos.Y - size / 2),
                            (int)size, (int)size),
                Color.White);

            // Restore default spritebatch
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void Unload()
        {
            bubbleEffect = null;
            // circleTexture?.Dispose(); // Allegedly GPU handles this on shuddown automatically. Causes crash if you include it here because of thread issues
            circleTexture = null;
        }

        public override void AI()
        {
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
        }

        public override void OnKill(int timeLeft)
        {
            int numDust = 20;
            for (int i = 0; i < numDust; i++)
            {
                Vector2 velocity = Vector2.One.RotatedBy(MathHelper.ToRadians(360 / numDust * i));
                Dust.NewDustPerfect(Projectile.Center, DustID.Electric, velocity).noGravity = true;
            }
        }
    }
}