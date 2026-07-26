using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WorldTileScouting;

public static class ScoutIntelAnalyzer
{
	private const int IntelLifetimeDays = 15;

	public static ScoutIntel Analyze(WorldObject obj)
	{
		var intel = new ScoutIntel
		{
			worldObjectId = obj.ID,
			fingerprint = ScoutTargetUtility.Fingerprint(obj),
			targetLabel = obj.LabelCap,
			factionName = obj.Faction?.Name ?? string.Empty,
			createdTick = Find.TickManager.TicksGame,
			expireTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * IntelLifetimeDays
		};

		var contributions = SiteThreatResolver.Resolve(obj);
		var allKinds = new List<PawnKindDef>();
		bool anyGear = false;
		ScoutThreatMode primary = ScoutThreatMode.Unknown;
		string? banner = null;
		int modePriority = -1;

		foreach (var c in contributions)
		{
			allKinds.AddRange(c.kinds);
			if (c.includeGearSamples)
				anyGear = true;
			int p = ModePriority(c.mode);
			if (p > modePriority)
			{
				modePriority = p;
				primary = c.mode;
				banner = c.noteKey;
			}
			else if (banner == null && !c.noteKey.NullOrEmpty())
			{
				banner = c.noteKey;
			}
		}

		intel.threatMode = primary;
		intel.modeBannerKey = banner ?? DefaultBannerKey(primary);
		intel.expectedCount = allKinds.Count;

		if (allKinds.Count == 0)
		{
			intel.kindBreakdown.Add(new ScoutIntel.KindCount
			{
				label = EmptyLabel(primary),
				count = 0
			});
			ApplyEmptyCombatProfile(intel, primary);
			return intel;
		}

		if (anyGear && primary is ScoutThreatMode.FactionRoster or ScoutThreatMode.Mechanoids or ScoutThreatMode.Turrets or ScoutThreatMode.Complex)
			FillFromKinds(intel, allKinds, includeGear: true);
		else
			FillFromKinds(intel, allKinds, includeGear: false);

		return intel;
	}

	private static int ModePriority(ScoutThreatMode mode) => mode switch
	{
		ScoutThreatMode.Fleshbeasts => 100,
		ScoutThreatMode.Manhunters => 90,
		ScoutThreatMode.Mechanoids => 85,
		ScoutThreatMode.Insects => 84,
		ScoutThreatMode.AmbushDeferred => 70,
		ScoutThreatMode.ConditionCauser => 60,
		ScoutThreatMode.Complex => 55,
		ScoutThreatMode.Turrets => 50,
		ScoutThreatMode.Abandoned => 40,
		ScoutThreatMode.FactionRoster => 30,
		_ => 0
	};

	private static string DefaultBannerKey(ScoutThreatMode mode) => mode switch
	{
		ScoutThreatMode.Fleshbeasts => "WTS_ModeDistressCall",
		ScoutThreatMode.Manhunters => "WTS_ModeManhunters",
		ScoutThreatMode.Mechanoids => "WTS_ModeSleepingMechanoids",
		ScoutThreatMode.Insects => "WTS_ModeInsects",
		ScoutThreatMode.AmbushDeferred => "WTS_ModeAmbush",
		ScoutThreatMode.ConditionCauser => "WTS_ModeConditionCauser",
		ScoutThreatMode.Complex => "WTS_ModeComplex",
		ScoutThreatMode.Abandoned => "WTS_ModeAbandoned",
		ScoutThreatMode.Turrets => "WTS_ModeTurrets",
		ScoutThreatMode.Unknown => "WTS_ModeUnknown",
		_ => string.Empty
	};

	private static string EmptyLabel(ScoutThreatMode mode) => mode switch
	{
		ScoutThreatMode.Abandoned => "WTS_EmptyAbandoned".Translate(),
		ScoutThreatMode.ConditionCauser => "WTS_EmptyConditionCauser".Translate(),
		ScoutThreatMode.AmbushDeferred => "WTS_EmptyAmbush".Translate(),
		ScoutThreatMode.Unknown => "WTS_UnknownRoster".Translate(),
		_ => "WTS_UnknownRoster".Translate()
	};

	private static void ApplyEmptyCombatProfile(ScoutIntel intel, ScoutThreatMode mode)
	{
		intel.hasArmor = false;
		intel.armorSamples.Clear();
		intel.weaponSamples.Clear();
		intel.armorCategories.Clear();
		intel.weaponCategories.Clear();
		switch (mode)
		{
			case ScoutThreatMode.Fleshbeasts:
			case ScoutThreatMode.Manhunters:
			case ScoutThreatMode.Insects:
				intel.rangedShare = 0.15f;
				intel.meleeShare = 0.85f;
				break;
			case ScoutThreatMode.Mechanoids:
				intel.rangedShare = 0.7f;
				intel.meleeShare = 0.3f;
				break;
			default:
				intel.rangedShare = 0f;
				intel.meleeShare = 0f;
				break;
		}
	}

	private static void FillFromKinds(ScoutIntel intel, List<PawnKindDef> kinds, bool includeGear)
	{
		intel.kindBreakdown = kinds
			.GroupBy(k => k.defName)
			.Select(g => new ScoutIntel.KindCount
			{
				defName = g.Key,
				label = g.First().LabelCap.Resolve(),
				count = g.Count()
			})
			.OrderByDescending(k => k.count)
			.ThenBy(k => k.label)
			.ToList();

		float rangedWeight = 0f;
		float meleeWeight = 0f;
		var armorDefWeights = new Dictionary<ThingDef, float>();
		var armorCatWeights = new Dictionary<ThingCategoryDef, float>();
		var weaponDefWeights = new Dictionary<ThingDef, float>();
		var weaponCatWeights = new Dictionary<ThingCategoryDef, float>();
		var armorPresent = false;

		foreach (var kind in kinds)
		{
			var weapons = includeGear ? CandidateWeapons(kind).ToList() : new List<ThingDef>();
			if (weapons.Count == 0)
			{
				// Существа без оружия / без тегов — ближний профиль
				if (kind.race?.race != null && !kind.race.race.Humanlike)
					meleeWeight += 1f;
				else if (!includeGear)
					meleeWeight += 0.85f;
				else
					meleeWeight += 1f;
			}
			else
			{
				float ranged = weapons.Count(w => w.IsRangedWeapon);
				float melee = weapons.Count(w => w.IsMeleeWeapon);
				float total = Mathf.Max(1f, ranged + melee);
				rangedWeight += ranged / total;
				meleeWeight += melee / total;

				foreach (var weapon in weapons)
				{
					AddWeight(weaponDefWeights, weapon, 1f / weapons.Count);
					if (weapon.thingCategories != null)
					{
						foreach (var cat in weapon.thingCategories)
							AddWeight(weaponCatWeights, cat, 1f / weapons.Count);
					}
				}
			}

			if (!includeGear)
				continue;

			var apparel = CandidateApparel(kind).ToList();
			foreach (var app in apparel)
			{
				bool armored = IsArmoredApparel(app);
				if (!armored)
					continue;
				armorPresent = true;
				AddWeight(armorDefWeights, app, 1f / Mathf.Max(1, apparel.Count));
				if (app.thingCategories != null)
				{
					foreach (var cat in app.thingCategories)
						AddWeight(armorCatWeights, cat, 1f / Mathf.Max(1, apparel.Count));
				}
			}
		}

		float combatTotal = Mathf.Max(0.0001f, rangedWeight + meleeWeight);
		intel.rangedShare = rangedWeight / combatTotal;
		intel.meleeShare = meleeWeight / combatTotal;
		intel.hasArmor = armorPresent;
		if (includeGear)
		{
			intel.armorSamples = TopDefs(armorDefWeights, 8);
			intel.weaponSamples = TopDefs(weaponDefWeights, 10);
			intel.armorCategories = TopCats(armorCatWeights, 8);
			intel.weaponCategories = TopCats(weaponCatWeights, 10);
		}
		else
		{
			intel.armorSamples.Clear();
			intel.weaponSamples.Clear();
			intel.armorCategories.Clear();
			intel.weaponCategories.Clear();
		}
	}

	private static IEnumerable<ThingDef> CandidateWeapons(PawnKindDef kind)
	{
		if (kind.weaponTags.NullOrEmpty())
			yield break;

		foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
		{
			if (!def.IsWeapon || def.weaponTags.NullOrEmpty())
				continue;
			if (!def.weaponTags.Any(t => kind.weaponTags.Contains(t)))
				continue;
			if (kind.weaponMoney != FloatRange.Zero)
			{
				float cost = def.BaseMarketValue;
				if (cost > kind.weaponMoney.max * 1.25f)
					continue;
			}
			yield return def;
		}
	}

	private static IEnumerable<ThingDef> CandidateApparel(PawnKindDef kind)
	{
		if (kind.apparelRequired != null)
		{
			foreach (var req in kind.apparelRequired)
				yield return req;
		}

		if (kind.apparelTags.NullOrEmpty())
			yield break;

		foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
		{
			if (def.apparel == null || def.apparel.tags.NullOrEmpty())
				continue;
			if (!def.apparel.tags.Any(t => kind.apparelTags.Contains(t)))
				continue;
			if (kind.apparelMoney != FloatRange.Zero)
			{
				float cost = def.BaseMarketValue;
				if (cost > kind.apparelMoney.max * 1.25f)
					continue;
			}
			yield return def;
		}
	}

	private static bool IsArmoredApparel(ThingDef def)
	{
		if (def?.apparel == null)
			return false;
		if (def.statBases != null)
		{
			float sharp = def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp);
			float blunt = def.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt);
			if (sharp > 0.01f || blunt > 0.01f)
				return true;
		}

		return def.thingCategories != null && def.thingCategories.Any(c =>
			c.defName.IndexOf("Armor", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| c.defName.IndexOf("Flak", System.StringComparison.OrdinalIgnoreCase) >= 0);
	}

	private static void AddWeight<T>(Dictionary<T, float> map, T key, float amount) where T : notnull
	{
		map.TryGetValue(key, out float cur);
		map[key] = cur + amount;
	}

	private static List<ThingDef> TopDefs(Dictionary<ThingDef, float> weights, int take) =>
		weights.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.label).Take(take).Select(kv => kv.Key).ToList();

	private static List<ThingCategoryDef> TopCats(Dictionary<ThingCategoryDef, float> weights, int take) =>
		weights.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.label).Take(take).Select(kv => kv.Key).ToList();
}
