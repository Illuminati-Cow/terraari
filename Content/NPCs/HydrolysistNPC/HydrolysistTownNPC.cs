using System.Collections.Generic;
using Terraari.Common.Systems;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Events;
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
        public int NumberOfTimesTalkedTo = 0;

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

        public override string GetChat()
        {
            Player player = Main.LocalPlayer;
            NumberOfTimesTalkedTo++;

            // ========== First interaction ==========
            if (NumberOfTimesTalkedTo == 1)
            {
                return "Oi, my name is The Hydrolisist... You don't know what that is? Well, for someone like you I will simply say that I study liquids.";
            }

            // Graveyard
            if (player.ZoneGraveyard)
            {
                if (Main.rand.NextBool())
                {
                    return "Goodness, typically I like it when the water molecules group up and float around. But this just feels... ominous.";
                }
                return "You know all those undead have close to no liquid left in their body? I would hate my existence as well if I were a dry husk.";
            }

            // Blood Moon
            if (Main.bloodMoon)
            {
                return "Scary... Blood is one liquid I have no interest in. Leave that in the body, thank you.";
            }

            // Party
            if (BirthdayParty.PartyIsUp)
            {
                return "Typically I avoid parties, but this one seems fun enough.";
            }

            // Thunderstorm
            if (Main.IsItStorming)
            {
                if (Main.rand.NextBool())
                {
                    return "Ugh, the only thing that could ruin the rain... it's the thunder and lightning.";
                }
                return "Guess today I will only observe from inside...";
            }

            // Plain rain (but not storming)
            if (Main.raining && !Main.IsItStorming)
            {
                if (Main.rand.NextBool())
                {
                    return "Yes, yes, yes, I love the rain! I get to take some of the water and study it! Goodness, all the beautiful things found in the rain!";
                }
                return "Good thing there is no acid in this rain, otherwise it would melt the collection pots I have outside.";
            }

            // Windy day (no rain)
            if (Main.IsItAHappyWindyDay && !Main.raining)
            {
                if (Main.rand.NextBool())
                {
                    return "Nothing interesting about air. It just blows and ruins my cloak.";
                }
                return "If we have to deal with the wind, why not add a little bit of rain so I can get a sample.";
            }

            // Witch Doctor present
            int witchDoctorIndex = NPC.FindFirstNPC(NPCID.WitchDoctor);
            if (witchDoctorIndex >= 0)
            {
                string wdName = Main.npc[witchDoctorIndex].GivenName;
                return $"No, I am nothing like {wdName}! He believes in voodoo while I do real science.";
            }

            // Steampunker present
            int steampunkerIndex = NPC.FindFirstNPC(NPCID.Steampunker);
            if (steampunkerIndex >= 0)
            {
                string spName = Main.npc[steampunkerIndex].GivenName;
                return $"{spName} keeps asking what liquid would best run their machines. Using anything other than water would just damage their machines, as well as the environment!";
            }

            switch (Main.rand.Next(4))
            {
                case 0:
                    return "What do you need? Can you not see that I have some experiments I am trying to run!";
                case 1:
                    return "No, of course I don't study the ocean water... That's a different person entirely.";
                case 2:
                    return "Wait, don't... ugh, whatever, it's just water, but please don't just drink the stuff laying around.";
                default:
                    return "Hm, if I were to combine this liquid with some of this then maybe... huh? Oh, sorry, didn't see you come in, just rambling.";
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
            return new List<string> { "Hydro", "Lyss", "Tyler", "Coleman", "Thomas" };
        }
    }
}
