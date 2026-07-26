using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WorldTileScouting;

public static class ScoutTargetUtility
{
	public static bool ResearchDone =>
		WorldTileScoutingDefOf.WorldTileScouting != null
		&& WorldTileScoutingDefOf.WorldTileScouting.IsFinished;

	public static bool IsScoutable(WorldObject? obj, out string? failReason)
	{
		failReason = null;
		if (obj == null)
		{
			failReason = "WTS_Fail_NoTarget".Translate();
			return false;
		}

		if (!ResearchDone)
		{
			failReason = "WTS_Fail_NeedResearch".Translate();
			return false;
		}

		if (obj.Faction == null || obj.Faction.IsPlayer)
		{
			failReason = "WTS_Fail_NoHostiles".Translate();
			return false;
		}

		if (obj.Faction.def?.pawnGroupMakers.NullOrEmpty() == true)
		{
			failReason = "WTS_Fail_NoRosterData".Translate();
			return false;
		}

		switch (obj)
		{
			case Settlement settlement when settlement.Attackable:
				return true;
			case Site site:
				if (site.ActualThreatPoints > 0f || site.parts.Any(p => p.expectedEnemyCount > 0))
					return true;
				failReason = "WTS_Fail_NoInhabitants".Translate();
				return false;
			case Camp:
				return true;
			case MapParent:
				return true;
			default:
				failReason = "WTS_Fail_Unsupported".Translate();
				return false;
		}
	}

	public static string Fingerprint(WorldObject obj)
	{
		var parts = new List<string>
		{
			obj.GetUniqueLoadID(),
			obj.def.defName,
			obj.Faction?.GetUniqueLoadID() ?? "nofaction",
			obj.Label
		};

		if (obj is Site site && !site.parts.NullOrEmpty())
		{
			parts.Add(site.MainSitePartDef?.defName ?? "nosite");
			parts.Add(((int)site.ActualThreatPoints).ToString());
			foreach (var part in site.parts)
				parts.Add($"{part.def.defName}:{part.expectedEnemyCount}:{(int)part.parms.threatPoints}");
		}

		return string.Join("|", parts);
	}

	public static IEnumerable<PawnGroupMakerParms> BuildGroupParms(WorldObject obj)
	{
		var faction = obj.Faction;
		if (faction == null)
			yield break;

		switch (obj)
		{
			case Site site:
				foreach (var parms in BuildSiteParms(site))
					yield return parms;
				yield break;
			case Settlement:
			case Camp:
			case MapParent:
			{
				var points = Mathf.Max(
					RimWorld.BaseGen.SymbolResolver_Settlement.DefaultPawnsPoints.Average,
					faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Settlement));
				yield return new PawnGroupMakerParms
				{
					groupKind = PawnGroupKindDefOf.Settlement,
					tile = obj.Tile,
					faction = faction,
					points = points,
					inhabitants = true,
					seed = obj.ID
				};
				yield break;
			}
		}
	}

	private static IEnumerable<PawnGroupMakerParms> BuildSiteParms(Site site)
	{
		var faction = site.Faction;
		if (faction == null)
			yield break;

		foreach (var part in site.parts)
		{
			var worker = part.def.Worker;
			var seed = OutpostSitePartUtility.GetPawnGroupMakerSeed(part.parms);
			var points = Mathf.Max(part.parms.threatPoints, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Settlement));

			if (worker is SitePartWorker_WorkSite workSite)
			{
				var half = points / 2f;
				var workerKind = workSite.WorkerGroupKind;
				if (faction.def.pawnGroupMakers.Any(m => m.kindDef == workerKind))
				{
					yield return new PawnGroupMakerParms
					{
						groupKind = workerKind,
						tile = site.Tile,
						faction = faction,
						inhabitants = true,
						seed = seed,
						points = Mathf.Max(half, faction.def.MinPointsToGeneratePawnGroup(workerKind))
					};
				}

				var fighterKind = faction.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat)
					? PawnGroupKindDefOf.Combat
					: PawnGroupKindDefOf.Settlement;
				yield return new PawnGroupMakerParms
				{
					groupKind = fighterKind,
					tile = site.Tile,
					faction = faction,
					inhabitants = true,
					generateFightersOnly = true,
					seed = seed,
					points = Mathf.Max(half, faction.def.MinPointsToGeneratePawnGroup(fighterKind))
				};
			}
			else
			{
				yield return new PawnGroupMakerParms
				{
					groupKind = PawnGroupKindDefOf.Settlement,
					tile = site.Tile,
					faction = faction,
					inhabitants = true,
					seed = seed,
					points = points
				};
			}
		}
	}
}
