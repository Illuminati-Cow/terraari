using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.Personalities;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;

namespace terraari.Content.NPCs
{
	// [AutoloadHead] and NPC.townNPC are extremely important and absolutely both necessary for any Town NPC to work at all.
	// public override string Texture => $"Terraria/Images/NPC_{NPCID.Guide}";
	// Later, switch to: $"{Mod.Name}/Content/NPCs/{nameof(HydrolysistNpcBody)}"

	[AutoloadHead]
	public class HydrolysistNpcBody : ModNPC
	{
		public const string ShopName = "Shop";
		public int NumberOfTimesTalkedTo = 0;

		private static int ShimmerHeadIndex;
		private static Profiles.StackedNPCProfile NPCProfile;

		public static LocalizedText UpgradedText { get; private set; }

		// Sets a unique message when the NPC dies.
		// See also NPCID.Sets.IsTownChild if you just want the message used by Angler and Princess.
		// See ModifyDeathMessage() way below for more details
		public override LocalizedText DeathMessage => this.GetLocalization("DeathMessage");


		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25; // The total amount of frames the NPC has

			NPCID.Sets.ExtraFramesCount[Type] = 9; // Generally for Town NPCs, but this is how the NPC does extra things such as sitting in a chair and talking to other NPCs. This is the remaining frames after the walking frames.
			NPCID.Sets.AttackFrameCount[Type] = 4; // The amount of frames in the attacking animation.
			NPCID.Sets.DangerDetectRange[Type] = 700; // The amount of pixels away from the center of the NPC that it tries to attack enemies.
			NPCID.Sets.AttackType[Type] = 0; // The type of attack the Town NPC performs. 0 = throwing, 1 = shooting, 2 = magic, 3 = melee
			NPCID.Sets.AttackTime[Type] = 90; // The amount of time it takes for the NPC's attack animation to be over once it starts.
			NPCID.Sets.AttackAverageChance[Type] = 30; // The denominator for the chance for a Town NPC to attack. Lower numbers make the Town NPC appear more aggressive.
			NPCID.Sets.HatOffsetY[Type] = 4; // For when a party is active, the party hat spawns at a Y offset.
			NPCID.Sets.ShimmerTownTransform[NPC.type] = true; // This set says that the Town NPC has a Shimmered form. Otherwise, the Town NPC will become transparent when touching Shimmer like other enemies.

			NPCID.Sets.ShimmerTownTransform[Type] = true; // Allows for this NPC to have a different texture after touching the Shimmer liquid.

			// Connects this NPC with a custom emote.
			// This makes it when the NPC is in the world, other NPCs will "talk about him".
			// By setting this you don't have to override the PickEmote method for the emote to appear.
			// NPCID.Sets.FaceEmote[Type] = ModContent.EmoteBubbleType<ExamplePersonEmote>();

			// Influences how the NPC looks in the Bestiary
			NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers()
			{
				Velocity = 1f, // Draws the NPC in the bestiary as if its walking +1 tiles in the x direction
				Direction = 1 // -1 is left and 1 is right. NPCs are drawn facing the left by default but ExamplePerson will be drawn facing the right
							  // Rotation = MathHelper.ToRadians(180) // You can also change the rotation of an NPC. Rotation is measured in radians
							  // If you want to see an example of manually modifying these when the NPC is drawn, see PreDraw
			};
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

			// Set Example Person's biome and neighbor preferences with the NPCHappiness hook. You can add happiness text and remarks with localization (See an example in ExampleMod/Localization/en-US.lang).
			// NOTE: The following code uses chaining - a style that works due to the fact that the SetXAffection methods return the same NPCHappiness instance they're called on.
			NPC.Happiness
				.SetBiomeAffection<ForestBiome>(AffectionLevel.Like) // Example Person prefers the forest.
				.SetBiomeAffection<SnowBiome>(AffectionLevel.Dislike) // Example Person dislikes the snow.
				// .SetBiomeAffection<ExampleSurfaceBiome>(AffectionLevel.Love) // Example Person likes the Example Surface Biome
				.SetNPCAffection(NPCID.Dryad, AffectionLevel.Love) // Loves living near the dryad.
				.SetNPCAffection(NPCID.Guide, AffectionLevel.Like) // Likes living near the guide.
				.SetNPCAffection(NPCID.Merchant, AffectionLevel.Dislike) // Dislikes living near the merchant.
				.SetNPCAffection(NPCID.Demolitionist, AffectionLevel.Hate) // Hates living near the demolitionist.
			; // < Mind the semicolon!

			// This creates a "profile" for ExamplePerson, which allows for different textures during a party and/or while the NPC is shimmered.

			//Textures from example NPC, can be used to modify textures for "Party" or "Shimmered"

			// NPCProfile = new Profiles.StackedNPCProfile(
			// 	new Profiles.DefaultNPCProfile(Texture, NPCHeadLoader.GetHeadSlot(HeadTexture), Texture + "_Party"),
			// 	new Profiles.DefaultNPCProfile(Texture + "_Shimmer", ShimmerHeadIndex, Texture + "_Shimmer_Party")
			// );

			ContentSamples.NpcBestiaryRarityStars[Type] = 3; // We can override the default bestiary star count calculation by setting this.

			UpgradedText = this.GetLocalization("Upgraded");


		}
		public override void SetDefaults()
		{
			NPC.townNPC = true; // Sets NPC to be a Town NPC
			NPC.friendly = true; // NPC Will not attack player
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

		// public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		// {
		// 	// We can use AddRange instead of calling Add multiple times in order to add multiple items at once
		// 	bestiaryEntry.Info.AddRange([
		// 		// Sets the preferred biomes of this town NPC listed in the bestiary.
		// 		// With Town NPCs, you usually set this to what biome it likes the most in regards to NPC happiness.
		// 		BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,

		// 		// Sets your NPC's flavor text in the bestiary. (use localization keys)
		// 		new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExamplePerson_1"),

		// 		// You can add multiple elements if you really wanted to
		// 		new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExamplePerson_2")
		// 	]);
		// }

		// public override void HitEffect(NPC.HitInfo hit)
		// {
		// 	int num = NPC.life > 0 ? 1 : 5;

		// 	for (int k = 0; k < num; k++)
		// 	{
		// 		Dust.NewDust(NPC.position, NPC.width, NPC.height, ModContent.DustType<Sparkle>());
		// 	}

		// 	// Create gore when the NPC is killed.
		// 	if (Main.netMode != NetmodeID.Server && NPC.life <= 0)
		// 	{
		// 		// Retrieve the gore types. This NPC has shimmer and party variants for head, arm, and leg gore. (12 total gores)
		// 		string variant = "";
		// 		if (NPC.IsShimmerVariant)
		// 			variant += "_Shimmer";
		// 		if (NPC.altTexture == 1)
		// 			variant += "_Party";
		// 		int hatGore = NPC.GetPartyHatGore();
		// 		int headGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Head").Type;
		// 		int armGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Arm").Type;
		// 		int legGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Leg").Type;

		// 		// Spawn the gores. The positions of the arms and legs are lowered for a more natural look.
		// 		if (hatGore > 0)
		// 		{
		// 			Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, hatGore);
		// 		}
		// 		Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, headGore, 1f);
		// 		Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
		// 		Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
		// 		Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
		// 		Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
		// 	}
		// }

		// public override void OnSpawn(IEntitySource source)
		// {
		// 	if (source is EntitySource_SpawnNPC)
		// 	{
		// 		// A TownNPC is "unlocked" once it successfully spawns into the world.
		// 		TownNPCRespawnSystem.unlockedExamplePersonSpawn = true;
		// 	}
		// }

		public override bool CanTownNPCSpawn(int numTownNPCs)
		{ // Requirements for the town NPC to spawn.
			// if (TownNPCRespawnSystem.unlockedExamplePersonSpawn)
			// {
			// 	// If Example Person has spawned in this world before, we don't require the user satisfying the ExampleItem/ExampleBlock inventory conditions for a respawn.
				return true;
			// }

			// foreach (var player in Main.ActivePlayers)
			// {
			// 	// Player has to have either an ExampleItem or an ExampleBlock in order for the NPC to spawn
			// 	if (player.inventory.Any(item => item.type == ModContent.ItemType<ExampleItem>() || item.type == ModContent.ItemType<Items.Placeable.ExampleBlock>()))
			// 	{
			// 		return true;
			// 	}
			// }

			//  return false;
		}


		//--- USED FOR DIALOGUE --- Commented out because it uses example dialogue which is not in this project.

		// public override string GetChat() {
		// 	WeightedRandom<string> chat = new WeightedRandom<string>();

		// 	int partyGirl = NPC.FindFirstNPC(NPCID.PartyGirl);
		// 	if (partyGirl >= 0 && Main.rand.NextBool(4)) {
		// 		chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.PartyGirlDialogue", Main.npc[partyGirl].GivenName));
		// 	}
		// 	// These are things that the NPC has a chance of telling you when you talk to it.
		// 	chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.StandardDialogue1"));
		// 	chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.StandardDialogue2"));
		// 	chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.StandardDialogue3"));
		// 	chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.StandardDialogue4"));
		// 	chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.CommonDialogue"), 5.0);
		// 	chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.RareDialogue"), 0.1);

		// 	NumberOfTimesTalkedTo++;
		// 	if (NumberOfTimesTalkedTo >= 10) {
		// 		// This counter is linked to a single instance of the NPC, so if ExamplePerson is killed, the counter will reset.
		// 		chat.Add(Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.TalkALot"));
		// 	}

		// 	string chosenChat = chat; // chat is implicitly cast to a string. This is where the random choice is made.

		// 	// Here is some additional logic based on the chosen chat line. In this case, we want to display an item in the corner for StandardDialogue4.
		// 	if (chosenChat == Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson.StandardDialogue4")) {
		// 		// Main.npcChatCornerItem shows a single item in the corner, like the Angler Quest chat.
		// 		Main.npcChatCornerItem = ItemID.HiveBackpack;
		// 	}

		// 	return chosenChat;
		// }

		public override void SetChatButtons(ref string button, ref string button2)
		{ // What the chat buttons are when you open up the chat UI
			button = Language.GetTextValue("LegacyInterface.28"); // This is the key to the word "Shop"
			button2 = "Awesomeify";
			if (Main.LocalPlayer.HasItem(ItemID.HiveBackpack))
			{
				button = "Upgrade " + Lang.GetItemNameValue(ItemID.HiveBackpack);
			}
		}

		// public override void SetChatButtons(ref string button, ref string button2)
		// { // What the chat buttons are when you open up the chat UI
		// 	button = Language.GetTextValue("LegacyInterface.28"); // This is the key to the word "Shop"
		// 	button2 = "Awesomeify";
		// 	if (Main.LocalPlayer.HasItem(ItemID.HiveBackpack))
		// 	{
		// 		button = "Upgrade " + Lang.GetItemNameValue(ItemID.HiveBackpack);
		// 	}
		// }

		public override void OnChatButtonClicked(bool firstButton, ref string shop)
		{
			if (firstButton)
			{
				// We want 3 different functionalities for chat buttons, so we use HasItem to change button 1 between a shop and upgrade action.

				if (Main.LocalPlayer.HasItem(ItemID.HiveBackpack))
				{
					SoundEngine.PlaySound(SoundID.Item37); // Reforge/Anvil sound

					Main.npcChatText = UpgradedText.Value;

					int hiveBackpackItemIndex = Main.LocalPlayer.FindItem(ItemID.HiveBackpack);
					var entitySource = NPC.GetSource_GiftOrReward();

					Main.LocalPlayer.inventory[hiveBackpackItemIndex].TurnToAir();
					// Main.LocalPlayer.QuickSpawnItem(entitySource, ModContent.ItemType<WaspNest>());

					return;
				}

				shop = ShopName; // Name of the shop tab we want to open.
			}
		}


		// --- NPC SHOP WITH ITEMS --- Commented out as might crash due to using example items.

		// Not completely finished, but below is what the NPC will sell
		// public override void AddShops() {
		// 	var npcShop = new NPCShop(Type, ShopName)
		// 		.Add<ExampleItem>()
		// 		//.Add<EquipMaterial>()
		// 		//.Add<BossItem>()
		// 		.Add(new Item(ModContent.ItemType<Items.Placeable.Furniture.ExampleWorkbench>()) { shopCustomPrice = Item.buyPrice(copper: 15) }) // This example sets a custom price, ExampleNPCShop.cs has more info on custom prices and currency. 
		// 		.Add<Items.Placeable.Furniture.ExampleChair>()
		// 		.Add<Items.Placeable.Furniture.ExampleDoor>()
		// 		.Add<Items.Placeable.Furniture.ExampleBed>()
		// 		.Add<Items.Placeable.Furniture.ExampleChest>()
		// 		.Add<Items.Tools.ExamplePickaxe>()
		// 		.Add<Items.Tools.ExampleHamaxe>()
		// 		.Add<Items.Consumables.ExampleHealingPotion>(new Condition("Mods.ExampleMod.Conditions.PlayerHasLifeforceBuff", () => Main.LocalPlayer.HasBuff(BuffID.Lifeforce)))
		// 		.Add<Items.Weapons.ExampleSword>(Condition.MoonPhasesQuarter0)
		// 		//.Add<ExampleGun>(Condition.MoonPhasesQuarter1)
		// 		.Add<Items.Ammo.ExampleBullet>(Condition.MoonPhasesQuarter1)
		// 		.Add<Items.Weapons.ExampleStaff>(ExampleConditions.DownedMinionBoss)
		// 		.Add<ExampleOnBuyItem>()
		// 		.Add(ItemID.AcornAxe) // Here is an example of how to sell an existing vanilla item.
		// 		.Add<Items.Weapons.ExampleYoyo>(Condition.IsNpcShimmered); // Let's sell an yoyo if this NPC is shimmered!

		// 	if (ModContent.GetInstance<ExampleModConfig>().ExampleWingsToggle) {
		// 		npcShop.Add<ExampleWings>(ExampleConditions.InExampleBiome);
		// 	}

		// 	if (ModContent.TryFind("SummonersAssociation/BloodTalisman", out ModItem bloodTalisman)) {
		// 		npcShop.Add(bloodTalisman.Type);
		// 	}
		// 	npcShop.Register(); // Name of this shop tab
		// }
	}
}