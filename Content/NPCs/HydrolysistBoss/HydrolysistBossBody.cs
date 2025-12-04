using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraari.Common.StateMachine;
using Terraari.Common.Systems;
using Terraari.Content.Buffs;
using Terraari.Content.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using AnimationFrameData = Terraria.Animation.AnimationFrameData;
using ShaderHelper = Terraari.Common.Helpers.ShaderHelper;
using ShimmerHelper = Terraari.Common.Helpers.ShimmerHelper;

namespace terraari.Content.NPCs.HydrolysistBoss;

[AutoloadBossHead]
public class HydrolysistBossBody : ModNPC
{
    private StateMachine<HydrolysistContext> stateMachine;
    private Effect shader;

    public float Timer
    {
        get => NPC.ai[1];
        set => NPC.ai[1] = value;
    }

    public float Phase
    {
        get => NPC.ai[2];
        set => NPC.ai[2] = value;
    }

    public AnimationFrameData CurrentAnimation;

    private const int TeleportTrailDuration = 60;
    private int teleportTrailTimer;

    private void SyncState()
    {
        NPC.ai[0] = stateMachine.GetSerializedState();
        NPC.netUpdate = true;
    }

    private static void FaceHorizontallyTowards(NPC npc, Vector2 target)
    {
        int dir = Math.Sign(npc.Center.X - target.X);
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

    private void DebugPrint(string msg)
    {
        CombatText.NewText(
            new Rectangle((int)NPC.position.X, (int)NPC.position.Y, 100, 100),
            Color.White,
            msg
        );
    }

    public override void SetStaticDefaults()
    {
        // Adjust to match the frames of the spritesheet
        Main.npcFrameCount[NPC.type] = 13;
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

        NPCID.Sets.TrailCacheLength[Type] = 20; // how many old positions we store
        NPCID.Sets.TrailingMode[Type] = 3; //afterimage style
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

        shader = ShaderHelper.SetUpShimmerShader();
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[NPC.type].Value;
        Rectangle frame = NPC.frame;
        Vector2 origin = frame.Size() / 2f;
        SpriteEffects effects =
            NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        // Draw the trail / afterimages
        if (teleportTrailTimer > 0 && NPC.oldPos != null && NPC.oldPos.Length > 0)
        {
            // How strong the whole trail should be this frame
            float trailStrength = teleportTrailTimer / (float)TeleportTrailDuration;

            for (int i = 0; i < NPC.oldPos.Length; i++)
            {
                // t goes from 1 (near the boss) to ~0 (back towards the start)
                float t = (NPC.oldPos.Length - i) / (float)NPC.oldPos.Length;

                // Stronger alpha near the “newer” positions, scaled by trailStrength
                float alpha = 0.75f * t * trailStrength;
                Color afterimageColor = Color.White * alpha;

                Vector2 drawPos =
                    NPC.oldPos[i] + NPC.Size / 2f - screenPos + new Vector2(0f, NPC.gfxOffY);

                ShaderHelper.DrawShimmerShader(
                    shader,
                    texture,
                    drawPos,
                    frame,
                    afterimageColor,
                    NPC.rotation,
                    origin,
                    NPC.scale,
                    effects,
                    0f
                );
            }
        }

        Vector2 mainPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);
        ShaderHelper.DrawShimmerShader(
            shader,
            texture,
            mainPos,
            frame,
            drawColor,
            NPC.rotation,
            origin,
            NPC.scale,
            effects,
            0f
        );

        return false;
    }

    public override void OnSpawn(IEntitySource source)
    {
        var transformationState = new TransformationState();
        var idleState = new IdleState();
        var lightningState = new LightningState();
        var bubbleSwarmState = new BubbleSwarmState();
        var giantBubbleState = new GiantBubbleState();
        var movementState = new MovementState();

        transformationState.Transitions =
        [
            new Transition<HydrolysistContext>
            {
                To = idleState,
                Conditions = [new TransitionCondition { Predicate = () => Timer <= 0f }],
            },
        ];
        idleState.Transitions =
        [
            new Transition<HydrolysistContext>()
            {
                To = lightningState,
                Conditions =
                [
                    new TransitionCondition { Predicate = () => Timer <= 0 && Phase == 0f },
                ],
            },
            new Transition<HydrolysistContext>()
            {
                To = bubbleSwarmState,
                Conditions =
                [
                    new TransitionCondition { Predicate = () => Timer <= 0 && Phase == 1f },
                ],
            },
            new Transition<HydrolysistContext>()
            {
                To = giantBubbleState,
                Conditions =
                [
                    new TransitionCondition { Predicate = () => Timer <= 0 && Phase == 2f },
                ],
            },
        ];
        lightningState.Transitions =
        [
            new Transition<HydrolysistContext>()
            {
                To = movementState,
                Conditions = [new TransitionCondition { Predicate = () => Phase >= 3f }],
            },
        ];
        bubbleSwarmState.Transitions =
        [
            new Transition<HydrolysistContext>()
            {
                To = movementState,
                Conditions = [new TransitionCondition { Predicate = () => Phase >= 2f }],
            },
        ];
        giantBubbleState.Transitions =
        [
            new Transition<HydrolysistContext>()
            {
                To = movementState,
                Conditions = [new TransitionCondition { Predicate = () => Phase >= 2f }],
            },
        ];
        movementState.Transitions =
        [
            new Transition<HydrolysistContext>()
            {
                To = idleState,
                Conditions = [new TransitionCondition { Predicate = () => Phase > 0f }],
            },
        ];

        stateMachine = new StateMachine<HydrolysistContext>(
            [
                transformationState,
                idleState,
                lightningState,
                bubbleSwarmState,
                giantBubbleState,
                movementState,
            ],
            new HydrolysistContext { Boss = this }
        );
        CurrentAnimation = new AnimationFrameData(1, [0]);
    }

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        // use the boss immunity cooldown counter, to prevent ignoring boss attacks by taking damage from other sources
        cooldownSlot = ImmunityCooldownID.Bosses;
        return true;
    }

    public override void FindFrame(int frameHeight)
    {
        NPC.frameCounter += 0.5f;
        int index = (int)(NPC.frameCounter / CurrentAnimation.frameRate);
        if ((int)(NPC.frameCounter / CurrentAnimation.frameRate) >= CurrentAnimation.frames.Length)
        {
            NPC.frameCounter = 0;
        }
        NPC.frame.Y =
            CurrentAnimation.frames[(int)(NPC.frameCounter / CurrentAnimation.frameRate)]
            * frameHeight;
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
        int strikeDamage = NPC.GetAttackDamage_ForProjectiles_MultiLerp(30, 45, 60);
        if (
            Main.netMode != NetmodeID.MultiplayerClient
            && Timer % 2 != 0
            && stateMachine.CurrentState is not TransformationState
        )
            StrikeShimmeredPlayers(strikeDamage);
        Vector2 center = NPC.Center;
        // Get a target
        if (
            NPC.target < 0
            || NPC.target == Main.maxPlayers
            || Main.player[NPC.target].dead
            || !Main.player[NPC.target].active
        )
        {
            NPC.TargetClosest();
        }

        Player player = Main.player[NPC.target];

        if (player.dead || !player.active || Vector2.Distance(player.Center, center) > AggroRadius)
        {
            NPC.life = 0;
            NPC.HitEffect();
            NPC.active = false;
            NPC.velocity.Y -= 0.02f;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.EncourageDespawn(120);
            return;
        }
        var context = new HydrolysistContext { Boss = this, Target = player };
        stateMachine.Tick(context);
        if (teleportTrailTimer > 0)
            teleportTrailTimer--;

        if (stateMachine.CurrentState is not TransformationState)
            Lighting.AddLight(center, Color.Pink.ToVector3() * 0.75f);
    }

    private void StrikeShimmeredPlayers(int strikeDamage)
    {
        foreach (Player player in Main.ActivePlayers)
        {
            if (!player.shimmering)
                continue;
            int finalDamage = (int)player.GetTotalDamage(DamageClass.Magic).ApplyTo(strikeDamage);
            player.Hurt(
                PlayerDeathReason.ByNPC(this.NPC.whoAmI),
                finalDamage,
                -player.direction,
                knockback: 4f
            );
            player.AddBuff(ModContent.BuffType<ShimmerImmunityBuff>(), 300, false);
            if (Collision.SolidCollision(player.position, player.width, player.height - 2, false))
            {
                Vector2? position = ShimmerHelper.FindSpotWithoutShimmer(player, 1024, false);
                if (position.HasValue)
                {
                    Vector2 spot = position.Value;
                    Lighting.AddLight(spot, Color.White.ToVector3());
                    // spot.X += player.width;
                    spot.Y -= player.height * 2;
                    player.position = spot;
                }
            }
            CreateArc(player);
        }
    }

    private void CreateArc(Player player)
    {
        Vector2 start = NPC.Center;
        Vector2 end = player.Center;
        float distance = Vector2.Distance(start, end);

        // Number of intermediate points – scales with distance but clamped
        int segments = Math.Clamp((int)(distance / 40f), 4, 24);

        var points = new List<Vector2>(segments + 1) { start };

        // Build a jagged, semi-random polyline between start and end
        Vector2 dir = end - start;
        Vector2 perp = new(-dir.Y, dir.X);
        if (perp == Vector2.Zero)
            perp = Vector2.UnitX;
        perp = Vector2.Normalize(perp);

        float maxOffset = Math.Min(120f, distance * 0.25f);
        for (int i = 1; i < segments; i++)
        {
            float t = i / (float)segments;
            Vector2 point = Vector2.Lerp(start, end, t);

            // Offset stronger near the middle, weaker near endpoints
            float midFactor = 1f - Math.Abs(2f * t - 1f);
            float offset = Main.rand.NextFloat(-1f, 1f) * maxOffset * midFactor;
            point += perp * offset;
            // small random jitter
            point += Main.rand.NextVector2Circular(4f, 4f);
            points.Add(point);
        }
        points.Add(end);

        if (Main.netMode == NetmodeID.Server)
            return;

        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch, NPC.Center);
        for (int s = 0; s < points.Count - 1; s++)
        {
            Vector2 a = points[s];
            Vector2 b = points[s + 1];
            float segLen = Vector2.Distance(a, b);
            int dustCount = Math.Max(2, (int)(segLen / 8f));
            for (int k = 0; k <= dustCount; k++)
            {
                float u = k / (float)dustCount;
                Vector2 pos = Vector2.Lerp(a, b, u) + Main.rand.NextVector2Circular(2f, 2f);
                Vector2 velocity =
                    (b - a).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.2f, 2f);
                var d = Dust.NewDustPerfect(pos, DustID.ShimmerSpark, velocity);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(2f, 3f);
            }

            // occasional brighter sparks at segment midpoints
            if (Main.rand.NextBool(3))
            {
                Vector2 mid = (a + b) * 0.5f + Main.rand.NextVector2Circular(6f, 6f);
                var spark = Dust.NewDustPerfect(
                    mid,
                    DustID.ShimmerSpark,
                    (mid - NPC.Center) * 0.02f
                );
                spark.noGravity = true;
                spark.scale = Main.rand.NextFloat(2.6f, 3.4f);
            }
        }
    }

    private class HydrolysistContext
    {
        public HydrolysistBossBody Boss;
        public Player Target;
    }

    private class IdleState : IState<HydrolysistContext>
    {
        private const float DECISION_TIME = 60f;
        private int phaseTracker = 0;
        private readonly AnimationFrameData IdleAnimation = new(10, [4, 5, 6]);
        public List<Transition<HydrolysistContext>> Transitions { get; set; } = [];

        public void Enter(IState<HydrolysistContext> from, HydrolysistContext context)
        {
            context.Boss.Timer = DECISION_TIME;
            context.Boss.Phase = 0f;
            context.Boss.CurrentAnimation = IdleAnimation;
        }

        public void Exit(IState<HydrolysistContext> to, HydrolysistContext context)
        {
            context.Boss.DebugPrint($"Transitioning to {to.GetType().Name}");
        }

        public void Tick(HydrolysistContext context)
        {
            context.Boss.Timer -= 1f;
            if (Main.netMode == NetmodeID.MultiplayerClient || context.Boss.Timer != 0f)
                return;
            context.Boss.Phase = ++phaseTracker % Transitions.Count;
            context.Boss.NPC.netUpdate = true;
        }
    }

    private class TransformationState : IState<HydrolysistContext>
    {
        private const float TRANSFORMATION_TIME = 240f;
        private static readonly AnimationFrameData StartAnimation = new(3, [0, 3, 2]);
        private static readonly AnimationFrameData FloatAnimation = new(10, [7, 8, 9]);
        public List<Transition<HydrolysistContext>> Transitions { get; set; } = [];

        public void Enter(IState<HydrolysistContext> from, HydrolysistContext context)
        {
            context.Boss.Timer = TRANSFORMATION_TIME;
            context.Boss.Phase = 0f;
            context.Boss.NPC.Opacity = 0.25f;
            context.Boss.NPC.velocity.Y = 0f;
            context.Boss.NPC.dontTakeDamage = true;
            context.Boss.NPC.chaseable = false;
            context.Boss.NPC.netUpdate = true;
            context.Boss.CurrentAnimation = StartAnimation;
        }

        public void Exit(IState<HydrolysistContext> to, HydrolysistContext context)
        {
            // context.Boss.NPC.Opacity = 1f;
            context.Boss.NPC.velocity.Y = 0f;
            context.Boss.NPC.dontTakeDamage = false;
            context.Boss.NPC.chaseable = true;
            context.Boss.NPC.netUpdate = true;
        }

        public void Tick(HydrolysistContext context)
        {
            context.Boss.NPC.Opacity = Math.Min(
                context.Boss.NPC.Opacity + 1 / TRANSFORMATION_TIME,
                1f
            );
            Lighting.AddLight(
                context.Boss.NPC.Center,
                Color.Pink.ToVector3()
                    * (0.75f + 0.25f * MathF.Sin(context.Boss.Timer * MathF.PI / 15f))
            );
            ;
            context.Boss.Timer--;
            if (
                context.Boss.Timer
                < TRANSFORMATION_TIME
                    - 60 * (1f / StartAnimation.frameRate) * StartAnimation.frames.Length
            )
            {
                context.Boss.CurrentAnimation = FloatAnimation;
                context.Boss.NPC.velocity.Y = -0.33f;
            }
        }
    }

    private class LightningState : IState<HydrolysistContext>
    {
        private const float CHARGE_TIME = 120f;
        private const float ATTACK_TIME = 300f;
        private const float RECOVER_TIME = 60f;
        private const int FIRE_INTERVAL = 15;
        private static readonly AnimationFrameData chargeAnimation = new(10, [7, 8, 9]);
        private static readonly AnimationFrameData fireAnimation = new(10, [10, 11, 12]);
        private static readonly AnimationFrameData recoverAnimation = new(10, [3, 4, 5, 6]);
        public List<Transition<HydrolysistContext>> Transitions { get; set; }

        public void Enter(IState<HydrolysistContext> from, HydrolysistContext context)
        {
            context.Boss.Timer = -1f;
            context.Boss.Phase = 0f;
        }

        public void Exit(IState<HydrolysistContext> to, HydrolysistContext context) { }

        public void Tick(HydrolysistContext context)
        {
            switch (context.Boss.Phase)
            {
                case 0:
                    if (context.Boss.Timer <= 0)
                    {
                        context.Boss.Timer = CHARGE_TIME;
                        context.Boss.DebugPrint("Entering Charging Phase");
                    }
                    ChargeLightning(context);
                    break;
                case 1:
                    if (context.Boss.Timer <= 0)
                    {
                        context.Boss.Timer = ATTACK_TIME;
                        context.Boss.DebugPrint("Entering Firing Phase");
                    }
                    FireLightning(context);
                    break;
                case 2:
                    if (context.Boss.Timer <= 0)
                    {
                        context.Boss.Timer = RECOVER_TIME;
                        context.Boss.DebugPrint("Entering Recovery Phase");
                    }
                    Recover(context);
                    break;
            }
            context.Boss.Timer -= 1f;
            if (context.Boss.Timer <= 0)
            {
                context.Boss.Phase++;
            }
        }

        private static void ChargeLightning(HydrolysistContext context)
        {
            context.Boss.CurrentAnimation = chargeAnimation;
        }

        private static void FireLightning(HydrolysistContext context)
        {
            context.Boss.CurrentAnimation = fireAnimation;
            if (context.Boss.NPC.HasValidTarget)
            {
                FaceHorizontallyTowards(
                    context.Boss.NPC,
                    Main.player[context.Boss.NPC.target].Center
                );
            }
            if (context.Boss.Timer % FIRE_INTERVAL != 0)
                return;
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (!context.Boss.NPC.HasValidTarget)
                context.Boss.NPC.TargetClosest();
            if (!context.Boss.NPC.HasValidTarget)
                return;
            Vector2 directionToPlayer = SafeVector(
                Main.player[context.Boss.NPC.target].position - context.Boss.NPC.position,
                1
            );
            directionToPlayer.Normalize();
            float spawnAngle = (
                Vector2.Normalize(directionToPlayer.RotatedByRandom(Math.PI / 4)) * 7f
            ).ToRotation();
            Projectile.NewProjectileDirect(
                context.Boss.NPC.GetSource_FromAI(),
                context.Boss.NPC.Center,
                directionToPlayer * 2f,
                ModContent.ProjectileType<ShimmerLightning>(),
                10,
                10,
                Main.myPlayer,
                spawnAngle,
                Main.rand.Next(100)
            );
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap, context.Boss.NPC.Center);
        }

        private static void Recover(HydrolysistContext context)
        {
            context.Boss.CurrentAnimation = recoverAnimation;
        }
    }

    private class BubbleSwarmState : IState<HydrolysistContext>
    {
        private const float CHARGE_TIME = 120f;
        private const float ATTACK_TIME = 300f;
        private const int FIRE_INTERVAL = 5;
        private const float BUBBLE_SPEED = 8f;
        private static int BUBBLE_DAMAGE;
        private static readonly AnimationFrameData chargeAnimation = new(10, [7, 8, 9]);
        private static readonly AnimationFrameData fireAnimation = new(10, [10, 11, 12]);

        public List<Transition<HydrolysistContext>> Transitions { get; set; }

        public void Enter(IState<HydrolysistContext> from, HydrolysistContext context)
        {
            context.Boss.Timer = -1f;
            context.Boss.Phase = 0f;
            BUBBLE_DAMAGE = context.Boss.NPC.GetAttackDamage_ForProjectiles(25, 30);
        }

        public void Exit(IState<HydrolysistContext> to, HydrolysistContext context) { }

        public void Tick(HydrolysistContext context)
        {
            switch (context.Boss.Phase)
            {
                case 0:
                    if (context.Boss.Timer <= 0)
                    {
                        context.Boss.Timer = CHARGE_TIME;
                        context.Boss.DebugPrint("Entering Charging Phase");
                    }
                    ChargeBubbleSwarm(context);
                    break;
                case 1:
                    if (context.Boss.Timer <= 0)
                    {
                        context.Boss.Timer = ATTACK_TIME;
                        context.Boss.DebugPrint("Entering Firing Phase");
                    }
                    FireBubbleSwarm(context);
                    break;
            }
            context.Boss.Timer -= 1f;
            if (context.Boss.Timer <= 0)
            {
                context.Boss.Phase++;
            }
        }

        private static void ChargeBubbleSwarm(HydrolysistContext context)
        {
            context.Boss.CurrentAnimation = chargeAnimation;
            if (context.Boss.NPC.HasValidTarget)
            {
                FaceHorizontallyTowards(
                    context.Boss.NPC,
                    Main.player[context.Boss.NPC.target].Center
                );
            }
        }

        private static void FireBubbleSwarm(HydrolysistContext context)
        {
            context.Boss.CurrentAnimation = fireAnimation;
            if (context.Boss.NPC.HasValidTarget)
            {
                FaceHorizontallyTowards(
                    context.Boss.NPC,
                    Main.player[context.Boss.NPC.target].Center
                );
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (!context.Boss.NPC.HasValidTarget)
                context.Boss.NPC.TargetClosest();
            if (!context.Boss.NPC.HasValidTarget)
                return;
            if (context.Boss.Timer % FIRE_INTERVAL != 0)
                return;
            Vector2 directionToPlayer = SafeVector(
                Main.player[context.Boss.NPC.target].position - context.Boss.NPC.position,
                1
            );
            Vector2 velocity = directionToPlayer.RotatedByRandom(Math.PI / 4);
            velocity = velocity.SafeNormalize(Vector2.UnitX) * BUBBLE_SPEED;
            Projectile.NewProjectileDirect(
                context.Boss.NPC.GetSource_FromAI(),
                context.Boss.NPC.Center,
                velocity,
                ModContent.ProjectileType<SmallBubble>(),
                BUBBLE_DAMAGE,
                10,
                Main.myPlayer
            );
            SoundEngine.PlaySound(SoundID.Item85, context.Boss.NPC.Center);
        }
    }

    private class GiantBubbleState : IState<HydrolysistContext>
    {
        private const float CHARGE_TIME = 60f;
        private const float BUBBLE_SPEED = 5f;
        private static readonly AnimationFrameData chargeAnimation = new(10, [7, 8, 9]);
        private static readonly AnimationFrameData fireAnimation = new(10, [10, 11, 12]);
        public List<Transition<HydrolysistContext>> Transitions { get; set; }

        public void Enter(IState<HydrolysistContext> from, HydrolysistContext context)
        {
            context.Boss.Timer = 0f;
            context.Boss.Phase = 0f;
            context.Boss.NPC.netUpdate = true;
        }

        public void Exit(IState<HydrolysistContext> to, HydrolysistContext context) { }

        public void Tick(HydrolysistContext context)
        {
            switch (context.Boss.Phase)
            {
                case 0:
                    if (context.Boss.Timer <= 0)
                    {
                        context.Boss.Timer = CHARGE_TIME;
                        context.Boss.DebugPrint("Entering Charging Phase");
                    }
                    context.Boss.CurrentAnimation = chargeAnimation;
                    if (context.Target.active)
                        FaceHorizontallyTowards(context.Boss.NPC, context.Target.Center);
                    break;
                case 1:
                    if (context.Boss.Timer <= 0)
                    {
                        context.Boss.Timer = 10f;
                    }
                    FireBubble(context);
                    break;
            }
            context.Boss.Timer -= 1f;
            if (context.Boss.Timer <= 0)
            {
                context.Boss.Phase++;
            }
        }

        private static void FireBubble(HydrolysistContext context)
        {
            context.Boss.CurrentAnimation = fireAnimation;
            FaceHorizontallyTowards(context.Boss.NPC, context.Target.Center);

            if (context.Boss.Timer < 10)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 directionToPlayer = SafeVector(
                context.Target.position - context.Boss.NPC.position,
                1
            );
            directionToPlayer.Normalize();
            Vector2 velocity = directionToPlayer * BUBBLE_SPEED;
            Projectile.NewProjectileDirect(
                context.Boss.NPC.GetSource_FromAI(),
                context.Boss.NPC.Center,
                velocity,
                ModContent.ProjectileType<BigBubble>(),
                160,
                10,
                Main.myPlayer
            );
            SoundEngine.PlaySound(SoundID.Item85, context.Boss.NPC.Center);
        }
    }

    private class MovementState : IState<HydrolysistContext>
    {
        // Time the boss spends "charging" before disappearing
        private const int TELEGRAPH_TIME = 30;

        // Time after reappearing before going back to idle / attacks
        private const int RECOVER_TIME = 20;

        private static readonly AnimationFrameData teleportOutAnimation = new(5, [4, 5, 6]);
        private static readonly AnimationFrameData teleportInAnimation = new(5, [7, 8, 9]);

        public List<Transition<HydrolysistContext>> Transitions { get; set; }

        private bool hasTeleported;

        public void Enter(IState<HydrolysistContext> from, HydrolysistContext context)
        {
            hasTeleported = false;

            context.Boss.Timer = TELEGRAPH_TIME;
            context.Boss.Phase = 0f; // will be set to >0 when teleport is done
            context.Boss.CurrentAnimation = teleportOutAnimation;

            // briefly invulnerable / un-targetable during warp
            context.Boss.NPC.dontTakeDamage = true;
            context.Boss.NPC.chaseable = false;
            context.Boss.NPC.velocity = Vector2.Zero;
            context.Boss.NPC.netUpdate = true;
        }

        public void Exit(IState<HydrolysistContext> to, HydrolysistContext context)
        {
            context.Boss.NPC.dontTakeDamage = false;
            context.Boss.NPC.chaseable = true;
            context.Boss.NPC.velocity = Vector2.Zero;
            context.Boss.NPC.netUpdate = true;
        }

        public void Tick(HydrolysistContext context)
        {
            context.Boss.Timer--;

            if (!hasTeleported && context.Boss.Timer <= 0)
            {
                DoTeleport(context); // actually warp
                hasTeleported = true;

                context.Boss.CurrentAnimation = teleportInAnimation;
                context.Boss.Timer = RECOVER_TIME;

                context.Boss.Phase = 1f;
                context.Boss.NPC.netUpdate = true;
            }
        }

        private void DoTeleport(HydrolysistContext context)
        {
            NPC npc = context.Boss.NPC;
            Player target = context.Target ?? Main.player[npc.target];
            if (target == null || !target.active)
                return;

            //before teleport
            Vector2 oldCenter = npc.Center;

            SpawnTeleportDust(oldCenter);

            //find new position around player
            Vector2 newCenter = FindTeleportDestination(npc, target);

            if (npc.oldPos != null && npc.oldPos.Length > 0)
            {
                int len = npc.oldPos.Length;

                for (int i = 0; i < len; i++)
                {
                    // t goes 0 → 1 along the path
                    float t = i / (float)(len - 1);

                    // Interpolate between centers
                    Vector2 lerpCenter = Vector2.Lerp(oldCenter, newCenter, t);

                    // oldPos is top-left, so subtract half the size
                    npc.oldPos[i] = lerpCenter - npc.Size / 2f;
                }
            }

            context.Boss.teleportTrailTimer = TeleportTrailDuration;

            // Only the server actually moves the NPC
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.Center = newCenter;
                FaceHorizontallyTowards(npc, target.Center);
                npc.netUpdate = true;
            }

            // Burst of dust at the new location
            SpawnTeleportDust(newCenter);
        }

        private static void SpawnTeleportDust(Vector2 center)
        {
            for (int i = 0; i < 25; i++)
            {
                Vector2 speed = Main.rand.NextVector2Circular(4f, 4f);
                int d = Dust.NewDust(
                    center - new Vector2(16f, 16f),
                    32,
                    32,
                    DustID.ShimmerSpark,
                    speed.X,
                    speed.Y,
                    150,
                    default,
                    1.6f
                );
                Main.dust[d].noGravity = true;
            }

            for (int i = 0; i < 15; i++)
            {
                Vector2 speed = Main.rand.NextVector2Circular(2f, 2f);
                int d = Dust.NewDust(
                    center - new Vector2(24f, 24f),
                    48,
                    48,
                    DustID.PinkTorch,
                    speed.X,
                    speed.Y,
                    200,
                    default,
                    1.1f
                );
                Main.dust[d].noGravity = true;
            }
        }

        private static Vector2 FindTeleportDestination(NPC npc, Player target)
        {
            const int attempts = 30;
            const float minRadius = 250f;
            const float maxRadius = 450f;

            for (int i = 0; i < attempts; i++)
            {
                // Random angle around the player
                float angle = MathHelper.ToRadians(Main.rand.Next(360));
                float radius = Main.rand.NextFloat(minRadius, maxRadius);

                Vector2 offset = angle.ToRotationVector2() * radius;
                Vector2 candidateCenter = target.Center + offset;

                // Don't go too far off-screen or outside world
                candidateCenter.X = MathHelper.Clamp(
                    candidateCenter.X,
                    200f,
                    Main.maxTilesX * 16f - 200f
                );
                candidateCenter.Y = MathHelper.Clamp(
                    candidateCenter.Y,
                    200f,
                    Main.maxTilesY * 16f - 200f
                );

                Rectangle hitbox = new Rectangle(
                    (int)(candidateCenter.X - npc.width / 2),
                    (int)(candidateCenter.Y - npc.height / 2),
                    npc.width,
                    npc.height
                );

                if (
                    !Collision.SolidCollision(
                        hitbox.Location.ToVector2(),
                        hitbox.Width,
                        hitbox.Height
                    )
                )
                    return candidateCenter;
            }

            // If all else fails stay where we are
            return npc.Center;
        }
    }

    //#region Cultist Code
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

    private enum CultistAiState
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

    // Constants extracted for readability
    private const float AggroRadius = 5600f;
    private const float SpreadSmall = 0.5235987901687622f; // ~30 degrees
    private const float SpreadLarge = 1.2566370964050293f; // ~72 degrees
    private const float TwoPi = (float)Math.PI * 2f;
    private const float RitualRingRadius = 180f;
    private const int RitualFadeOutTicks = 30;
    private const int RitualBetweenTicks = 60; // 30->90 window
    private const int RitualActiveStart = 120;
    private const int RitualActiveEnd = 420;

    // High-level AI overview
    // ai[0] = State machine selector (see AiState)
    // ai[1] = State-local timer (ticks)
    // ai[2] = Projectile id for ritual/anchor projectiles (when used)
    // ai[3] = Pattern index/phase counter OR link to leader (vanilla Cultist), context-dependent
    // localAI[0] = One-time spawn init flag
    // localAI[1] = Clone group id (vanilla Cultist behavior)
    // localAI[2] = Pose/animation hint (10, 11, 12, 13 used by vanilla sprites)
    public void _CultistAI()
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
        switch ((CultistAiState)(int)aiState)
        {
            // Idle/decision state: pick and transition into the next attack pattern
            case CultistAiState.IdleDecision:
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
            case CultistAiState.MoveSequence:
            {
                TickMoveSequence(ref isInvulnerable);

                break;
            }
            // Ice mist volley (periodic aimed projectiles)
            case CultistAiState.IceMistVolley:
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
            case CultistAiState.FireballBarrage:
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
            case CultistAiState.LightningOrbAndBolts:
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
            case CultistAiState.Ritual:
            {
                TickRitual(player, isLeaderCultist, ref isInvulnerable, ref isUnchaseable);

                break;
            }
            // Short taunt/pause state
            case CultistAiState.TauntPause:
            {
                TickTauntPause();

                break;
            }
            // Ancient Doom fanned waves from boss and clones
            case CultistAiState.AncientDoomFan:
            {
                TickAncientDoomFan(player, center, isLeaderCultist, doomInterval, doomRepeats);

                break;
            }
            // Summon adds around the player at random valid tiles
            case CultistAiState.SummonAdds:
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

    //#endregion
}
