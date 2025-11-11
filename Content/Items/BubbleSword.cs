using Microsoft.Xna.Framework;
using terraari.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace terraari.Content.Items
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
                projType: ModContent.ProjectileType<Bubble>(), // Our own projectile
                singleShotTime: 35, // useTime & useAnimation
                shotVelocity: 9f,
                hasAutoReuse: true
            );

            Item.damage = 45;
            Item.knockBack = 5f;
            Item.value = 10000;
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item94; // Some electric sound
            Item.mana = 1; // This item uses 10 mana
            Item.width = Item.height = 40;
        }

        public override void AddRecipes()
        {
            CreateRecipe().AddIngredient(ItemID.Wood, 1).AddTile(TileID.WorkBenches).Register();
        }
    }
}
