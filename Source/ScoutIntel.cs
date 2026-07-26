using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WorldTileScouting;

public class ScoutIntel : IExposable
{
	public int worldObjectId = -1;
	public string fingerprint = string.Empty;
	public string targetLabel = string.Empty;
	public string factionName = string.Empty;
	public int createdTick;
	public int expireTick;
	public int expectedCount;
	public float rangedShare;
	public float meleeShare;
	public bool hasArmor;
	public ScoutThreatMode threatMode = ScoutThreatMode.FactionRoster;
	public string modeBannerKey = string.Empty;
	public List<ThingDef> armorSamples = new();
	public List<ThingDef> weaponSamples = new();
	public List<ThingCategoryDef> armorCategories = new();
	public List<ThingCategoryDef> weaponCategories = new();
	public List<KindCount> kindBreakdown = new();

	public bool IsExpired => Find.TickManager != null && Find.TickManager.TicksGame >= expireTick;

	public bool ShowsGearPanel =>
		threatMode is ScoutThreatMode.FactionRoster or ScoutThreatMode.Mechanoids or ScoutThreatMode.Turrets
		|| (threatMode == ScoutThreatMode.Complex && (weaponSamples.Count > 0 || armorSamples.Count > 0));

	public void ExposeData()
	{
		Scribe_Values.Look(ref worldObjectId, "worldObjectId", -1);
		Scribe_Values.Look(ref fingerprint, "fingerprint", string.Empty);
		Scribe_Values.Look(ref targetLabel, "targetLabel", string.Empty);
		Scribe_Values.Look(ref factionName, "factionName", string.Empty);
		Scribe_Values.Look(ref createdTick, "createdTick", 0);
		Scribe_Values.Look(ref expireTick, "expireTick", 0);
		Scribe_Values.Look(ref expectedCount, "expectedCount", 0);
		Scribe_Values.Look(ref rangedShare, "rangedShare", 0f);
		Scribe_Values.Look(ref meleeShare, "meleeShare", 0f);
		Scribe_Values.Look(ref hasArmor, "hasArmor", false);
		Scribe_Values.Look(ref threatMode, "threatMode", ScoutThreatMode.FactionRoster);
		Scribe_Values.Look(ref modeBannerKey, "modeBannerKey", string.Empty);
		Scribe_Collections.Look(ref armorSamples, "armorSamples", LookMode.Def);
		Scribe_Collections.Look(ref weaponSamples, "weaponSamples", LookMode.Def);
		Scribe_Collections.Look(ref armorCategories, "armorCategories", LookMode.Def);
		Scribe_Collections.Look(ref weaponCategories, "weaponCategories", LookMode.Def);
		Scribe_Collections.Look(ref kindBreakdown, "kindBreakdown", LookMode.Deep);
		armorSamples ??= new List<ThingDef>();
		weaponSamples ??= new List<ThingDef>();
		armorCategories ??= new List<ThingCategoryDef>();
		weaponCategories ??= new List<ThingCategoryDef>();
		kindBreakdown ??= new List<KindCount>();
		armorSamples.RemoveAll(d => d == null);
		weaponSamples.RemoveAll(d => d == null);
		armorCategories.RemoveAll(d => d == null);
		weaponCategories.RemoveAll(d => d == null);
	}

	public class KindCount : IExposable
	{
		public string label = string.Empty;
		public string defName = string.Empty;
		public int count;

		public void ExposeData()
		{
			Scribe_Values.Look(ref label, "label", string.Empty);
			Scribe_Values.Look(ref defName, "defName", string.Empty);
			Scribe_Values.Look(ref count, "count", 0);
		}

		public PawnKindDef? KindDef =>
			defName.NullOrEmpty() ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
	}
}

public enum ScoutThreatMode : byte
{
	FactionRoster = 0,
	Fleshbeasts = 1,
	Mechanoids = 2,
	Manhunters = 3,
	AmbushDeferred = 4,
	ConditionCauser = 5,
	Complex = 6,
	Abandoned = 7,
	Unknown = 8,
	Turrets = 9,
	Insects = 10,
}

public class PendingScout : IExposable
{
	public int worldObjectId = -1;
	public string fingerprint = string.Empty;
	public string targetLabel = string.Empty;
	public int finishTick;

	public void ExposeData()
	{
		Scribe_Values.Look(ref worldObjectId, "worldObjectId", -1);
		Scribe_Values.Look(ref fingerprint, "fingerprint", string.Empty);
		Scribe_Values.Look(ref targetLabel, "targetLabel", string.Empty);
		Scribe_Values.Look(ref finishTick, "finishTick", 0);
	}
}
