using Terraria.ModLoader;
using Terraria.ModLoader.IO;

// File for changing NPC to TownNPC
namespace terraari.Content.NPCs;

public class HydrolysistWorldSystem : ModSystem
{
    public static bool unlockedHydrolysist;

    public override void OnWorldLoad() => unlockedHydrolysist = false;

    public override void OnWorldUnload() => unlockedHydrolysist = false;

    public override void SaveWorldData(TagCompound tag)
    {
        if (unlockedHydrolysist)
            tag["unlockedHydrolysist"] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        unlockedHydrolysist = tag.GetBool("unlockedHydrolysist");
    }
}
