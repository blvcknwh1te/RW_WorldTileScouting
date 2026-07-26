using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
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

		switch (obj)
		{
			case Settlement settlement when settlement.Attackable:
				if (settlement.Faction == null || settlement.Faction.IsPlayer)
				{
					failReason = "WTS_Fail_NoHostiles".Translate();
					return false;
				}
				if (settlement.Faction.def?.pawnGroupMakers.NullOrEmpty() == true)
				{
					failReason = "WTS_Fail_NoRosterData".Translate();
					return false;
				}
				return true;

			case Site site:
				if (SiteThreatResolver.LooksScoutableSite(site))
					return true;
				failReason = "WTS_Fail_NoInhabitants".Translate();
				return false;

			case Camp camp:
				if (camp.Faction == null || camp.Faction.IsPlayer)
				{
					failReason = "WTS_Fail_NoHostiles".Translate();
					return false;
				}
				return true;

			case MapParent mapParent:
				if (mapParent.Faction == null || mapParent.Faction.IsPlayer)
				{
					failReason = "WTS_Fail_NoHostiles".Translate();
					return false;
				}
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
			{
				parts.Add($"{part.def.defName}:{part.expectedEnemyCount}:{(int)part.parms.threatPoints}");
				if (part.parms.animalKind != null)
					parts.Add(part.parms.animalKind.defName);
			}

			foreach (var c in SiteThreatResolver.Resolve(site))
				parts.Add(c.mode.ToString());
		}

		return string.Join("|", parts);
	}
}
