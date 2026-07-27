using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorldTileScouting;

public class WorldTileScoutingMod : Mod
{
	public static WorldTileScoutingSettings Settings = null!;

	public WorldTileScoutingMod(ModContentPack content) : base(content)
	{
		Settings = GetSettings<WorldTileScoutingSettings>();
	}

	public override string SettingsCategory() => "WTS_SettingsCategory".Translate();

	public override void DoSettingsWindowContents(Rect inRect)
	{
		var list = new Listing_Standard();
		list.Begin(inRect);

		list.Label("WTS_ColonistsPerSlot".Translate(Settings.colonistsPerScoutSlot));
		Settings.colonistsPerScoutSlot = Mathf.RoundToInt(list.Slider(Settings.colonistsPerScoutSlot, 1f, 20f));
		list.Label("WTS_ColonistsPerSlotTip".Translate(
			Settings.colonistsPerScoutSlot,
			ScoutQuota.MaxConcurrent(),
			ScoutQuota.ColonistCount()).Colorize(ColoredText.SubtleGrayColor));

		list.GapLine();
		if (list.ButtonText("WTS_ResetDefaults".Translate()))
			Settings.ResetToDefaults();

		list.End();
		base.DoSettingsWindowContents(inRect);
	}
}

public class WorldTileScoutingSettings : ModSettings
{
	public int colonistsPerScoutSlot = 5;

	public void ResetToDefaults()
	{
		colonistsPerScoutSlot = 5;
	}

	public override void ExposeData()
	{
		Scribe_Values.Look(ref colonistsPerScoutSlot, "colonistsPerScoutSlot", 5);
		colonistsPerScoutSlot = Mathf.Clamp(colonistsPerScoutSlot, 1, 20);
	}
}

/// <summary>
/// Одновременно: ceil(колонисты / N), минимум 1.
/// </summary>
public static class ScoutQuota
{
	public static int ColonistCount()
	{
		if (Current.ProgramState != ProgramState.Playing || Find.World == null)
			return 0;
		return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Count;
	}

	public static int MaxConcurrent()
	{
		int per = WorldTileScoutingMod.Settings?.colonistsPerScoutSlot ?? 5;
		if (per < 1)
			per = 1;
		return Mathf.Max(1, Mathf.CeilToInt(ColonistCount() / (float)per));
	}
}

[StaticConstructorOnStartup]
public static class WorldTileScoutingBootstrap
{
	public const string HarmonyId = "local.WorldTileScouting";

	static WorldTileScoutingBootstrap()
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
