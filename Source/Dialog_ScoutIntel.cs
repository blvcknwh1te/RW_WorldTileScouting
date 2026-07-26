using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WorldTileScouting;

public class Dialog_ScoutIntel : Window
{
	private readonly ScoutIntel intel;
	private readonly WorldObject? target;
	private Vector2 scrollPos;

	public override Vector2 InitialSize => new(520f, 560f);

	public Dialog_ScoutIntel(ScoutIntel intel, WorldObject? target = null)
	{
		this.intel = intel;
		this.target = target;
		forcePause = false;
		absorbInputAroundWindow = false;
		closeOnClickedOutside = true;
		doCloseX = true;
		draggable = true;
	}

	public override void DoWindowContents(Rect inRect)
	{
		float y = 0f;
		Text.Font = GameFont.Medium;
		Widgets.Label(new Rect(0f, y, inRect.width, 32f), "WTS_ReportTitle".Translate(intel.targetLabel));
		y += 36f;

		Text.Font = GameFont.Small;
		Widgets.Label(new Rect(0f, y, inRect.width, 22f), "WTS_ReportFaction".Translate(intel.factionName));
		y += 22f;
		Widgets.Label(new Rect(0f, y, inRect.width, 22f), "WTS_ReportExpected".Translate(intel.expectedCount));
		y += 22f;

		int daysLeft = Mathf.Max(0, (intel.expireTick - Find.TickManager.TicksGame + GenDate.TicksPerDay - 1) / GenDate.TicksPerDay);
		Widgets.Label(new Rect(0f, y, inRect.width, 22f), "WTS_ReportFreshness".Translate(daysLeft));
		y += 28f;

		DrawSectionHeader(ref y, inRect.width, "WTS_SectionCombat".Translate());
		DrawBar(ref y, inRect.width, "WTS_Ranged".Translate(), intel.rangedShare, new Color(0.45f, 0.7f, 0.95f));
		DrawBar(ref y, inRect.width, "WTS_Melee".Translate(), intel.meleeShare, new Color(0.9f, 0.55f, 0.4f));
		y += 8f;

		DrawSectionHeader(ref y, inRect.width, "WTS_SectionArmor".Translate());
		Widgets.Label(new Rect(0f, y, inRect.width, 22f),
			intel.hasArmor ? "WTS_ArmorYes".Translate() : "WTS_ArmorNo".Translate());
		y += 24f;
		y = DrawChipBlock(y, inRect.width, "WTS_ArmorTags".Translate(), intel.armorTags);
		y = DrawChipBlock(y, inRect.width, "WTS_ArmorCats".Translate(), intel.armorCategories);

		DrawSectionHeader(ref y, inRect.width, "WTS_SectionWeapons".Translate());
		y = DrawChipBlock(y, inRect.width, "WTS_WeaponTags".Translate(), intel.weaponTags);
		y = DrawChipBlock(y, inRect.width, "WTS_WeaponCats".Translate(), intel.weaponCategories);

		DrawSectionHeader(ref y, inRect.width, "WTS_SectionKinds".Translate());
		float listHeight = Mathf.Max(80f, inRect.height - y - 56f);
		Rect outRect = new(0f, y, inRect.width, listHeight);
		Rect viewRect = new(0f, 0f, inRect.width - 16f, intel.kindBreakdown.Count * 22f + 4f);
		Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
		float ly = 0f;
		foreach (var row in intel.kindBreakdown)
		{
			Widgets.Label(new Rect(0f, ly, viewRect.width, 22f), $"{row.count}× {row.label}");
			ly += 22f;
		}
		Widgets.EndScrollView();
		y += listHeight + 8f;

		Text.Font = GameFont.Tiny;
		GUI.color = new Color(0.7f, 0.7f, 0.7f);
		Widgets.Label(new Rect(0f, inRect.height - 42f, inRect.width, 36f), "WTS_ReportDisclaimer".Translate());
		GUI.color = Color.white;
		Text.Font = GameFont.Small;

		if (target != null && Widgets.ButtonText(new Rect(inRect.width - 120f, inRect.height - 32f, 120f, 28f), "JumpToLocation".Translate()))
			CameraJumper.TryJumpAndSelect(target);
	}

	private static void DrawSectionHeader(ref float y, float width, string label)
	{
		Widgets.DrawLineHorizontal(0f, y, width);
		y += 6f;
		Text.Font = GameFont.Small;
		Widgets.Label(new Rect(0f, y, width, 22f), label);
		y += 24f;
	}

	private static void DrawBar(ref float y, float width, string label, float share, Color color)
	{
		Widgets.Label(new Rect(0f, y, 90f, 22f), label);
		Rect bar = new(96f, y + 4f, width - 160f, 14f);
		Widgets.DrawBoxSolid(bar, new Color(0.15f, 0.15f, 0.15f));
		Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(share), bar.height), color);
		Widgets.Label(new Rect(width - 58f, y, 58f, 22f), share.ToStringPercent());
		y += 24f;
	}

	private static float DrawChipBlock(float y, float width, string title, List<string> items)
	{
		Widgets.Label(new Rect(0f, y, width, 20f), title);
		y += 20f;
		if (items.NullOrEmpty())
		{
			GUI.color = new Color(0.6f, 0.6f, 0.6f);
			Widgets.Label(new Rect(8f, y, width - 8f, 20f), "WTS_None".Translate());
			GUI.color = Color.white;
			return y + 24f;
		}

		var sb = new StringBuilder();
		for (int i = 0; i < items.Count; i++)
		{
			if (i > 0)
				sb.Append(" · ");
			sb.Append(items[i]);
		}

		float h = Text.CalcHeight(sb.ToString(), width - 8f);
		Widgets.Label(new Rect(8f, y, width - 8f, h), sb.ToString());
		return y + h + 8f;
	}
}
