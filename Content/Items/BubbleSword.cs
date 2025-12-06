using System;
using TheHydrolysist.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheHydrolysist.Content.Items
{
    public class BubbleSword : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.staff[Item.type] = true;
        }

        public override void SetDefaults()
        {
            // Helper method to quickly set basic magic weapon properties
            Item.DefaultToMagicWeapon(
                projType: ModContent.ProjectileType<BigBubble>(), // Our own projectile
                singleShotTime: 8, // useTime & useAnimation
                shotVelocity: 10f,
                hasAutoReuse: true
            );

            Item.damage = 1;
            Item.knockBack = 5f;
            Item.value = 10000;
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item85; // Bubble gun sound
            Item.mana = 1; // This item uses 10 mana
            Item.width = Item.height = 40;
        }

        public override void AddRecipes()
        {
            CreateRecipe().AddIngredient(ItemID.Wood, 1).AddTile(TileID.WorkBenches).Register();
        }
    }
}
