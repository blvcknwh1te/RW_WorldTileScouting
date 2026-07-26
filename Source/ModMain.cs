using HarmonyLib;
using RimWorld;
using Verse;

namespace WorldTileScouting;

[StaticConstructorOnStartup]
public static class WorldTileScoutingMod
{
	public const string HarmonyId = "local.WorldTileScouting";

	static WorldTileScoutingMod()
	{
		new Harmony(HarmonyId).PatchAll();
		Log.Message("[WorldTileScouting] active.");
	}
}

[DefOf]
public static class WorldTileScoutingDefOf
{
	public static ResearchProjectDef WorldTileScouting = null!;

	static WorldTileScoutingDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(WorldTileScoutingDefOf));
	}
}
