using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WorldTileScouting;

/// <summary>
/// Универсальная классификация угрозы сайта по Worker / GenStep / parms — без списков defName квестов.
/// </summary>
public static class SiteThreatResolver
{
	public sealed class Contribution
	{
		public ScoutThreatMode mode;
		public List<PawnKindDef> kinds = new();
		public bool includeGearSamples = true;
		public string? noteKey;
	}

	public static List<Contribution> Resolve(WorldObject obj)
	{
		return obj switch
		{
			Site site => ResolveSite(site),
			Settlement or Camp or MapParent => new List<Contribution> { ResolveSettlementLike(obj) },
			_ => new List<Contribution>()
		};
	}

	public static bool LooksScoutableSite(Site site)
	{
		if (site?.parts == null)
			return false;
		if (site.ActualThreatPoints > 0f)
			return true;
		foreach (var part in site.parts)
		{
			if (part == null)
				continue;
			if (part.expectedEnemyCount > 0)
				return true;
			if (part.def?.conditionCauserDef != null)
				return true;
			if (part.def != null && part.def.defaultHidden)
				return true;
			if (part.def?.wantsThreatPoints == true)
				return true;
			if (ClassifyPart(site, part).mode != ScoutThreatMode.Unknown)
				return true;
		}
		return false;
	}

	private static Contribution ResolveSettlementLike(WorldObject obj)
	{
		var faction = obj.Faction;
		var kinds = new List<PawnKindDef>();
		if (faction?.def != null && !faction.def.pawnGroupMakers.NullOrEmpty())
		{
			var points = Mathf.Max(
				RimWorld.BaseGen.SymbolResolver_Settlement.DefaultPawnsPoints.Average,
				faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Settlement));
			kinds.AddRange(SafeExample(new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Settlement,
				tile = obj.Tile,
				faction = faction,
				points = points,
				inhabitants = true,
				seed = obj.ID
			}));
		}

		return new Contribution
		{
			mode = ScoutThreatMode.FactionRoster,
			kinds = kinds,
			includeGearSamples = true
		};
	}

	private static List<Contribution> ResolveSite(Site site)
	{
		var list = new List<Contribution>();
		if (site.parts == null)
			return list;

		foreach (var part in site.parts)
		{
			if (part?.def == null)
				continue;
			list.Add(ClassifyPart(site, part));
		}

		if (list.Count == 0)
		{
			list.Add(new Contribution
			{
				mode = ScoutThreatMode.Unknown,
				noteKey = "WTS_ModeUnknown"
			});
		}

		return list;
	}

	private static Contribution ClassifyPart(Site site, SitePart part)
	{
		var worker = part.def.Worker;
		var parms = part.parms;
		var points = Mathf.Max(0f, parms.threatPoints);
		var seed = SafeSeed(parms, part);

		// 1) DistressCall (Anomaly) — любой subclass базового worker
		if (ModsConfig.AnomalyActive && worker is SitePartWorker_DistressCall)
		{
			var c = TryFleshbeasts(site, points, seed);
			c.noteKey = "WTS_ModeDistressCall";
			return c;
		}

		// Soft: tag DistressCall без загруженного worker type (моды)
		if (HasTag(part, "DistressCall") && ModsConfig.AnomalyActive)
		{
			var c = TryFleshbeasts(site, points, seed);
			c.noteKey = "WTS_ModeDistressCall";
			return c;
		}

		// 2) Manhunters
		if (worker is SitePartWorker_Manhunters || parms.animalKind != null)
		{
			return ResolveManhunters(site, parms, points, seed);
		}

		// 3) Sleeping mechanoids
		if (worker is SitePartWorker_SleepingMechanoids || HasTag(part, "SleepingMechanoids"))
		{
			return ResolveMechanoids(site, points, seed, "WTS_ModeSleepingMechanoids");
		}

		// 4) Mech cluster
		if (worker is SitePartWorker_MechCluster || HasTag(part, "MechCluster") || HasGenStepType(part, "GenStep_MechCluster"))
		{
			return ResolveMechanoids(site, points, seed, "WTS_ModeMechCluster");
		}

		// 4b) Odyssey mechanoid relay (soft by type name)
		if (WorkerNameIs(worker, "SitePartWorker_MechanoidRelay"))
		{
			return ResolveMechanoids(site, Mathf.Max(points, 300f), seed, "WTS_ModeMechanoidRelay");
		}

		// 4c) Insect lair — насекомые по points, если фракция доступна
		if (WorkerNameIs(worker, "SitePartWorker_InsectLair") || HasTag(part, "InsectLair"))
		{
			return ResolveInsects(site, points, seed);
		}

		// 5) Turrets — охрана фракции сайта (если есть) + режим Turrets
		if (worker is SitePartWorker_Turrets)
		{
			var c = ResolveFactionSettlement(site, points, seed, forceFightersOnly: true);
			c.mode = ScoutThreatMode.Turrets;
			c.noteKey = "WTS_ModeTurrets";
			return c;
		}

		// 6) WorkSite
		if (worker is SitePartWorker_WorkSite workSite)
		{
			return ResolveWorkSite(site, workSite, points, seed);
		}

		// 7) Condition causer
		if (worker is SitePartWorker_ConditionCauser || part.def.conditionCauserDef != null || HasTag(part, "QuestConditionCauser"))
		{
			return new Contribution
			{
				mode = ScoutThreatMode.ConditionCauser,
				includeGearSamples = false,
				noteKey = "WTS_ModeConditionCauser",
				kinds = new List<PawnKindDef>()
			};
		}

		// 8) Ancient complex
		if (worker is SitePartWorker_AncientComplex || HasTag(part, "AncientComplex") || parms.ancientLayoutStructureSketch != null)
		{
			var c = ResolveFactionSettlement(site, Mathf.Max(points, parms.threatPoints), seed);
			if (c.kinds.Count == 0)
			{
				c.mode = ScoutThreatMode.Complex;
				c.includeGearSamples = false;
				c.noteKey = "WTS_ModeComplex";
			}
			else
			{
				c.mode = ScoutThreatMode.Complex;
				c.noteKey = "WTS_ModeComplex";
			}
			return c;
		}

		// 9) Ambush / hidden
		if (worker is SitePartWorker_Ambush || part.def.defaultHidden)
		{
			return new Contribution
			{
				mode = ScoutThreatMode.AmbushDeferred,
				includeGearSamples = false,
				noteKey = "WTS_ModeAmbush",
				kinds = new List<PawnKindDef>()
			};
		}

		// 10) Abandoned / no pawns on settlement gen
		if (SettlementGenSkipsPawns(part) || HasTag(part, "AbandonedSettlement") || WorkerNameIs(worker, "SitePartWorker_AbandonedSettlement"))
		{
			return new Contribution
			{
				mode = ScoutThreatMode.Abandoned,
				includeGearSamples = false,
				noteKey = "WTS_ModeAbandoned",
				kinds = new List<PawnKindDef>()
			};
		}

		// 11) Outpost-like (BanditCamp, RaidSource, plain Outpost)
		if (worker is SitePartWorker_Outpost || HasGenStepType(part, "GenStep_Outpost") || HasTag(part, "Outpost"))
		{
			return ResolveFactionSettlement(site, points, seed);
		}

		// 12) Fallback
		if (site.Faction?.def != null
			&& !site.Faction.def.pawnGroupMakers.NullOrEmpty()
			&& (part.def.wantsThreatPoints || points > 0f))
		{
			return ResolveFactionSettlement(site, points, seed);
		}

		return new Contribution
		{
			mode = ScoutThreatMode.Unknown,
			includeGearSamples = false,
			noteKey = "WTS_ModeUnknown",
			kinds = new List<PawnKindDef>()
		};
	}

	private static Contribution TryFleshbeasts(Site site, float points, int seed)
	{
		var kinds = new List<PawnKindDef>();
		var entities = Faction.OfEntities;
		var fleshKind = DefDatabase<PawnGroupKindDef>.GetNamedSilentFail("Fleshbeasts");
		if (entities != null && fleshKind != null)
		{
			var p = Mathf.Max(points, entities.def.MinPointsToGeneratePawnGroup(fleshKind));
			// Как ванильный distress: живые твари ~ Evaluate(desired) — берём Actual/desired-like points
			var desired = site.desiredThreatPoints > 0f ? site.desiredThreatPoints : p;
			p = Mathf.Max(p, desired * 0.5f);
			kinds.AddRange(SafeExample(new PawnGroupMakerParms
			{
				groupKind = fleshKind,
				faction = entities,
				points = p,
				tile = site.Tile,
				seed = seed,
				raidStrategy = RaidStrategyDefOf.ImmediateAttack
			}));
		}

		if (kinds.Count == 0)
		{
			// Soft fallback без group maker
			kinds.AddRange(EstimateFromNamedKinds(points, seed, new[] { "Fingerspike", "Toughspike", "Trispike", "Bulbfreak" }));
		}

		return new Contribution
		{
			mode = ScoutThreatMode.Fleshbeasts,
			kinds = kinds,
			includeGearSamples = false
		};
	}

	private static Contribution ResolveManhunters(Site site, SitePartParams parms, float points, int seed)
	{
		var kinds = new List<PawnKindDef>();
		var animal = parms.animalKind;
		if (animal == null)
			ManhunterPackGenStepUtility.TryGetAnimalsKind(points, site.Tile, out animal);

		if (animal != null)
		{
			int count = AggressiveAnimalIncidentUtility.GetAnimalsCount(animal, Mathf.Max(points, animal.combatPower));
			count = Mathf.Clamp(count, 1, 40);
			for (int i = 0; i < count; i++)
				kinds.Add(animal);
		}

		return new Contribution
		{
			mode = ScoutThreatMode.Manhunters,
			kinds = kinds,
			includeGearSamples = false,
			noteKey = "WTS_ModeManhunters"
		};
	}

	private static Contribution ResolveMechanoids(Site site, float points, int seed, string noteKey)
	{
		var kinds = new List<PawnKindDef>();
		var mechs = Faction.OfMechanoids;
		if (mechs != null)
		{
			var p = Mathf.Max(points, mechs.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));
			kinds.AddRange(SafeExample(new PawnGroupMakerParms
			{
				tile = site.Tile,
				faction = mechs,
				groupKind = PawnGroupKindDefOf.Combat,
				points = p,
				seed = seed
			}));
		}

		return new Contribution
		{
			mode = ScoutThreatMode.Mechanoids,
			kinds = kinds,
			includeGearSamples = true,
			noteKey = noteKey
		};
	}

	private static Contribution ResolveInsects(Site site, float points, int seed)
	{
		var kinds = new List<PawnKindDef>();
		var insects = Faction.OfInsects;
		if (insects != null)
		{
			var p = Mathf.Max(points, insects.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));
			kinds.AddRange(SafeExample(new PawnGroupMakerParms
			{
				tile = site.Tile,
				faction = insects,
				groupKind = PawnGroupKindDefOf.Combat,
				points = p,
				seed = seed
			}));
		}

		return new Contribution
		{
			mode = ScoutThreatMode.Insects,
			kinds = kinds,
			includeGearSamples = false,
			noteKey = "WTS_ModeInsects"
		};
	}

	private static Contribution ResolveWorkSite(Site site, SitePartWorker_WorkSite workSite, float points, int seed)
	{
		var faction = site.Faction;
		var kinds = new List<PawnKindDef>();
		if (faction?.def == null)
		{
			return new Contribution { mode = ScoutThreatMode.FactionRoster, kinds = kinds, noteKey = "WTS_ModeWorkSite" };
		}

		var half = Mathf.Max(points / 2f, 50f);
		var workerKind = workSite.WorkerGroupKind;
		if (faction.def.pawnGroupMakers.Any(m => m.kindDef == workerKind))
		{
			kinds.AddRange(SafeExample(new PawnGroupMakerParms
			{
				groupKind = workerKind,
				tile = site.Tile,
				faction = faction,
				inhabitants = true,
				seed = seed,
				points = Mathf.Max(half, faction.def.MinPointsToGeneratePawnGroup(workerKind))
			}));
		}

		var fighterKind = faction.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat)
			? PawnGroupKindDefOf.Combat
			: PawnGroupKindDefOf.Settlement;
		kinds.AddRange(SafeExample(new PawnGroupMakerParms
		{
			groupKind = fighterKind,
			tile = site.Tile,
			faction = faction,
			inhabitants = true,
			generateFightersOnly = true,
			seed = seed ^ 17,
			points = Mathf.Max(half, faction.def.MinPointsToGeneratePawnGroup(fighterKind))
		}));

		return new Contribution
		{
			mode = ScoutThreatMode.FactionRoster,
			kinds = kinds,
			includeGearSamples = true,
			noteKey = "WTS_ModeWorkSite"
		};
	}

	private static Contribution ResolveFactionSettlement(Site site, float points, int seed, bool forceFightersOnly = false)
	{
		var faction = site.Faction;
		var kinds = new List<PawnKindDef>();
		if (faction?.def != null && !faction.def.pawnGroupMakers.NullOrEmpty())
		{
			var groupKind = forceFightersOnly && faction.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat)
				? PawnGroupKindDefOf.Combat
				: PawnGroupKindDefOf.Settlement;
			var p = Mathf.Max(points, faction.def.MinPointsToGeneratePawnGroup(groupKind));
			kinds.AddRange(SafeExample(new PawnGroupMakerParms
			{
				groupKind = groupKind,
				tile = site.Tile,
				faction = faction,
				points = p,
				inhabitants = true,
				generateFightersOnly = forceFightersOnly,
				seed = seed
			}));
		}

		return new Contribution
		{
			mode = ScoutThreatMode.FactionRoster,
			kinds = kinds,
			includeGearSamples = true
		};
	}

	private static bool SettlementGenSkipsPawns(SitePart part)
	{
		foreach (var stepDef in part.def.ExtraGenSteps)
		{
			if (stepDef?.genStep == null)
				continue;
			if (stepDef.genStep is GenStep_Settlement settlement && !settlement.generatePawns)
				return true;
			if (stepDef.genStep is GenStep_Outpost outpost && outpost.settlementDontGeneratePawns)
				return true;
			var t = stepDef.genStep.GetType().Name;
			if (t.Contains("Fleshmass") || t.Contains("PitBurrow"))
				return true;
		}
		return false;
	}

	private static bool HasTag(SitePart part, string tag) =>
		part.def.tags != null && part.def.tags.Contains(tag);

	private static bool HasGenStepType(SitePart part, string typeName) =>
		part.def.ExtraGenSteps.Any(s => s?.genStep != null && s.genStep.GetType().Name == typeName);

	private static bool WorkerNameIs(SitePartWorker? worker, string typeName) =>
		worker != null && worker.GetType().Name == typeName;

	private static int SafeSeed(SitePartParams parms, SitePart part)
	{
		try
		{
			return OutpostSitePartUtility.GetPawnGroupMakerSeed(parms);
		}
		catch
		{
			return part.site?.ID ?? part.GetHashCode();
		}
	}

	private static List<PawnKindDef> SafeExample(PawnGroupMakerParms parms)
	{
		try
		{
			return PawnGroupMakerUtility.GeneratePawnKindsExample(parms).ToList();
		}
		catch
		{
			return new List<PawnKindDef>();
		}
	}

	private static List<PawnKindDef> EstimateFromNamedKinds(float points, int seed, string[] names)
	{
		var pool = names.Select(DefDatabase<PawnKindDef>.GetNamedSilentFail).Where(k => k != null).Cast<PawnKindDef>().ToList();
		var result = new List<PawnKindDef>();
		if (pool.Count == 0)
			return result;

		Rand.PushState(seed);
		try
		{
			float remaining = Mathf.Max(points, 100f);
			int guard = 48;
			while (remaining > 25f && guard-- > 0)
			{
				var pick = pool[Rand.Range(0, pool.Count)];
				result.Add(pick);
				remaining -= Mathf.Max(30f, pick.combatPower);
			}
		}
		finally
		{
			Rand.PopState();
		}

		return result;
	}
}
