using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace terraari.Content.NPCs.HydrolysistBoss;

[AutoloadBossHead]
public class HydrolysistBossBody : ModNPC
{
    public override void SetStaticDefaults()
    {
        // Adjust to match the frames of the spritesheet
        Main.npcFrameCount[NPC.type] = 1;
        // Add boss to the bestiary
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        NPCID.Sets.NPCBestiaryDrawModifiers value = new()
        {
            Scale = 1f,
            PortraitScale = 1f,
            CustomTexturePath = "terraari/ExtraTextures/Bestiary/HydrolysistBoss_Bestiary",
        };
        NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.TrailingMode[Type] = 0;
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

    public override void AI()
    {
        if (NPC.ai[0] != -1f && Main.rand.Next(1000) == 0)
        {
            // SoundEngine.PlaySound(29, (int)NPC.position.X, (int)NPC.position.Y, Main.rand.Next(88, 92));
        }
        bool expertMode = Main.expertMode;
        bool flag = NPC.life <= NPC.lifeMax / 2;
        int num = 120;
        int attackDamage_ForProjectiles = NPC.GetAttackDamage_ForProjectiles(35f, 25f);
        if (expertMode)
        {
            num = 90;
        }
        if (Main.getGoodWorld)
        {
            num -= 30;
        }
        int num2 = 18;
        int num3 = 3;
        int attackDamage_ForProjectiles2 = NPC.GetAttackDamage_ForProjectiles(30f, 20f);
        if (expertMode)
        {
            num2 = 12;
            num3 = 4;
        }
        if (Main.getGoodWorld)
        {
            num2 = 10;
            num3 = 5;
        }
        int num4 = 80;
        int attackDamage_ForProjectiles3 = NPC.GetAttackDamage_ForProjectiles(45f, 30f);
        if (expertMode)
        {
            num4 = 40;
        }
        if (Main.getGoodWorld)
        {
            num4 -= 20;
        }
        int num5 = 20;
        int num6 = 2;
        if (expertMode)
        {
            num5 = 30;
            num6 = 2;
        }
        int num7 = 20;
        int num8 = 3;
        bool flag2 = NPC.type == NPCID.CultistBoss;
        bool flag3 = false;
        bool flag4 = false;
        if (flag)
        {
            NPC.defense = (int)(NPC.defDefense * 0.65f);
        }
        if (!flag2)
        {
            if (
                NPC.ai[3] < 0f
                || !Main.npc[(int)NPC.ai[3]].active
                || Main.npc[(int)NPC.ai[3]].type != NPCID.CultistBoss
            )
            {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.active = false;
                return;
            }
            NPC.ai[0] = Main.npc[(int)NPC.ai[3]].ai[0];
            NPC.ai[1] = Main.npc[(int)NPC.ai[3]].ai[1];
            if (NPC.ai[0] == 5f)
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
                    NPC obj = Main.npc[(int)NPC.ai[3]];
                    obj.ai[0] = 6f;
                    obj.ai[1] = 0f;
                    obj.netUpdate = true;
                }
            }
            else
            {
                flag3 = true;
                flag4 = true;
            }
        }
        else if (NPC.ai[0] == 5f && NPC.ai[1] >= 120f && NPC.ai[1] < 420f && NPC.justHit)
        {
            NPC.ai[0] = 0f;
            NPC.ai[1] = 0f;
            NPC.ai[3] += 1f;
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
                if (nPC.localAI[1] == NPC.localAI[1] && num9 > 0)
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
            Main.projectile[(int)NPC.ai[2]].ai[1] = -1f;
            Main.projectile[(int)NPC.ai[2]].netUpdate = true;
        }
        Vector2 center = NPC.Center;
        Player player = Main.player[NPC.target];
        float num10 = 5600f;
        if (
            NPC.target < 0
            || NPC.target == 255
            || player.dead
            || !player.active
            || Vector2.Distance(player.Center, center) > num10
        )
        {
            NPC.TargetClosest(faceTarget: false);
            player = Main.player[NPC.target];
            NPC.netUpdate = true;
        }
        if (player.dead || !player.active || Vector2.Distance(player.Center, center) > num10)
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
        float num11 = NPC.ai[3];
        if (NPC.localAI[0] == 0f)
        {
            // SoundEngine.PlaySound(29, (int)NPC.position.X, (int)NPC.position.Y, 89);
            NPC.localAI[0] = 1f;
            NPC.alpha = 255;
            NPC.rotation = 0f;
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                NPC.ai[0] = -1f;
                NPC.netUpdate = true;
            }
        }
        if (NPC.ai[0] == -1f)
        {
            NPC.alpha -= 5;
            if (NPC.alpha < 0)
            {
                NPC.alpha = 0;
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= 420f)
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.netUpdate = true;
            }
            else if (NPC.ai[1] > 360f)
            {
                NPC.velocity *= 0.95f;
                if (NPC.localAI[2] != 13f)
                {
                    // SoundEngine.PlaySound(in new SoundStyle(29), NPC.position, 105);
                }
                NPC.localAI[2] = 13f;
            }
            else if (NPC.ai[1] > 300f)
            {
                NPC.velocity = -Vector2.UnitY;
                NPC.localAI[2] = 10f;
            }
            else if (NPC.ai[1] > 120f)
            {
                NPC.localAI[2] = 1f;
            }
            else
            {
                NPC.localAI[2] = 0f;
            }
            flag3 = true;
            flag4 = true;
        }
        if (NPC.ai[0] == 0f)
        {
            if (NPC.ai[1] == 0f)
            {
                NPC.TargetClosest(faceTarget: false);
            }
            NPC.localAI[2] = 10f;
            int num12 = Math.Sign(player.Center.X - center.X);
            if (num12 != 0)
            {
                NPC.direction = (NPC.spriteDirection = num12);
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= 40f && flag2)
            {
                int num13 = 0;
                if (flag)
                {
                    switch ((int)NPC.ai[3])
                    {
                        case 0:
                            num13 = 0;
                            break;
                        case 1:
                            num13 = 1;
                            break;
                        case 2:
                            num13 = 0;
                            break;
                        case 3:
                            num13 = 5;
                            break;
                        case 4:
                            num13 = 0;
                            break;
                        case 5:
                            num13 = 3;
                            break;
                        case 6:
                            num13 = 0;
                            break;
                        case 7:
                            num13 = 5;
                            break;
                        case 8:
                            num13 = 0;
                            break;
                        case 9:
                            num13 = 2;
                            break;
                        case 10:
                            num13 = 0;
                            break;
                        case 11:
                            num13 = 3;
                            break;
                        case 12:
                            num13 = 0;
                            break;
                        case 13:
                            num13 = 4;
                            NPC.ai[3] = -1f;
                            break;
                        default:
                            NPC.ai[3] = -1f;
                            break;
                    }
                }
                else
                {
                    switch ((int)NPC.ai[3])
                    {
                        case 0:
                            num13 = 0;
                            break;
                        case 1:
                            num13 = 1;
                            break;
                        case 2:
                            num13 = 0;
                            break;
                        case 3:
                            num13 = 2;
                            break;
                        case 4:
                            num13 = 0;
                            break;
                        case 5:
                            num13 = 3;
                            break;
                        case 6:
                            num13 = 0;
                            break;
                        case 7:
                            num13 = 1;
                            break;
                        case 8:
                            num13 = 0;
                            break;
                        case 9:
                            num13 = 2;
                            break;
                        case 10:
                            num13 = 0;
                            break;
                        case 11:
                            num13 = 4;
                            NPC.ai[3] = -1f;
                            break;
                        default:
                            NPC.ai[3] = -1f;
                            break;
                    }
                }
                int maxValue = 6;
                if (NPC.life < NPC.lifeMax / 3)
                {
                    maxValue = 4;
                }
                if (NPC.life < NPC.lifeMax / 4)
                {
                    maxValue = 3;
                }
                if (
                    expertMode
                    && flag
                    && Main.rand.Next(maxValue) == 0
                    && num13 != 0
                    && num13 != 4
                    && num13 != 5
                    && NPC.CountNPCS(523) < 10
                )
                {
                    num13 = 6;
                }
                if (num13 == 0)
                {
                    float num14 = (float)
                        Math.Ceiling(
                            (player.Center + new Vector2(0f, -100f) - center).Length() / 50f
                        );
                    if (num14 == 0f)
                    {
                        num14 = 1f;
                    }
                    List<int> list2 = new List<int>();
                    int num15 = 0;
                    list2.Add(NPC.whoAmI);
                    for (int k = 0; k < 200; k++)
                    {
                        if (
                            Main.npc[k].active
                            && Main.npc[k].type == NPCID.CultistBossClone
                            && Main.npc[k].ai[3] == (float)NPC.whoAmI
                        )
                        {
                            list2.Add(k);
                        }
                    }
                    bool flag5 = list2.Count % 2 == 0;
                    foreach (int item2 in list2)
                    {
                        NPC nPC2 = Main.npc[item2];
                        Vector2 center2 = nPC2.Center;
                        float num16 =
                            (float)((num15 + flag5.ToInt() + 1) / 2)
                            * ((float)Math.PI * 2f)
                            * 0.4f
                            / (float)list2.Count;
                        if (num15 % 2 == 1)
                        {
                            num16 *= -1f;
                        }
                        if (list2.Count == 1)
                        {
                            num16 = 0f;
                        }
                        Vector2 vector =
                            new Vector2(0f, -1f).RotatedBy(num16) * new Vector2(300f, 200f);
                        Vector2 vector2 = player.Center + vector - center2;
                        nPC2.ai[0] = 1f;
                        nPC2.ai[1] = num14 * 2f;
                        nPC2.velocity = vector2 / num14;
                        if (NPC.whoAmI >= nPC2.whoAmI)
                        {
                            nPC2.position -= nPC2.velocity;
                        }
                        nPC2.netUpdate = true;
                        num15++;
                    }
                }
                switch (num13)
                {
                    case 1:
                        NPC.ai[0] = 3f;
                        NPC.ai[1] = 0f;
                        break;
                    case 2:
                        NPC.ai[0] = 2f;
                        NPC.ai[1] = 0f;
                        break;
                    case 3:
                        NPC.ai[0] = 4f;
                        NPC.ai[1] = 0f;
                        break;
                    case 4:
                        NPC.ai[0] = 5f;
                        NPC.ai[1] = 0f;
                        break;
                }
                if (num13 == 5)
                {
                    NPC.ai[0] = 7f;
                    NPC.ai[1] = 0f;
                }
                if (num13 == 6)
                {
                    NPC.ai[0] = 8f;
                    NPC.ai[1] = 0f;
                }
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 1f)
        {
            flag3 = true;
            NPC.localAI[2] = 10f;
            if ((float)(int)NPC.ai[1] % 2f != 0f && NPC.ai[1] != 1f)
            {
                NPC.position -= NPC.velocity;
            }
            NPC.ai[1] -= 1f;
            if (NPC.ai[1] <= 0f)
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 2f)
        {
            NPC.localAI[2] = 11f;
            Vector2 vec = Vector2.Normalize(player.Center - center);
            if (vec.HasNaNs())
            {
                vec = new Vector2(NPC.direction, 0f);
            }
            if (NPC.ai[1] >= 4f && flag2 && (int)(NPC.ai[1] - 4f) % num == 0)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    List<int> list3 = new List<int>();
                    for (int l = 0; l < 200; l++)
                    {
                        if (
                            Main.npc[l].active
                            && Main.npc[l].type == NPCID.CultistBossClone
                            && Main.npc[l].ai[3] == (float)NPC.whoAmI
                        )
                        {
                            list3.Add(l);
                        }
                    }
                    foreach (int item3 in list3)
                    {
                        NPC nPC3 = Main.npc[item3];
                        Vector2 center3 = nPC3.Center;
                        int num17 = Math.Sign(player.Center.X - center3.X);
                        if (num17 != 0)
                        {
                            nPC3.direction = (nPC3.spriteDirection = num17);
                        }

                        if (Main.netMode == NetmodeID.MultiplayerClient)
                            continue;
                        vec = Vector2.Normalize(player.Center - center3 + player.velocity * 20f);
                        if (vec.HasNaNs())
                        {
                            vec = new Vector2(NPC.direction, 0f);
                        }
                        Vector2 firePoint = center3 + new Vector2(NPC.direction * 30, 12f);
                        for (int m = 0; m < 1; m++)
                        {
                            Vector2 spinninpoint = vec * (6f + (float)Main.rand.NextDouble() * 4f);
                            spinninpoint = spinninpoint.RotatedByRandom(0.5235987901687622);
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                firePoint.X,
                                firePoint.Y,
                                spinninpoint.X,
                                spinninpoint.Y,
                                ProjectileID.CultistBossFireBallClone,
                                18,
                                0f,
                                Main.myPlayer
                            );
                        }
                    }
                }
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    vec = Vector2.Normalize(player.Center - center + player.velocity * 20f);
                    if (vec.HasNaNs())
                    {
                        vec = new Vector2(NPC.direction, 0f);
                    }
                    Vector2 vector4 = NPC.Center + new Vector2(NPC.direction * 30, 12f);
                    for (int n = 0; n < 1; n++)
                    {
                        Vector2 vector5 = vec * 4f;
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            vector4.X,
                            vector4.Y,
                            vector5.X,
                            vector5.Y,
                            ProjectileID.CultistBossIceMist,
                            attackDamage_ForProjectiles,
                            0f,
                            Main.myPlayer,
                            0f,
                            1f
                        );
                    }
                }
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= (float)(4 + num))
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 3f)
        {
            NPC.localAI[2] = 11f;
            Vector2 vec2 = Vector2.Normalize(player.Center - center);
            if (vec2.HasNaNs())
            {
                vec2 = new Vector2(NPC.direction, 0f);
            }
            if (NPC.ai[1] >= 4f && flag2 && (int)(NPC.ai[1] - 4f) % num2 == 0)
            {
                if ((int)(NPC.ai[1] - 4f) / num2 == 2)
                {
                    List<int> list4 = new List<int>();
                    for (int num18 = 0; num18 < 200; num18++)
                    {
                        if (
                            Main.npc[num18].active
                            && Main.npc[num18].type == NPCID.CultistBossClone
                            && Main.npc[num18].ai[3] == (float)NPC.whoAmI
                        )
                        {
                            list4.Add(num18);
                        }
                    }
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        foreach (int item4 in list4)
                        {
                            NPC nPC4 = Main.npc[item4];
                            Vector2 center4 = nPC4.Center;
                            int num19 = Math.Sign(player.Center.X - center4.X);
                            if (num19 != 0)
                            {
                                nPC4.direction = (nPC4.spriteDirection = num19);
                            }

                            if (Main.netMode == NetmodeID.MultiplayerClient)
                                continue;
                            vec2 = Vector2.Normalize(
                                player.Center - center4 + player.velocity * 20f
                            );
                            if (vec2.HasNaNs())
                            {
                                vec2 = new Vector2(NPC.direction, 0f);
                            }
                            Vector2 vector6 = center4 + new Vector2(NPC.direction * 30, 12f);
                            for (int num20 = 0; num20 < 1; num20++)
                            {
                                Vector2 spinninpoint2 =
                                    vec2 * (6f + (float)Main.rand.NextDouble() * 4f);
                                spinninpoint2 = spinninpoint2.RotatedByRandom(0.5235987901687622);
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    vector6.X,
                                    vector6.Y,
                                    spinninpoint2.X,
                                    spinninpoint2.Y,
                                    ProjectileID.CultistBossFireBallClone,
                                    18,
                                    0f,
                                    Main.myPlayer
                                );
                            }
                        }
                    }
                }
                int num21 = Math.Sign(player.Center.X - center.X);
                if (num21 != 0)
                {
                    NPC.direction = (NPC.spriteDirection = num21);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    vec2 = Vector2.Normalize(player.Center - center + player.velocity * 20f);
                    if (vec2.HasNaNs())
                    {
                        vec2 = new Vector2(NPC.direction, 0f);
                    }
                    Vector2 vector7 = NPC.Center + new Vector2(NPC.direction * 30, 12f);
                    for (int num22 = 0; num22 < 1; num22++)
                    {
                        Vector2 spinninpoint3 = vec2 * (6f + (float)Main.rand.NextDouble() * 4f);
                        spinninpoint3 = spinninpoint3.RotatedByRandom(0.5235987901687622);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            vector7.X,
                            vector7.Y,
                            spinninpoint3.X,
                            spinninpoint3.Y,
                            ProjectileID.CultistBossFireBall,
                            attackDamage_ForProjectiles2,
                            0f,
                            Main.myPlayer
                        );
                    }
                }
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= (float)(4 + num2 * num3))
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 4f)
        {
            if (flag2)
            {
                NPC.localAI[2] = 12f;
            }
            else
            {
                NPC.localAI[2] = 11f;
            }
            if (NPC.ai[1] == 20f && flag2 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                List<int> list5 = new List<int>();
                for (int num23 = 0; num23 < 200; num23++)
                {
                    if (
                        Main.npc[num23].active
                        && Main.npc[num23].type == NPCID.CultistBossClone
                        && Main.npc[num23].ai[3] == (float)NPC.whoAmI
                    )
                    {
                        list5.Add(num23);
                    }
                }
                foreach (int item5 in list5)
                {
                    NPC nPC5 = Main.npc[item5];
                    Vector2 center5 = nPC5.Center;
                    int num24 = Math.Sign(player.Center.X - center5.X);
                    if (num24 != 0)
                    {
                        nPC5.direction = (nPC5.spriteDirection = num24);
                    }

                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        continue;
                    Vector2 vector8 = Vector2.Normalize(
                        player.Center - center5 + player.velocity * 20f
                    );
                    if (vector8.HasNaNs())
                    {
                        vector8 = new Vector2(NPC.direction, 0f);
                    }
                    Vector2 vector9 = center5 + new Vector2(NPC.direction * 30, 12f);
                    for (int num25 = 0; num25 < 1; num25++)
                    {
                        Vector2 spinninpoint4 = vector8 * (6f + (float)Main.rand.NextDouble() * 4f);
                        spinninpoint4 = spinninpoint4.RotatedByRandom(0.5235987901687622);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            vector9.X,
                            vector9.Y,
                            spinninpoint4.X,
                            spinninpoint4.Y,
                            ProjectileID.CultistBossFireBallClone,
                            18,
                            0f,
                            Main.myPlayer
                        );
                    }
                }
                if ((int)(NPC.ai[1] - 20f) % num4 == 0)
                {
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center.X,
                        NPC.Center.Y - 100f,
                        0f,
                        0f,
                        ProjectileID.CultistBossLightningOrb,
                        attackDamage_ForProjectiles3,
                        0f,
                        Main.myPlayer
                    );
                }
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= (float)(20 + num4))
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 5f)
        {
            NPC.localAI[2] = 10f;
            if (Vector2.Normalize(player.Center - center).HasNaNs())
            {
                new Vector2(NPC.direction, 0f);
            }
            if (NPC.ai[1] >= 0f && NPC.ai[1] < 30f)
            {
                flag3 = true;
                flag4 = true;
                float num26 = (NPC.ai[1] - 0f) / 30f;
                NPC.alpha = (int)(num26 * 255f);
            }
            else if (NPC.ai[1] >= 30f && NPC.ai[1] < 90f)
            {
                if (NPC.ai[1] == 30f && Main.netMode != NetmodeID.MultiplayerClient && flag2)
                {
                    NPC.localAI[1] += 1f;
                    Vector2 spinningpoint = new Vector2(180f, 0f);
                    List<int> list6 = new List<int>();
                    for (int num27 = 0; num27 < 200; num27++)
                    {
                        if (
                            Main.npc[num27].active
                            && Main.npc[num27].type == NPCID.CultistBossClone
                            && Main.npc[num27].ai[3] == (float)NPC.whoAmI
                        )
                        {
                            list6.Add(num27);
                        }
                    }
                    int num28 = 6 - list6.Count;
                    if (num28 > 2)
                    {
                        num28 = 2;
                    }
                    int num29 = list6.Count + num28 + 1;
                    float[] array = new float[num29];
                    for (int num30 = 0; num30 < array.Length; num30++)
                    {
                        array[num30] = Vector2.Distance(
                            NPC.Center
                                + spinningpoint.RotatedBy(
                                    (float)num30 * ((float)Math.PI * 2f) / (float)num29
                                        - (float)Math.PI / 2f
                                ),
                            player.Center
                        );
                    }
                    int num31 = 0;
                    for (int num32 = 1; num32 < array.Length; num32++)
                    {
                        if (array[num31] > array[num32])
                        {
                            num31 = num32;
                        }
                    }
                    num31 = ((num31 >= num29 / 2) ? (num31 - num29 / 2) : (num31 + num29 / 2));
                    int num33 = num28;
                    for (int num34 = 0; num34 < array.Length; num34++)
                    {
                        if (num31 == num34)
                            continue;
                        Vector2 center6 =
                            NPC.Center
                            + spinningpoint.RotatedBy(
                                (float)num34 * ((float)Math.PI * 2f) / (float)num29
                                    - (float)Math.PI / 2f
                            );
                        if (num33-- > 0)
                        {
                            int num35 = NPC.NewNPC(
                                NPC.GetSource_FromAI(),
                                (int)center6.X,
                                (int)center6.Y + NPC.height / 2,
                                440,
                                NPC.whoAmI
                            );
                            Main.npc[num35].ai[3] = NPC.whoAmI;
                            Main.npc[num35].netUpdate = true;
                            Main.npc[num35].localAI[1] = NPC.localAI[1];
                        }
                        else
                        {
                            int num36 = list6[-num33 - 1];
                            Main.npc[num36].Center = center6;
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, num36);
                        }
                    }
                    NPC.ai[2] = Projectile.NewProjectile(
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
                    NPC.Center += spinningpoint.RotatedBy(
                        (float)num31 * ((float)Math.PI * 2f) / (float)num29 - (float)Math.PI / 2f
                    );
                    NPC.netUpdate = true;
                    list6.Clear();
                }
                flag3 = true;
                flag4 = true;
                NPC.alpha = 255;
                if (flag2)
                {
                    Vector2 vector10 = Main.projectile[(int)NPC.ai[2]].Center;
                    vector10 -= NPC.Center;
                    if (vector10 == Vector2.Zero)
                    {
                        vector10 = -Vector2.UnitY;
                    }
                    vector10.Normalize();
                    if (Math.Abs(vector10.Y) < 0.77f)
                    {
                        NPC.localAI[2] = 11f;
                    }
                    else if (vector10.Y < 0f)
                    {
                        NPC.localAI[2] = 12f;
                    }
                    else
                    {
                        NPC.localAI[2] = 10f;
                    }
                    int num37 = Math.Sign(vector10.X);
                    if (num37 != 0)
                    {
                        NPC.direction = (NPC.spriteDirection = num37);
                    }
                }
                else
                {
                    Vector2 vector11 = Main.projectile[(int)Main.npc[(int)NPC.ai[3]].ai[2]].Center;
                    vector11 -= NPC.Center;
                    if (vector11 == Vector2.Zero)
                    {
                        vector11 = -Vector2.UnitY;
                    }
                    vector11.Normalize();
                    if (Math.Abs(vector11.Y) < 0.77f)
                    {
                        NPC.localAI[2] = 11f;
                    }
                    else if (vector11.Y < 0f)
                    {
                        NPC.localAI[2] = 12f;
                    }
                    else
                    {
                        NPC.localAI[2] = 10f;
                    }
                    int num38 = Math.Sign(vector11.X);
                    if (num38 != 0)
                    {
                        NPC.direction = (NPC.spriteDirection = num38);
                    }
                }
            }
            else if (NPC.ai[1] >= 90f && NPC.ai[1] < 120f)
            {
                flag3 = true;
                flag4 = true;
                float num39 = (NPC.ai[1] - 90f) / 30f;
                NPC.alpha = 255 - (int)(num39 * 255f);
            }
            else if (NPC.ai[1] >= 120f && NPC.ai[1] < 420f)
            {
                flag4 = true;
                NPC.alpha = 0;
                if (flag2)
                {
                    Vector2 vector12 = Main.projectile[(int)NPC.ai[2]].Center;
                    vector12 -= NPC.Center;
                    if (vector12 == Vector2.Zero)
                    {
                        vector12 = -Vector2.UnitY;
                    }
                    vector12.Normalize();
                    if (Math.Abs(vector12.Y) < 0.77f)
                    {
                        NPC.localAI[2] = 11f;
                    }
                    else if (vector12.Y < 0f)
                    {
                        NPC.localAI[2] = 12f;
                    }
                    else
                    {
                        NPC.localAI[2] = 10f;
                    }
                    int num40 = Math.Sign(vector12.X);
                    if (num40 != 0)
                    {
                        NPC.direction = (NPC.spriteDirection = num40);
                    }
                }
                else
                {
                    Vector2 vector13 = Main.projectile[(int)Main.npc[(int)NPC.ai[3]].ai[2]].Center;
                    vector13 -= NPC.Center;
                    if (vector13 == Vector2.Zero)
                    {
                        vector13 = -Vector2.UnitY;
                    }
                    vector13.Normalize();
                    if (Math.Abs(vector13.Y) < 0.77f)
                    {
                        NPC.localAI[2] = 11f;
                    }
                    else if (vector13.Y < 0f)
                    {
                        NPC.localAI[2] = 12f;
                    }
                    else
                    {
                        NPC.localAI[2] = 10f;
                    }
                    int num41 = Math.Sign(vector13.X);
                    if (num41 != 0)
                    {
                        NPC.direction = (NPC.spriteDirection = num41);
                    }
                }
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= 420f)
            {
                flag4 = true;
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 6f)
        {
            NPC.localAI[2] = 13f;
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= 120f)
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 7f)
        {
            NPC.localAI[2] = 11f;
            Vector2 vec3 = Vector2.Normalize(player.Center - center);
            if (vec3.HasNaNs())
            {
                vec3 = new Vector2(NPC.direction, 0f);
            }
            if (NPC.ai[1] >= 4f && flag2 && (int)(NPC.ai[1] - 4f) % num5 == 0)
            {
                if ((int)(NPC.ai[1] - 4f) / num5 == 2)
                {
                    List<int> list7 = new List<int>();
                    for (int num42 = 0; num42 < 200; num42++)
                    {
                        if (
                            Main.npc[num42].active
                            && Main.npc[num42].type == NPCID.CultistBossClone
                            && Main.npc[num42].ai[3] == (float)NPC.whoAmI
                        )
                        {
                            list7.Add(num42);
                        }
                    }
                    foreach (int item6 in list7)
                    {
                        NPC nPC6 = Main.npc[item6];
                        Vector2 center7 = nPC6.Center;
                        int num43 = Math.Sign(player.Center.X - center7.X);
                        if (num43 != 0)
                        {
                            nPC6.direction = (nPC6.spriteDirection = num43);
                        }

                        if (Main.netMode == NetmodeID.MultiplayerClient)
                            continue;
                        vec3 = Vector2.Normalize(player.Center - center7 + player.velocity * 20f);
                        if (vec3.HasNaNs())
                        {
                            vec3 = new Vector2(NPC.direction, 0f);
                        }
                        Vector2 vector14 = center7 + new Vector2(NPC.direction * 30, 12f);
                        for (int num44 = 0; (float)num44 < 5f; num44++)
                        {
                            Vector2 spinninpoint5 =
                                vec3 * (6f + (float)Main.rand.NextDouble() * 4f);
                            spinninpoint5 = spinninpoint5.RotatedByRandom(1.2566370964050293);
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                vector14.X,
                                vector14.Y,
                                spinninpoint5.X,
                                spinninpoint5.Y,
                                ProjectileID.CultistBossFireBallClone,
                                18,
                                0f,
                                Main.myPlayer
                            );
                        }
                    }
                }
                int num45 = Math.Sign(player.Center.X - center.X);
                if (num45 != 0)
                {
                    NPC.direction = (NPC.spriteDirection = num45);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    vec3 = Vector2.Normalize(player.Center - center + player.velocity * 20f);
                    if (vec3.HasNaNs())
                    {
                        vec3 = new Vector2(NPC.direction, 0f);
                    }
                    Vector2 vector15 = NPC.Center + new Vector2(NPC.direction * 30, 12f);
                    float num46 = 8f;
                    float num47 = (float)Math.PI * 2f / 25f;
                    for (int num48 = 0; (float)num48 < 5f; num48++)
                    {
                        Vector2 spinningpoint2 = vec3 * num46;
                        spinningpoint2 = spinningpoint2.RotatedBy(
                            num47 * (float)num48 - ((float)Math.PI * 2f / 5f - num47) / 2f
                        );
                        float ai =
                            (Main.rand.NextFloat() - 0.5f) * 0.3f * ((float)Math.PI * 2f) / 60f;
                        int num49 = NPC.NewNPC(
                            NPC.GetSource_FromAI(),
                            (int)vector15.X,
                            (int)vector15.Y + 7,
                            522,
                            0,
                            0f,
                            ai,
                            spinningpoint2.X,
                            spinningpoint2.Y
                        );
                        Main.npc[num49].velocity = spinningpoint2;
                        Main.npc[num49].netUpdate = true;
                    }
                }
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= (float)(4 + num5 * num6))
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        else if (NPC.ai[0] == 8f)
        {
            NPC.localAI[2] = 13f;
            if (NPC.ai[1] >= 4f && flag2 && (int)(NPC.ai[1] - 4f) % num7 == 0)
            {
                List<int> list8 = new List<int>();
                for (int num50 = 0; num50 < 200; num50++)
                {
                    if (
                        Main.npc[num50].active
                        && Main.npc[num50].type == NPCID.CultistBossClone
                        && Main.npc[num50].ai[3] == (float)NPC.whoAmI
                    )
                    {
                        list8.Add(num50);
                    }
                }
                int num51 = list8.Count + 1;
                if (num51 > 3)
                {
                    num51 = 3;
                }
                int num52 = Math.Sign(player.Center.X - center.X);
                if (num52 != 0)
                {
                    NPC.direction = (NPC.spriteDirection = num52);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int num53 = 0; num53 < num51; num53++)
                    {
                        Point point = NPC.Center.ToTileCoordinates();
                        Point point2 = Main.player[NPC.target].Center.ToTileCoordinates();
                        Vector2 vector16 = Main.player[NPC.target].Center - NPC.Center;
                        int num54 = 20;
                        int num55 = 3;
                        int num56 = 7;
                        int num57 = 2;
                        int num58 = 0;
                        bool flag6 = vector16.Length() > 2000f;
                        while (!flag6 && num58 < 100)
                        {
                            num58++;
                            int num59 = Main.rand.Next(point2.X - num54, point2.X + num54 + 1);
                            int num60 = Main.rand.Next(point2.Y - num54, point2.Y + num54 + 1);
                            if (
                                (
                                    num60 >= point2.Y - num56
                                    && num60 <= point2.Y + num56
                                    && num59 >= point2.X - num56
                                    && num59 <= point2.X + num56
                                )
                                || (
                                    num60 >= point.Y - num55
                                    && num60 <= point.Y + num55
                                    && num59 >= point.X - num55
                                    && num59 <= point.X + num55
                                )
                                || Main.tile[num59, num60].HasUnactuatedTile
                            )
                            {
                                continue;
                            }

                            bool flag7 = false;
                            flag7 = !(
                                flag7
                                && Collision.SolidTiles(
                                    num59 - num57,
                                    num59 + num57,
                                    num60 - num57,
                                    num60 + num57
                                )
                            );
                            if (!flag7)
                                continue;
                            NPC.NewNPC(
                                NPC.GetSource_FromAI(),
                                num59 * 16 + 8,
                                num60 * 16 + 8,
                                523,
                                0,
                                NPC.whoAmI
                            );
                            break;
                        }
                    }
                }
            }
            NPC.ai[1] += 1f;
            if (NPC.ai[1] >= (float)(4 + num7 * num8))
            {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[3] += 1f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }
        }
        if (!flag2)
        {
            NPC.ai[3] = num11;
        }
        NPC.dontTakeDamage = flag3;
        NPC.chaseable = !flag4;
    }
}
