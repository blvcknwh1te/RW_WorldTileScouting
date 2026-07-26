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
		var kinds = new List<PawnKindDef>();
		foreach (var parms in ScoutTargetUtility.BuildGroupParms(obj))
		{
			try
			{
				kinds.AddRange(PawnGroupMakerUtility.GeneratePawnKindsExample(parms));
			}
			catch
			{
				// Некоторые модовые фракции могут не иметь подходящего group maker.
			}
		}

		var intel = new ScoutIntel
		{
			worldObjectId = obj.ID,
			fingerprint = ScoutTargetUtility.Fingerprint(obj),
			targetLabel = obj.LabelCap,
			factionName = obj.Faction?.Name ?? string.Empty,
			createdTick = Find.TickManager.TicksGame,
			expireTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * IntelLifetimeDays,
			expectedCount = kinds.Count
		};

		if (kinds.Count == 0)
		{
			intel.kindBreakdown.Add(new ScoutIntel.KindCount
			{
				label = "WTS_UnknownRoster".Translate(),
				count = 0
			});
			return intel;
		}

		intel.kindBreakdown = kinds
			.GroupBy(k => k.LabelCap.Resolve())
			.Select(g => new ScoutIntel.KindCount { label = g.Key, count = g.Count() })
			.OrderByDescending(k => k.count)
			.ThenBy(k => k.label)
			.ToList();

		float rangedWeight = 0f;
		float meleeWeight = 0f;
		var armorTagWeights = new Dictionary<string, float>();
		var armorCatWeights = new Dictionary<string, float>();
		var weaponTagWeights = new Dictionary<string, float>();
		var weaponCatWeights = new Dictionary<string, float>();
		var armorPresent = false;

		foreach (var kind in kinds)
		{
			var weapons = CandidateWeapons(kind).ToList();
			if (weapons.Count == 0)
			{
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
					if (weapon.weaponTags != null)
					{
						foreach (var tag in weapon.weaponTags)
							AddWeight(weaponTagWeights, PrettyTag(tag), 1f / weapons.Count);
					}

					if (weapon.thingCategories != null)
					{
						foreach (var cat in weapon.thingCategories)
							AddWeight(weaponCatWeights, cat.LabelCap.Resolve(), 1f / weapons.Count);
					}
				}
			}

			var apparel = CandidateApparel(kind).ToList();
			foreach (var app in apparel)
			{
				bool armored = IsArmoredApparel(app);
				if (armored)
					armorPresent = true;

				if (app.apparel?.tags != null)
				{
					foreach (var tag in app.apparel.tags)
					{
						if (armored || LooksLikeArmorTag(tag))
							AddWeight(armorTagWeights, PrettyTag(tag), 1f / Mathf.Max(1, apparel.Count));
					}
				}

				if (armored && app.thingCategories != null)
				{
					foreach (var cat in app.thingCategories)
						AddWeight(armorCatWeights, cat.LabelCap.Resolve(), 1f / Mathf.Max(1, apparel.Count));
				}
			}

			if (!armorPresent && kind.apparelRequired != null)
			{
				foreach (var req in kind.apparelRequired)
				{
					if (IsArmoredApparel(req))
						armorPresent = true;
				}
			}
		}

		float combatTotal = Mathf.Max(0.0001f, rangedWeight + meleeWeight);
		intel.rangedShare = rangedWeight / combatTotal;
		intel.meleeShare = meleeWeight / combatTotal;
		intel.hasArmor = armorPresent;
		intel.armorTags = TopLabels(armorTagWeights, 8);
		intel.armorCategories = TopLabels(armorCatWeights, 8);
		intel.weaponTags = TopLabels(weaponTagWeights, 10);
		intel.weaponCategories = TopLabels(weaponCatWeights, 10);
		return intel;
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

		if (def.apparel.tags != null && def.apparel.tags.Any(LooksLikeArmorTag))
			return true;

		return def.thingCategories != null && def.thingCategories.Any(c =>
			c.defName.IndexOf("Armor", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| c.defName.IndexOf("Flak", System.StringComparison.OrdinalIgnoreCase) >= 0);
	}

	private static bool LooksLikeArmorTag(string tag)
	{
		if (tag.NullOrEmpty())
			return false;
		return tag.IndexOf("Armor", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| tag.IndexOf("Flak", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| tag.IndexOf("Marine", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| tag.IndexOf("Plate", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| tag.IndexOf("Cataphract", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| tag.IndexOf("Prestige", System.StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static void AddWeight(Dictionary<string, float> map, string key, float amount)
	{
		if (key.NullOrEmpty())
			return;
		map.TryGetValue(key, out float cur);
		map[key] = cur + amount;
	}

	private static List<string> TopLabels(Dictionary<string, float> weights, int take)
	{
		return weights
			.OrderByDescending(kv => kv.Value)
			.ThenBy(kv => kv.Key)
			.Take(take)
			.Select(kv => kv.Key)
			.ToList();
	}

	private static string PrettyTag(string tag)
	{
		if (tag.NullOrEmpty())
			return tag;
		return tag.Replace('_', ' ');
	}
}
