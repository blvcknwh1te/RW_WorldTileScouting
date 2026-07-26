using System.Collections.Generic;
using System.Linq;
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
	public List<string> armorTags = new();
	public List<string> armorCategories = new();
	public List<string> weaponTags = new();
	public List<string> weaponCategories = new();
	public List<KindCount> kindBreakdown = new();

	public bool IsExpired => Find.TickManager != null && Find.TickManager.TicksGame >= expireTick;

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
		Scribe_Collections.Look(ref armorTags, "armorTags", LookMode.Value);
		Scribe_Collections.Look(ref armorCategories, "armorCategories", LookMode.Value);
		Scribe_Collections.Look(ref weaponTags, "weaponTags", LookMode.Value);
		Scribe_Collections.Look(ref weaponCategories, "weaponCategories", LookMode.Value);
		Scribe_Collections.Look(ref kindBreakdown, "kindBreakdown", LookMode.Deep);
		armorTags ??= new List<string>();
		armorCategories ??= new List<string>();
		weaponTags ??= new List<string>();
		weaponCategories ??= new List<string>();
		kindBreakdown ??= new List<KindCount>();
	}

	public class KindCount : IExposable
	{
		public string label = string.Empty;
		public int count;

		public void ExposeData()
		{
			Scribe_Values.Look(ref label, "label", string.Empty);
			Scribe_Values.Look(ref count, "count", 0);
		}
	}
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
