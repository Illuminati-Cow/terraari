using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraari.Content.Buffs;

public class ShimmerImmunityBuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
        Main.debuff[Type] = false;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.buffImmune[BuffID.Shimmer] = true;
    }

    public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
    {
        buffName = "Shimmer Immunity";
        tip = "You are immune to Shimmer.";
        rare = ItemRarityID.Pink;
    }
}
