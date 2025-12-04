using System.Collections.Generic;
using Terraari.Common.Systems;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace terraari.Content.NPCs
{
    [AutoloadHead]
    public class HydrolysistTownNPC : ModNPC
    {
        public const string ShopName = "Shop";

        public override string Texture =>
            $"{Mod.Name}/Content/NPCs/HydrolysistNPC/HydrolysistTownNPC";

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 25;

            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.DangerDetectRange[Type] = 700;
            NPCID.Sets.AttackType[Type] = 0;
            NPCID.Sets.AttackTime[Type] = 90;
            NPCID.Sets.AttackAverageChance[Type] = 30;
            NPCID.Sets.HatOffsetY[Type] = 4;

            NPCID.Sets.ShimmerTownTransform[Type] = true;

            NPC.Happiness.SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
                .SetBiomeAffection<SnowBiome>(AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Dryad, AffectionLevel.Love)
                .SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
                .SetNPCAffection(NPCID.Merchant, AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Demolitionist, AffectionLevel.Hate);

            ContentSamples.NpcBestiaryRarityStars[Type] = 3;
        }

        public override void SetDefaults()
        {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 18;
            NPC.height = 40;
            NPC.aiStyle = NPCAIStyleID.Passive;
            NPC.damage = 10;
            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;

            AnimationType = NPCID.Guide;
        }

        // This makes him eligible to spawn as a town NPC once the boss is beaten
        public override bool CanTownNPCSpawn(int numTownNPCs)
        {
            // Only after he has been unlocked and the boss is downed in this world
            return HydrolysistWorldSystem.unlockedHydrolysist
                && DownedBossSystem.downedHydrolysistBoss;
        }

        public override bool CanChat() => true;

        public override void SetChatButtons(ref string button, ref string button2)
        {
            // First button = Shop, second button unused for now
            button = Language.GetTextValue("LegacyInterface.28"); // "Shop"
            button2 = "";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (firstButton)
            {
                // Open our shop
                shopName = ShopName;
            }
        }

        public override void AddShops()
        {
            var npcShop = new NPCShop(Type, ShopName);

            // Raw item IDs (from the wiki)
            const int AetherCampfireID = 5357;
            const int TerraformerID = 5134;

            static Item Price(
                int type,
                int platinum = 0,
                int gold = 0,
                int silver = 0,
                int copper = 0
            ) => new Item(type) { shopCustomPrice = Item.buyPrice(platinum, gold, silver, copper) };

            // Helium Moss + building set
            npcShop.Add(Price(ItemID.RainbowMoss, copper: 1));
            npcShop.Add(Price(ItemID.RainbowMossBlock, copper: 2));
            npcShop.Add(Price(ItemID.RainbowMossBlockWall, copper: 2));

            // Aetherium / Shimmer building set
            npcShop.Add(Price(ItemID.ShimmerBrick, copper: 2));
            npcShop.Add(Price(ItemID.ShimmerBrickWall, copper: 2));
            npcShop.Add(Price(ItemID.ShimmerBlock, copper: 2));
            npcShop.Add(Price(ItemID.ShimmerWall, copper: 2));

            // Aetherium light / furniture
            npcShop.Add(Price(ItemID.ShimmerTorch, copper: 5));
            npcShop.Add(Price(AetherCampfireID, silver: 1));
            npcShop.Add(Price(ItemID.ShimmerMonolith, gold: 1));

            // Shimmer utility / combat stuff
            npcShop.Add(Price(ItemID.ShimmerFlare, copper: 15));
            npcShop.Add(Price(ItemID.ShimmerArrow, copper: 10));
            npcShop.Add(Price(ItemID.ShimmerCloak, gold: 10));
            npcShop.Add(Price(ItemID.GasTrap, gold: 1));

            // POST-MOON LORD ITEMS

            npcShop.Add(Price(ItemID.HeavenforgeBrick, copper: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(ItemID.LunarRustBrick, copper: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(ItemID.AstraBrick, copper: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(ItemID.DarkCelestialBrick, copper: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(ItemID.MercuryBrick, copper: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(ItemID.StarRoyaleBrick, copper: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(ItemID.CryocoreBrick, copper: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(ItemID.CosmicEmberBrick, copper: 5), Condition.DownedMoonLord);

            npcShop.Add(Price(ItemID.RodOfHarmony, platinum: 5), Condition.DownedMoonLord);
            npcShop.Add(Price(TerraformerID, platinum: 2), Condition.DownedMoonLord);

            npcShop.Register();
        }

        //custom name pool
        public override List<string> SetNPCNameList()
        {
            return new List<string> { "Hydro", "Lyss", "Mad Scientist", "Scary Guy", "" };
        }
    }
}
