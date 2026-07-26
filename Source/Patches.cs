using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WorldTileScouting;

public static class ScoutCommands
{
	private static readonly Texture2D ScoutTex =
		ContentFinder<Texture2D>.Get("Things/Building/Misc/LongRangeMineralScanner");

	public static IEnumerable<Gizmo> GetGizmos(WorldObject obj)
	{
		if (!ScoutTargetUtility.ResearchDone)
			yield break;
		if (!ScoutTargetUtility.IsScoutable(obj, out _))
			yield break;

		var comp = WorldComponent_TileScouting.Get();
		if (comp == null)
			yield break;

		if (comp.TryGetIntel(obj, out var intel) && intel != null)
		{
			yield return new Command_Action
			{
				defaultLabel = "WTS_CommandViewIntel".Translate(),
				defaultDesc = "WTS_CommandViewIntelDesc".Translate(),
				icon = ScoutTex,
				action = () => Find.WindowStack.Add(new Dialog_ScoutIntel(intel, obj))
			};
			yield break;
		}

		var cmd = new Command_Action
		{
			defaultLabel = "WTS_CommandScout".Translate(),
			defaultDesc = "WTS_CommandScoutDesc".Translate(),
			icon = ScoutTex,
			action = () => comp.StartScout(obj)
		};

		if (comp.IsPending(obj))
		{
			int ticks = comp.PendingTicksLeft(obj);
			cmd.Disable("WTS_ScoutInProgress".Translate(ticks.ToStringTicksToPeriod()));
		}

		yield return cmd;
	}

	public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(WorldObject obj)
	{
		if (!ScoutTargetUtility.ResearchDone)
			yield break;
		if (!ScoutTargetUtility.IsScoutable(obj, out _))
			yield break;

		var comp = WorldComponent_TileScouting.Get();
		if (comp == null)
			yield break;

		if (comp.TryGetIntel(obj, out var intel) && intel != null)
		{
			yield return new FloatMenuOption(
				"WTS_CommandViewIntel".Translate(),
				() => Find.WindowStack.Add(new Dialog_ScoutIntel(intel, obj)));
			yield break;
		}

		if (comp.IsPending(obj))
		{
			yield return new FloatMenuOption(
				"WTS_ScoutInProgress".Translate(comp.PendingTicksLeft(obj).ToStringTicksToPeriod()),
				null);
			yield break;
		}

		yield return new FloatMenuOption("WTS_CommandScout".Translate(), () => comp.StartScout(obj));
	}
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.GetGizmos))]
public static class Patch_WorldObject_GetGizmos
{
	public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, WorldObject __instance)
	{
		foreach (var g in __result)
			yield return g;
		foreach (var g in ScoutCommands.GetGizmos(__instance))
			yield return g;
	}
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.GetFloatMenuOptions))]
public static class Patch_WorldObject_GetFloatMenuOptions
{
	public static IEnumerable<FloatMenuOption> Postfix(
		IEnumerable<FloatMenuOption> __result,
		WorldObject __instance)
	{
		foreach (var opt in __result)
			yield return opt;
		foreach (var opt in ScoutCommands.GetFloatMenuOptions(__instance))
			yield return opt;
	}
}

[HarmonyPatch(typeof(MapParent), nameof(MapParent.PostMapGenerate))]
public static class Patch_MapParent_PostMapGenerate
{
	public static void Postfix(MapParent __instance)
	{
		WorldComponent_TileScouting.Get()?.NotifyAttackOrMapGenerated(__instance);
	}
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Destroy))]
public static class Patch_WorldObject_Destroy
{
	public static void Prefix(WorldObject __instance)
	{
		WorldComponent_TileScouting.Get()?.ClearIntel(__instance.ID);
	}
}

[HarmonyPatch(typeof(SettlementUtility), nameof(SettlementUtility.Attack))]
public static class Patch_SettlementUtility_Attack
{
	public static void Prefix(Settlement settlement)
	{
		WorldComponent_TileScouting.Get()?.NotifyAttackOrMapGenerated(settlement);
	}
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.GetInspectString))]
public static class Patch_WorldObject_GetInspectString
{
	public static void Postfix(WorldObject __instance, ref string __result)
	{
		if (!ScoutTargetUtility.ResearchDone || !ScoutTargetUtility.IsScoutable(__instance, out _))
			return;

		var comp = WorldComponent_TileScouting.Get();
		if (comp == null)
			return;

		if (comp.IsPending(__instance))
		{
			__result += "\n" + "WTS_InspectPending".Translate(comp.PendingTicksLeft(__instance).ToStringTicksToPeriod());
			return;
		}

		if (comp.TryGetIntel(__instance, out _))
			__result += "\n" + "WTS_InspectReady".Translate();
	}
}
