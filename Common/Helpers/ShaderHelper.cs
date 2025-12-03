namespace terraari.Common.Helpers;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

public class ShaderHelper {
    public static Effect SetUpShimmerShader() {
        if (Main.netMode == NetmodeID.Server) return null;
        return ModContent.Request<Effect>("terraari/Assets/Effects/ShimmerGlow", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
    }

    public static void DrawShimmerShader(Effect shader, string textureName, Projectile projectile)
    {
        // Don't draw on server
        if (Main.netMode == NetmodeID.Server || shader == null) return;

        SpriteBatch spriteBatch = Main.spriteBatch;
        Texture2D texture = ModContent.Request<Texture2D>(textureName).Value;
        
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
        Vector2 drawPosition = projectile.Center - Main.screenPosition;
        Rectangle sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
        Vector2 origin = sourceRect.Size() / 2f;

        spriteBatch.Draw(texture, drawPosition, sourceRect, Color.White, 
            projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0f);

        // Restart spritebatch with default settings
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, 
            Main.DefaultSamplerState, DepthStencilState.None, 
            RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
    }
}