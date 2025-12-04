using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraari.Common.Helpers;

public static class ShaderHelper {
    public static Effect SetUpShimmerShader() {
        if (Main.netMode == NetmodeID.Server) return null;
        Effect shader = ModContent.Request<Effect>("terraari/Assets/Effects/ShimmerGlow", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        return shader;
    }

    public static void DrawShimmerShader(Effect shader, Texture2D texture, Vector2 mainPos, Rectangle sourceRect, Color drawColor, float rotation, Vector2 origin, float scale, SpriteEffects spriteEffects, float layerDepth, float seed = 0)
    {
        // Don't draw on server
        if (Main.netMode == NetmodeID.Server || shader == null) return;

        SpriteBatch spriteBatch = Main.spriteBatch;
        
        // Save the current spritebatch state
        spriteBatch.End();

        // Start spritebatch with shader
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, 
            SamplerState.LinearClamp, DepthStencilState.Default, 
            RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        // Set shader parameters (customize based on your shader)
        shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
        shader.Parameters["random"]?.SetValue(seed);
        // Vector4[] colors = [
        //     new(255/255, 139/255, 139/255, 1),
        //     new(255/255, 203/255, 164/255, 1),
        //     new(255/255, 232/255, 058/255, 1),
        //     new(168/255, 255/255, 157/255, 1),
        //     new(188/255, 255/255, 244/255, 1),
        //     new(134/255, 167/255, 255/255, 1),
        //     new(135/255, 121/255, 255/255, 1),
        //     new(255/255, 124/255, 255/255, 1),
        //     new(255/255, 175/255, 255/255, 1)
        // ];
        // shader.Parameters["colors"].SetValue(colors);
        // shader.Parameters["COLOR_COUNT"].SetValue(colors.Length);
        
        shader.CurrentTechnique.Passes[0].Apply();

        // spriteBatch.Draw(texture, drawPosition, sourceRect, Color.White, 
        //     entity.rotation, origin, entity.scale, SpriteEffects.None, 0f);
        
        spriteBatch.Draw(texture, mainPos, sourceRect, drawColor, rotation, origin, scale, spriteEffects, 0f);

        // Restart spritebatch with default settings
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, 
            Main.DefaultSamplerState, DepthStencilState.None, 
            RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
    }
}