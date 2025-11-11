using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace terraari.Content.Projectiles
{
    public class BigBubble : ModProjectile
    {
        private static Effect bubbleEffect;
        private static Texture2D circleTexture;

        public override string Texture => "Terraria/Images/Projectile_0"; // We will use no texture

        public override void Load()
        {
            if (Main.dedServ)
                return;
            bubbleEffect = ModContent
                .Request<Effect>(
                    "terraari/Assets/Effects/BubbleWarp",
                    ReLogic.Content.AssetRequestMode.ImmediateLoad
                )
                .Value;
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
            circleTexture?.Dispose();
            circleTexture = null;
        }

        public override void AI()
        { // This hook updates every tick
            if (Main.netMode != NetmodeID.Server) // Do not spawn dust on server!
            {
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
        }

        public override void OnKill(int timeLeft)
        { // What happens on projectile death
            int numDust = 20;
            for (int i = 0; i < numDust; i++) // Loop through code below numDust times
            {
                Vector2 velocity = Vector2.One.RotatedBy(MathHelper.ToRadians(360 / numDust * i)); // Circular velocity
                Dust.NewDustPerfect(Projectile.Center, DustID.Electric, velocity).noGravity = true; // Creating dust
            }
        }
    }
}
