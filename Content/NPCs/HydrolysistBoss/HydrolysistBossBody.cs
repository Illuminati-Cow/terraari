using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using terraari.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.RGB;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace terraari.Content.NPCs.HydrolysistBoss;

[AutoloadBossHead]
public class HydrolysistBossBody : ModNPC
{
    // High-level AI overview
    // ai[0] = State machine selector (see AiState)
    // ai[1] = State-local timer (ticks)
    // ai[2] = Projectile id for ritual/anchor projectiles (when used)
    // ai[3] = Pattern index/phase counter OR link to leader (vanilla Cultist), context-dependent
    // localAI[0] = One-time spawn init flag
    // localAI[1] = Clone group id (vanilla Cultist behavior)
    // localAI[2] = Pose/animation hint (10, 11, 12, 13 used by vanilla sprites)

    private enum AiState
    {
        SpawnFadeIn = -1,
        IdleDecision = 0,
        MoveSequence = 1,
        IceMistVolley = 2,
        FireballBarrage = 3,
        LightningOrbAndBolts = 4,
        Ritual = 5,
        TauntPause = 6,
        AncientDoomFan = 7,
        SummonAdds = 8,
    }

    // Constants extracted for readability (behavior unchanged)
    private const float AggroRadius = 5600f;
    private const float SpreadSmall = 0.5235987901687622f; // ~30 degrees
    private const float SpreadLarge = 1.2566370964050293f; // ~72 degrees
    private const float TwoPi = (float)Math.PI * 2f;
    private const float RitualRingRadius = 180f;
    private const int RitualFadeOutTicks = 30;
    private const int RitualBetweenTicks = 60; // 30->90 window
    private const int RitualActiveStart = 120;
    private const int RitualActiveEnd = 420;

    // Small helpers (no behavior changes)
    private static List<int> GetLinkedClones(int leaderWhoAmI)
    {
        List<int> cloneIndices = new List<int>();
        for (int i = 0; i < 200; i++)
        {
            if (
                Main.npc[i].active
                && Main.npc[i].type == NPCID.CultistBossClone
                && Main.npc[i].ai[3] == leaderWhoAmI
            )
            {
                cloneIndices.Add(i);
            }
        }
        return cloneIndices;
    }

    private static void FaceHorizontallyTowards(NPC npc, Vector2 target)
    {
        int dir = Math.Sign(target.X - npc.Center.X);
        if (dir != 0)
        {
            npc.direction = npc.spriteDirection = dir;
        }
    }

    private static Vector2 SafeVector(Vector2 v, int fallbackDirX)
    {
        // Preserves original behavior: only fixes NaNs, does not normalize unless caller does
        return v.HasNaNs() ? new Vector2(fallbackDirX, 0f) : v;
    }

    public override void SetStaticDefaults()
    {
        // Adjust to match the frames of the spritesheet
        Main.npcFrameCount[NPC.type] = 16;
        // Add boss to the bestiary
        // NPCID.Sets.BossBestiaryPriority.Add(Type);
        // NPCID.Sets.NPCBestiaryDrawModifiers value = new()
        // {
        //     Scale = 1f,
        //     PortraitScale = 1f,
        //     CustomTexturePath = "terraari/ExtraTextures/Bestiary/HydrolysistBoss_Bestiary",
        // };
        // NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        // NPCID.Sets.TrailingMode[Type] = 0;
        NPCID.Sets.ImmuneToRegularBuffs[Type] = true;
    }

    public override void SetDefaults()
    {
        // TODO: Scale contact damage NPCd on difficulty
        NPC.damage = 40;
        // TODO: Set defense to a reasonable value NPCd on relative boss progression
        NPC.defense = 35;
        NPC.lifeMax = 20_000;
        NPC.HitSound = SoundID.NPCHit55;
        NPC.DeathSound = SoundID.NPCDeath59;
        NPC.alpha = 55;
        NPC.value = 50_000f;
        NPC.scale = 1.1f;
        NPC.npcSlots = 10f;
        NPC.width = 24;
        NPC.height = 50;
        // TODO: Create custom ai and remove the aiStyle
        NPC.aiStyle = -1;
        NPC.knockBackResist = 0f;
        NPC.noTileCollide = true;
        NPC.noGravity = true;
        NPC.boss = true;
        NPC.netAlways = true;
        NPC.SpawnWithHigherTime(30);
    }

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        // use the boss immunity cooldown counter, to prevent ignoring boss attacks by taking damage from other sources
        cooldownSlot = ImmunityCooldownID.Bosses;
        return true;
    }

    public override void FindFrame(int frameHeight)
    {
        // This NPC animates with a simple "go from start frame to final frame, and loop back to start frame" rule
        int startFrame = 3;
        int finalFrame = 6;

        int frameSpeed = 5;
        NPC.frameCounter += 0.5f;
        if (NPC.frameCounter > frameSpeed)
        {
            NPC.frameCounter = 0;
            NPC.frame.Y += frameHeight;

            if (NPC.frame.Y > finalFrame * frameHeight)
            {
                NPC.frame.Y = startFrame * frameHeight;
            }
        }
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        // If the NPC dies, spawn gore and play a sound
        if (Main.netMode == NetmodeID.Server)
        {
            // We don't want Mod.Find<ModGore> to run on servers as it will crash because gores are not loaded on servers
            return;
        }

        // TODO: Add gore on death
        return;

        if (NPC.life <= 0)
        {
            // These gores work by simply existing as a texture inside any folder which path contains "Gores/"
            int backGoreType = Mod.Find<ModGore>("MinionBossBody_Back").Type;
            int frontGoreType = Mod.Find<ModGore>("MinionBossBody_Front").Type;

            var entitySource = NPC.GetSource_Death();

            for (int i = 0; i < 2; i++)
            {
                Gore.NewGore(
                    entitySource,
                    NPC.position,
                    new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)),
                    backGoreType
                );
                Gore.NewGore(
                    entitySource,
                    NPC.position,
                    new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)),
                    frontGoreType
                );
            }

            // SoundEngine.PlaySound(SoundID.Roar, NPC.Center);

            // This adds a screen shake (screenshake) similar to Deerclops
            var modifier = new PunchCameraModifier(
                NPC.Center,
                (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(),
                20f,
                6f,
                20,
                1000f,
                FullName
            );
            Main.instance.CameraModifiers.Add(modifier);
        }
    }

    public override void OnKill()
    {
        if (!DownedBossSystem.downedHydrolysistBoss)
        {
            // Do something unique when the boss is first killed
        }
        NPC.SetEventFlagCleared(ref DownedBossSystem.downedHydrolysistBoss, -1);
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        // The order in which you add loot will appear as such in the Bestiary. To mirror vanilla boss order:
        // 1. Trophy
        // 2. Classic Mode ("not expert")
        // 3. Expert Mode (usually just the treasure bag)
        // 4. Master Mode (relic first, pet last, everything else in between)

        // TODO: Replace with Hydrolysist trophy
        npcLoot.Add(ItemDropRule.Common(ItemID.LunaticCultistMasterTrophy, 10));
        var commonLoot = new LeadingConditionRule(new Conditions.NotExpert());
        commonLoot.OnSuccess(ItemDropRule.Common(ItemID.BottomlessShimmerBucket));
        npcLoot.Add(commonLoot);
        // TODO: Replace with Hydrolysist bag
        npcLoot.Add(ItemDropRule.BossBag(ItemID.CultistBossBag));
        // TODO: Replace with Hydrolysist relic
        npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ItemID.LunaticCultistMasterTrophy));
    }

    public override void BossLoot(ref int potionType)
    {
        // TODO: Update to match boss progression
        potionType = ItemID.GreaterHealingPotion;
    }

    public override void AI()
    {
        // Aliases for AI indices
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiAnchorProj = ref NPC.ai[2];
        ref float aiPhase = ref NPC.ai[3];
        ref float localInit = ref NPC.localAI[0];
        ref float cloneGroupId = ref NPC.localAI[1];
        ref float poseHint = ref NPC.localAI[2];

        // Optional rare sound cue every ~1000 ticks when not in spawn fade-in
        if (aiState != -1f && Main.rand.Next(1000) == 0)
        {
            // SoundEngine.PlaySound(29, (int)NPC.position.X, (int)NPC.position.Y, Main.rand.Next(88, 92));
        }
        // Difficulty context
        bool expertMode = Main.expertMode;
        bool lessThanHalfHP = NPC.life <= NPC.lifeMax / 2;

        // Base damages of the vanilla projectiles this AI uses. tModLoader scales these appropriately.
        int iceMistDamage = NPC.GetAttackDamage_ForProjectiles(35f, 25f);
        int fireballDamage = NPC.GetAttackDamage_ForProjectiles(30f, 20f);
        int lightningOrbDamage = NPC.GetAttackDamage_ForProjectiles(45f, 30f);

        // Timings (ticks) and repeats, tuned by difficulty/world
        int attackPatternDelay = 120; // Ice mist volley cadence baseline
        if (expertMode)
            attackPatternDelay = 90;
        if (Main.getGoodWorld)
            attackPatternDelay -= 30;

        int fireBurstInterval = 18; // Interval between fireball barrages
        int fireBurstRepeats = 3; // Number of barrages
        if (expertMode)
        {
            fireBurstInterval = 12;
            fireBurstRepeats = 4;
        }
        if (Main.getGoodWorld)
        {
            fireBurstInterval = 10;
            fireBurstRepeats = 5;
        }

        int lightningWindow = 80; // Window length that spawns a lightning orb once
        if (expertMode)
            lightningWindow = 40;
        if (Main.getGoodWorld)
            lightningWindow -= 20;

        int doomInterval = 20; // "Ancient Doom" spawn cadence
        int doomRepeats = 2; // Number of doom fan waves
        if (expertMode)
        {
            doomInterval = 30;
            doomRepeats = 2;
        }

        int summonInterval = 20; // Summon add check cadence
        int summonRepeats = 3; // Max summon bursts

        // Flags reflecting vanilla Cultist multi-entity behavior
        // Treat this modded boss as the "leader" to avoid proxying state from a missing vanilla Cultist (which causes immediate despawn).
        bool isLeaderCultist = true;
        bool isInvulnerable = false; // When true: NPC.dontTakeDamage = true
        bool isUnchaseable = false; // When true: NPC.chaseable = false (targeting off)
        if (lessThanHalfHP)
        {
            NPC.defense = (int)(NPC.defDefense * 0.65f);
        }
        if (!isLeaderCultist)
        {
            if (
                aiPhase < 0f
                || !Main.npc[(int)aiPhase].active
                || Main.npc[(int)aiPhase].type != NPCID.CultistBoss
            )
            {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.active = false;
                return;
            }
            aiState = Main.npc[(int)aiPhase].ai[0];
            aiTimer = Main.npc[(int)aiPhase].ai[1];
            if (aiState == 5f)
            {
                if (NPC.justHit)
                {
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.active = false;
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
                    }
                    NPC obj = Main.npc[(int)aiPhase];
                    obj.ai[0] = 6f;
                    obj.ai[1] = 0f;
                    obj.netUpdate = true;
                }
            }
            else
            {
                isInvulnerable = true;
                isUnchaseable = true;
            }
        }
        else if (aiState == 5f && aiTimer >= 120f && aiTimer < 420f && NPC.justHit)
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
            List<int> list = new List<int>();
            for (int i = 0; i < 200; i++)
            {
                if (
                    Main.npc[i].active
                    && Main.npc[i].type == NPCID.CultistBossClone
                    && Main.npc[i].ai[3] == NPC.whoAmI
                )
                {
                    list.Add(i);
                }
            }
            int num9 = 10;
            if (Main.expertMode)
            {
                num9 = 3;
            }
            foreach (int item in list)
            {
                NPC nPC = Main.npc[item];
                if (nPC.localAI[1] == cloneGroupId && num9 > 0)
                {
                    num9--;
                    nPC.life = 0;
                    nPC.HitEffect();
                    nPC.active = false;
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, item);
                    }
                }
                else if (num9 > 0)
                {
                    num9--;
                    nPC.life = 0;
                    nPC.HitEffect();
                    nPC.active = false;
                }
            }
            Main.projectile[(int)aiAnchorProj].ai[1] = -1f;
            Main.projectile[(int)aiAnchorProj].netUpdate = true;
        }
        Vector2 center = NPC.Center;
        Player player = Main.player[NPC.target];
        if (
            NPC.target < 0
            || NPC.target == 255
            || player.dead
            || !player.active
            || Vector2.Distance(player.Center, center) > AggroRadius
        )
        {
            NPC.TargetClosest(faceTarget: false);
            player = Main.player[NPC.target];
            NPC.netUpdate = true;
        }
        if (player.dead || !player.active || Vector2.Distance(player.Center, center) > AggroRadius)
        {
            NPC.life = 0;
            NPC.HitEffect();
            NPC.active = false;
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, NPC.whoAmI, -1f);
            }
            new List<int>().Add(NPC.whoAmI);
            for (int j = 0; j < 200; j++)
            {
                if (
                    !Main.npc[j].active
                    || Main.npc[j].type != NPCID.CultistBossClone
                    || Main.npc[j].ai[3] != NPC.whoAmI
                )
                {
                    continue;
                }

                Main.npc[j].life = 0;
                Main.npc[j].HitEffect();
                Main.npc[j].active = false;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, NPC.whoAmI, -1f);
                }
            }
        }
        // Preserve the original ai[3] so we can restore after proxying through a leader
        float savedAi3 = aiPhase;
        if (localInit == 0f)
        {
            // SoundEngine.PlaySound(29, (int)NPC.position.X, (int)NPC.position.Y, 89);
            localInit = 1f;
            NPC.alpha = 255;
            NPC.rotation = 0f;
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                aiState = -1f;
                NPC.netUpdate = true;
            }
        }
        if (aiState == -1f)
        {
            NPC.alpha -= 5;
            if (NPC.alpha < 0)
            {
                NPC.alpha = 0;
            }
            aiTimer += 1f;
            if (aiTimer >= 420f)
            {
                aiState = 0f;
                aiTimer = 0f;
                NPC.netUpdate = true;
            }
            else if (aiTimer > 360f)
            {
                NPC.velocity *= 0.95f;
                if (poseHint != 13f)
                {
                    // SoundEngine.PlaySound(in new SoundStyle(29), NPC.position, 105);
                }
                poseHint = 13f;
            }
            else if (aiTimer > 300f)
            {
                NPC.velocity = -Vector2.UnitY;
                poseHint = 10f;
            }
            else if (aiTimer > 120f)
            {
                poseHint = 1f;
            }
            else
            {
                poseHint = 0f;
            }
            isInvulnerable = true;
            isUnchaseable = true;
        }
        // Main AI state machine
        switch ((AiState)(int)aiState)
        {
            // Idle/decision state: pick and transition into the next attack pattern
            case AiState.IdleDecision:
            {
                TickIdleDecision(
                    player,
                    center,
                    lessThanHalfHP,
                    expertMode,
                    isLeaderCultist,
                    attackPatternDelay
                );

                break;
            }
            // Movement wind-up/cooldown utility state used by several patterns
            case AiState.MoveSequence:
            {
                TickMoveSequence(ref isInvulnerable);

                break;
            }
            // Ice mist volley (periodic aimed projectiles)
            case AiState.IceMistVolley:
            {
                TickIceMistVolley(
                    player,
                    center,
                    isLeaderCultist,
                    attackPatternDelay,
                    iceMistDamage
                );

                break;
            }
            // Fireball barrage: several aimed bursts from boss and clones
            case AiState.FireballBarrage:
            {
                TickFireballBarrage(
                    player,
                    center,
                    isLeaderCultist,
                    fireBurstInterval,
                    fireBurstRepeats,
                    fireballDamage
                );

                break;
            }
            // Lightning orb phase: spawns a charging orb after allied clone volley
            case AiState.LightningOrbAndBolts:
            {
                TickLightningOrbAndBolts(
                    player,
                    isLeaderCultist,
                    lightningWindow,
                    lightningOrbDamage
                );

                break;
            }
            // Ritual phase: fade-out -> ritual circle -> fade-in & sustained tracking
            case AiState.Ritual:
            {
                TickRitual(player, isLeaderCultist, ref isInvulnerable, ref isUnchaseable);

                break;
            }
            // Short taunt/pause state
            case AiState.TauntPause:
            {
                TickTauntPause();

                break;
            }
            // Ancient Doom fanned waves from boss and clones
            case AiState.AncientDoomFan:
            {
                TickAncientDoomFan(player, center, isLeaderCultist, doomInterval, doomRepeats);

                break;
            }
            // Summon adds around the player at random valid tiles
            case AiState.SummonAdds:
            {
                TickSummonAdds(player, center, isLeaderCultist, summonInterval, summonRepeats);

                break;
            }
        }
        if (!isLeaderCultist)
        {
            aiPhase = savedAi3;
        }
        NPC.dontTakeDamage = isInvulnerable;
        NPC.chaseable = !isUnchaseable;
    }

    // State implementations (extracted from switch for readability)
    private void TickIdleDecision(
        Player player,
        Vector2 center,
        bool lessThanHalfHP,
        bool expertMode,
        bool isLeaderCultist,
        int attackPatternDelay
    )
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiAnchorProj = ref NPC.ai[2];
        ref float aiPhase = ref NPC.ai[3];
        ref float poseHint = ref NPC.localAI[2];

        if (aiTimer == 0f)
        {
            NPC.TargetClosest(faceTarget: false);
        }
        poseHint = 10f;
        FaceHorizontallyTowards(NPC, player.Center);
        aiTimer += 1f;
        if (aiTimer < 40f || !isLeaderCultist)
            return;

        int nextAction = 0;
        if (lessThanHalfHP)
        {
            switch ((int)aiPhase)
            {
                case 0:
                    nextAction = 0;
                    break;
                case 1:
                    nextAction = 1;
                    break;
                case 2:
                    nextAction = 0;
                    break;
                case 3:
                    nextAction = 5;
                    break; // Ritual
                case 4:
                    nextAction = 0;
                    break;
                case 5:
                    nextAction = 3;
                    break; // Fireball barrage
                case 6:
                    nextAction = 0;
                    break;
                case 7:
                    nextAction = 5;
                    break; // Ritual
                case 8:
                    nextAction = 0;
                    break;
                case 9:
                    nextAction = 2;
                    break; // Ice mist volley
                case 10:
                    nextAction = 0;
                    break;
                case 11:
                    nextAction = 3;
                    break; // Fireball barrage
                case 12:
                    nextAction = 0;
                    break;
                case 13:
                    nextAction = 4; // Lightning orb
                    aiPhase = -1f;
                    break;
                default:
                    aiPhase = -1f;
                    break;
            }
        }
        else
        {
            switch ((int)aiPhase)
            {
                case 0:
                    nextAction = 0;
                    break;
                case 1:
                    nextAction = 1;
                    break; // Move sequence
                case 2:
                    nextAction = 0;
                    break;
                case 3:
                    nextAction = 2;
                    break; // Ice mist volley
                case 4:
                    nextAction = 0;
                    break;
                case 5:
                    nextAction = 3;
                    break; // Fireball barrage
                case 6:
                    nextAction = 0;
                    break;
                case 7:
                    nextAction = 1;
                    break; // Move sequence
                case 8:
                    nextAction = 0;
                    break;
                case 9:
                    nextAction = 2;
                    break; // Ice mist volley
                case 10:
                    nextAction = 0;
                    break;
                case 11:
                    nextAction = 4; // Lightning orb
                    aiPhase = -1f;
                    break;
                default:
                    aiPhase = -1f;
                    break;
            }
        }

        int rareSummonRollMax = 6;
        if (NPC.life < NPC.lifeMax / 3)
            rareSummonRollMax = 4;
        if (NPC.life < NPC.lifeMax / 4)
            rareSummonRollMax = 3;
        if (
            expertMode
            && lessThanHalfHP
            && Main.rand.Next(rareSummonRollMax) == 0
            && nextAction != 0
            && nextAction != 4
            && nextAction != 5
            && NPC.CountNPCS(523) < 10
        )
        {
            nextAction = 6; // Summon adds
        }

        if (nextAction == 0)
        {
            float steps = (float)
                Math.Ceiling((player.Center + new Vector2(0f, -100f) - center).Length() / 50f);
            if (steps == 0f)
                steps = 1f;
            List<int> group = new List<int>();
            int indexInGroup = 0;
            group.Add(NPC.whoAmI);
            for (int k = 0; k < 200; k++)
            {
                if (
                    Main.npc[k].active
                    && Main.npc[k].type == NPCID.CultistBossClone
                    && Main.npc[k].ai[3] == (float)NPC.whoAmI
                )
                {
                    group.Add(k);
                }
            }
            bool evenCount = group.Count % 2 == 0;
            foreach (int idx in group)
            {
                NPC n = Main.npc[idx];
                Vector2 c = n.Center;
                float angle =
                    (indexInGroup + evenCount.ToInt() + 1) / 2 * TwoPi * 0.4f / group.Count;
                if (indexInGroup % 2 == 1)
                    angle *= -1f;
                if (group.Count == 1)
                    angle = 0f;
                Vector2 orbit = new Vector2(0f, -1f).RotatedBy(angle) * new Vector2(300f, 200f);
                Vector2 toPos = player.Center + orbit - c;
                n.ai[0] = 1f;
                n.ai[1] = steps * 2f;
                n.velocity = toPos / steps;
                if (NPC.whoAmI >= n.whoAmI)
                {
                    n.position -= n.velocity;
                }
                n.netUpdate = true;
                indexInGroup++;
            }
        }

        switch (nextAction)
        {
            case 1:
                aiState = 3f;
                aiTimer = 0f;
                break;
            case 2:
                aiState = 2f;
                aiTimer = 0f;
                break;
            case 3:
                aiState = 4f;
                aiTimer = 0f;
                break;
            case 4:
                aiState = 5f;
                aiTimer = 0f;
                break;
        }
        if (nextAction == 5)
        {
            aiState = 7f;
            aiTimer = 0f;
        }
        if (nextAction == 6)
        {
            aiState = 8f;
            aiTimer = 0f;
        }
        NPC.netUpdate = true;
    }

    private void TickMoveSequence(ref bool isInvulnerable)
    {
        isInvulnerable = true;
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = 10f;
        if ((float)(int)aiTimer % 2f != 0f && aiTimer != 1f)
        {
            NPC.position -= NPC.velocity;
        }
        aiTimer -= 1f;
        if (aiTimer <= 0f)
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    private void TickIceMistVolley(
        Player player,
        Vector2 center,
        bool isLeaderCultist,
        int attackPatternDelay,
        int iceMistDamage
    )
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = 11f;
        Vector2 vec = Vector2.Normalize(player.Center - center);
        vec = SafeVector(vec, NPC.direction);

        if (aiTimer >= 4f && isLeaderCultist && (int)(aiTimer - 4f) % attackPatternDelay == 0)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                List<int> clones = GetLinkedClones(NPC.whoAmI);
                foreach (int idx in clones)
                {
                    NPC clone = Main.npc[idx];
                    FaceHorizontallyTowards(clone, player.Center);
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        continue;
                    Vector2 aim = Vector2.Normalize(
                        player.Center - clone.Center + player.velocity * 20f
                    );
                    if (aim.HasNaNs())
                        aim = new Vector2(NPC.direction, 0f);
                    Vector2 firePoint = clone.Center + new Vector2(NPC.direction * 30, 12f);
                    Vector2 shotVel = aim * (6f + (float)Main.rand.NextDouble() * 4f);
                    shotVel = shotVel.RotatedByRandom(SpreadSmall);
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        firePoint.X,
                        firePoint.Y,
                        shotVel.X,
                        shotVel.Y,
                        ProjectileID.CultistBossFireBallClone,
                        18,
                        0f,
                        Main.myPlayer
                    );
                }
            }
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 aim = Vector2.Normalize(player.Center - center + player.velocity * 20f);
                aim = SafeVector(aim, NPC.direction);

                Vector2 firePoint = NPC.Center + new Vector2(NPC.direction * 30, 12f);
                Vector2 vel = aim * 4f;
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    firePoint.X,
                    firePoint.Y,
                    vel.X,
                    vel.Y,
                    ProjectileID.CultistBossIceMist,
                    iceMistDamage,
                    0f,
                    Main.myPlayer,
                    0f,
                    1f
                );
            }
        }
        aiTimer += 1f;
        if (aiTimer >= (float)(4 + attackPatternDelay))
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    private void TickFireballBarrage(
        Player player,
        Vector2 center,
        bool isLeaderCultist,
        int fireBurstInterval,
        int fireBurstRepeats,
        int fireballDamage
    )
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = 11f;
        Vector2 dirToPlayer = Vector2.Normalize(player.Center - center);
        if (dirToPlayer.HasNaNs())
            dirToPlayer = new Vector2(NPC.direction, 0f);

        if (aiTimer >= 4f && isLeaderCultist && (int)(aiTimer - 4f) % fireBurstInterval == 0)
        {
            if ((int)(aiTimer - 4f) / fireBurstInterval == 2)
            {
                List<int> clones = GetLinkedClones(NPC.whoAmI);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    foreach (int idx in clones)
                    {
                        NPC c = Main.npc[idx];
                        FaceHorizontallyTowards(c, player.Center);
                        if (Main.netMode == NetmodeID.MultiplayerClient)
                            continue;
                        // Server Code
                        Vector2 aim = Vector2.Normalize(
                            player.Center - c.Center + player.velocity * 20f
                        );
                        if (aim.HasNaNs())
                            aim = new Vector2(NPC.direction, 0f);
                        Vector2 firePoint = c.Center + new Vector2(NPC.direction * 30, 12f);
                        Vector2 shotVel = aim * (6f + (float)Main.rand.NextDouble() * 4f);
                        shotVel = shotVel.RotatedByRandom(SpreadSmall);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            firePoint.X,
                            firePoint.Y,
                            shotVel.X,
                            shotVel.Y,
                            ProjectileID.CultistBossFireBallClone,
                            18,
                            0f,
                            Main.myPlayer
                        );
                    }
                }
            }
            FaceHorizontallyTowards(NPC, player.Center);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 aim = Vector2.Normalize(player.Center - center + player.velocity * 20f);
                if (aim.HasNaNs())
                    aim = new Vector2(NPC.direction, 0f);
                Vector2 firePoint = NPC.Center + new Vector2(NPC.direction * 30, 12f);
                Vector2 shotVel = aim * (6f + (float)Main.rand.NextDouble() * 4f);
                shotVel = shotVel.RotatedByRandom(SpreadSmall);
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    firePoint.X,
                    firePoint.Y,
                    shotVel.X,
                    shotVel.Y,
                    ProjectileID.CultistBossFireBall,
                    fireballDamage,
                    0f,
                    Main.myPlayer
                );
            }
        }
        aiTimer += 1f;
        if (aiTimer >= (float)(4 + fireBurstInterval * fireBurstRepeats))
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    private void TickLightningOrbAndBolts(
        Player player,
        bool isLeaderCultist,
        int lightningWindow,
        int lightningOrbDamage
    )
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float aiAnchorProj = ref NPC.ai[2];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = isLeaderCultist ? 12f : 11f;
        if (aiTimer == 20f && isLeaderCultist && Main.netMode != NetmodeID.MultiplayerClient)
        {
            foreach (int idx in GetLinkedClones(NPC.whoAmI))
            {
                NPC n = Main.npc[idx];
                FaceHorizontallyTowards(n, player.Center);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    continue;
                Vector2 aim = Vector2.Normalize(player.Center - n.Center + player.velocity * 20f);
                if (aim.HasNaNs())
                    aim = new Vector2(NPC.direction, 0f);
                Vector2 firePoint = n.Center + new Vector2(NPC.direction * 30, 12f);
                Vector2 vel = aim * (6f + (float)Main.rand.NextDouble() * 4f);
                vel = vel.RotatedByRandom(SpreadSmall);
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    firePoint.X,
                    firePoint.Y,
                    vel.X,
                    vel.Y,
                    ProjectileID.CultistBossFireBallClone,
                    18,
                    0f,
                    Main.myPlayer
                );
            }
            if ((int)(aiTimer - 20f) % lightningWindow == 0)
            {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center.X,
                    NPC.Center.Y - 100f,
                    0f,
                    0f,
                    ProjectileID.CultistBossLightningOrb,
                    lightningOrbDamage,
                    0f,
                    Main.myPlayer
                );
            }
        }
        aiTimer += 1f;
        if (aiTimer >= (float)(20 + lightningWindow))
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    private void TickRitual(
        Player player,
        bool isLeaderCultist,
        ref bool isInvulnerable,
        ref bool isUnchaseable
    )
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float aiAnchorProj = ref NPC.ai[2];
        ref float cloneGroupId = ref NPC.localAI[1];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = 10f;
        Vector2 tmp = Vector2.Normalize(player.Center - NPC.Center);
        if (tmp.HasNaNs())
            _ = new Vector2(NPC.direction, 0f);

        if (aiTimer >= 0f && aiTimer < RitualFadeOutTicks)
        {
            isInvulnerable = true;
            isUnchaseable = true;
            float t = (aiTimer - 0f) / RitualFadeOutTicks;
            NPC.alpha = (int)(t * 255f);
        }
        else if (aiTimer >= RitualFadeOutTicks && aiTimer < RitualFadeOutTicks + RitualBetweenTicks)
        {
            if (
                aiTimer == RitualFadeOutTicks
                && Main.netMode != NetmodeID.MultiplayerClient
                && isLeaderCultist
            )
            {
                cloneGroupId += 1f;
                Vector2 ring = new Vector2(RitualRingRadius, 0f);
                List<int> clones = GetLinkedClones(NPC.whoAmI);
                int toSpawn = 6 - clones.Count;
                if (toSpawn > 2)
                    toSpawn = 2;
                int total = clones.Count + toSpawn + 1;
                float[] distances = new float[total];
                for (int i = 0; i < distances.Length; i++)
                {
                    distances[i] = Vector2.Distance(
                        NPC.Center + ring.RotatedBy((float)i * TwoPi / total - (float)Math.PI / 2f),
                        player.Center
                    );
                }
                int furthest = 0;
                for (int i = 1; i < distances.Length; i++)
                {
                    if (distances[furthest] > distances[i])
                        furthest = i;
                }
                furthest =
                    (furthest >= total / 2) ? (furthest - total / 2) : (furthest + total / 2);
                int remainingToSpawn = toSpawn;
                for (int i = 0; i < distances.Length; i++)
                {
                    if (furthest == i)
                        continue;
                    Vector2 pos =
                        NPC.Center + ring.RotatedBy((float)i * TwoPi / total - (float)Math.PI / 2f);
                    if (remainingToSpawn-- > 0)
                    {
                        int idx = NPC.NewNPC(
                            NPC.GetSource_FromAI(),
                            (int)pos.X,
                            (int)pos.Y + NPC.height / 2,
                            440,
                            NPC.whoAmI
                        );
                        Main.npc[idx].ai[3] = NPC.whoAmI;
                        Main.npc[idx].netUpdate = true;
                        Main.npc[idx].localAI[1] = cloneGroupId;
                    }
                    else
                    {
                        int reuseIndex = clones[-(remainingToSpawn) - 1];
                        Main.npc[reuseIndex].Center = pos;
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, reuseIndex);
                    }
                }
                aiAnchorProj = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center.X,
                    NPC.Center.Y,
                    0f,
                    0f,
                    ProjectileID.CultistRitual,
                    0,
                    0f,
                    Main.myPlayer,
                    0f,
                    NPC.whoAmI
                );
                NPC.Center += ring.RotatedBy((float)furthest * TwoPi / total - (float)Math.PI / 2f);
                NPC.netUpdate = true;
                clones.Clear();
            }
            isInvulnerable = true;
            isUnchaseable = true;
            NPC.alpha = 255;
            if (isLeaderCultist)
            {
                Vector2 toRitual = Main.projectile[(int)aiAnchorProj].Center - NPC.Center;
                if (toRitual == Vector2.Zero)
                    toRitual = -Vector2.UnitY;
                toRitual.Normalize();
                if (Math.Abs(toRitual.Y) < 0.77f)
                    poseHint = 11f;
                else if (toRitual.Y < 0f)
                    poseHint = 12f;
                else
                    poseHint = 10f;
                int dir = Math.Sign(toRitual.X);
                if (dir != 0)
                    NPC.direction = (NPC.spriteDirection = dir);
            }
            else
            {
                Vector2 toRitual =
                    Main.projectile[(int)Main.npc[(int)aiPhase].ai[2]].Center - NPC.Center;
                if (toRitual == Vector2.Zero)
                    toRitual = -Vector2.UnitY;
                toRitual.Normalize();
                if (Math.Abs(toRitual.Y) < 0.77f)
                    poseHint = 11f;
                else if (toRitual.Y < 0f)
                    poseHint = 12f;
                else
                    poseHint = 10f;
                int dir = Math.Sign(toRitual.X);
                if (dir != 0)
                    NPC.direction = (NPC.spriteDirection = dir);
            }
        }
        else if (aiTimer >= 90f && aiTimer < RitualActiveStart)
        {
            isInvulnerable = true;
            isUnchaseable = true;
            float t = (aiTimer - 90f) / 30f;
            NPC.alpha = 255 - (int)(t * 255f);
        }
        else if (aiTimer >= RitualActiveStart && aiTimer < RitualActiveEnd)
        {
            isUnchaseable = true;
            NPC.alpha = 0;
            if (isLeaderCultist)
            {
                Vector2 toRitual = Main.projectile[(int)aiAnchorProj].Center - NPC.Center;
                if (toRitual == Vector2.Zero)
                    toRitual = -Vector2.UnitY;
                toRitual.Normalize();
                if (Math.Abs(toRitual.Y) < 0.77f)
                    poseHint = 11f;
                else if (toRitual.Y < 0f)
                    poseHint = 12f;
                else
                    poseHint = 10f;
                int dir = Math.Sign(toRitual.X);
                if (dir != 0)
                    NPC.direction = (NPC.spriteDirection = dir);
            }
            else
            {
                Vector2 toRitual =
                    Main.projectile[(int)Main.npc[(int)aiPhase].ai[2]].Center - NPC.Center;
                if (toRitual == Vector2.Zero)
                    toRitual = -Vector2.UnitY;
                toRitual.Normalize();
                if (Math.Abs(toRitual.Y) < 0.77f)
                    poseHint = 11f;
                else if (toRitual.Y < 0f)
                    poseHint = 12f;
                else
                    poseHint = 10f;
                int dir = Math.Sign(toRitual.X);
                if (dir != 0)
                    NPC.direction = (NPC.spriteDirection = dir);
            }
        }
        aiTimer += 1f;
        if (aiTimer >= RitualActiveEnd)
        {
            isUnchaseable = true;
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    private void TickTauntPause()
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = 13f;
        aiTimer += 1f;
        if (aiTimer >= 120f)
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    private void TickAncientDoomFan(
        Player player,
        Vector2 center,
        bool isLeaderCultist,
        int doomInterval,
        int doomRepeats
    )
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = 11f;
        Vector2 playerDirection = Vector2.Normalize(player.Center - center);
        if (playerDirection.HasNaNs())
            playerDirection = new Vector2(NPC.direction, 0f);
        if (aiTimer >= 4f && isLeaderCultist && (int)(aiTimer - 4f) % doomInterval == 0)
        {
            if ((int)(aiTimer - 4f) / doomInterval == 2)
            {
                List<int> clones = GetLinkedClones(NPC.whoAmI);
                foreach (int idx in clones)
                {
                    NPC n = Main.npc[idx];
                    FaceHorizontallyTowards(n, player.Center);
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        continue;
                    Vector2 aim = Vector2.Normalize(
                        player.Center - n.Center + player.velocity * 20f
                    );
                    if (aim.HasNaNs())
                        aim = new Vector2(NPC.direction, 0f);
                    Vector2 firePoint = n.Center + new Vector2(NPC.direction * 30, 12f);
                    for (int j = 0; (float)j < 5f; j++)
                    {
                        Vector2 vel = aim * (6f + (float)Main.rand.NextDouble() * 4f);
                        vel = vel.RotatedByRandom(SpreadLarge);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            firePoint.X,
                            firePoint.Y,
                            vel.X,
                            vel.Y,
                            ProjectileID.CultistBossFireBallClone,
                            18,
                            0f,
                            Main.myPlayer
                        );
                    }
                }
            }
            FaceHorizontallyTowards(NPC, player.Center);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                playerDirection = Vector2.Normalize(player.Center - center + player.velocity * 20f);
                if (playerDirection.HasNaNs())
                    playerDirection = new Vector2(NPC.direction, 0f);
                Vector2 spawn = NPC.Center + new Vector2(NPC.direction * 30, 12f);
                float speed = 8f;
                float step = TwoPi / 25f;
                for (int i = 0; (float)i < 5f; i++)
                {
                    Vector2 vel = playerDirection * speed;
                    vel = vel.RotatedBy(step * i - (TwoPi / 5f - step) / 2f);
                    float ai = (Main.rand.NextFloat() - 0.5f) * 0.3f * TwoPi / 60f;
                    int idx = NPC.NewNPC(
                        NPC.GetSource_FromAI(),
                        (int)spawn.X,
                        (int)spawn.Y + 7,
                        522,
                        0,
                        0f,
                        ai,
                        vel.X,
                        vel.Y
                    );
                    Main.npc[idx].velocity = vel;
                    Main.npc[idx].netUpdate = true;
                }
            }
        }
        aiTimer += 1f;
        if (aiTimer >= (float)(4 + doomInterval * doomRepeats))
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }

    private void TickSummonAdds(
        Player player,
        Vector2 center,
        bool isLeaderCultist,
        int summonInterval,
        int summonRepeats
    )
    {
        ref float aiState = ref NPC.ai[0];
        ref float aiTimer = ref NPC.ai[1];
        ref float aiPhase = ref NPC.ai[3];
        ref float poseHint = ref NPC.localAI[2];

        poseHint = 13f;
        if (aiTimer >= 4f && isLeaderCultist && (int)(aiTimer - 4f) % summonInterval == 0)
        {
            List<int> clones = GetLinkedClones(NPC.whoAmI);
            int count = clones.Count + 1;
            if (count > 3)
                count = 3;
            FaceHorizontallyTowards(NPC, player.Center);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < count; i++)
                {
                    Point selfTile = NPC.Center.ToTileCoordinates();
                    Point playerTile = Main.player[NPC.target].Center.ToTileCoordinates();
                    Vector2 delta = Main.player[NPC.target].Center - NPC.Center;
                    int range = 20;
                    int selfExcludeRadius = 3;
                    int playerExcludeRadius = 7;
                    int solidProbeRadius = 2;
                    int attempts = 0;
                    bool tooFar = delta.Length() > 2000f;
                    while (!tooFar && attempts < 100)
                    {
                        attempts++;
                        int tx = Main.rand.Next(playerTile.X - range, playerTile.X + range + 1);
                        int ty = Main.rand.Next(playerTile.Y - range, playerTile.Y + range + 1);
                        bool inPlayerBox =
                            ty >= playerTile.Y - playerExcludeRadius
                            && ty <= playerTile.Y + playerExcludeRadius
                            && tx >= playerTile.X - playerExcludeRadius
                            && tx <= playerTile.X + playerExcludeRadius;
                        bool inSelfBox =
                            ty >= selfTile.Y - selfExcludeRadius
                            && ty <= selfTile.Y + selfExcludeRadius
                            && tx >= selfTile.X - selfExcludeRadius
                            && tx <= selfTile.X + selfExcludeRadius;
                        if (inPlayerBox || inSelfBox || Main.tile[tx, ty].HasUnactuatedTile)
                            continue;

                        bool free = !(
                            Collision.SolidTiles(
                                tx - solidProbeRadius,
                                tx + solidProbeRadius,
                                ty - solidProbeRadius,
                                ty + solidProbeRadius
                            )
                        );
                        if (!free)
                            continue;
                        NPC.NewNPC(
                            NPC.GetSource_FromAI(),
                            tx * 16 + 8,
                            ty * 16 + 8,
                            523,
                            0,
                            NPC.whoAmI
                        );
                        break;
                    }
                }
            }
        }
        aiTimer += 1f;
        if (aiTimer >= (float)(4 + summonInterval * summonRepeats))
        {
            aiState = 0f;
            aiTimer = 0f;
            aiPhase += 1f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }
    }
}
