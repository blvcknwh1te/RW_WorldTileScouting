using System.Collections.Generic;
using System.Linq;
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
	private const float IconSize = 32f;
	private const float IconPad = 4f;

	public override Vector2 InitialSize => new(540f, 560f);

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
		const float footerH = 36f;
		Rect scrollOut = new(0f, 0f, inRect.width, inRect.height - footerH - 4f);
		float viewWidth = scrollOut.width - 16f;
		float contentH = MeasureContentHeight(viewWidth);
		Rect viewRect = new(0f, 0f, viewWidth, Mathf.Max(contentH, scrollOut.height + 1f));

		Widgets.BeginScrollView(scrollOut, ref scrollPos, viewRect);
		float y = 0f;
		DrawContent(ref y, viewWidth);
		Widgets.EndScrollView();

		Text.Font = GameFont.Tiny;
		GUI.color = new Color(0.7f, 0.7f, 0.7f);
		string disclaimer = NeedsSpecialDisclaimer(intel.threatMode)
			? "WTS_ReportDisclaimerSpecial".Translate()
			: "WTS_ReportDisclaimer".Translate();
		Widgets.Label(new Rect(0f, inRect.height - footerH, inRect.width - 130f, footerH), disclaimer);
		GUI.color = Color.white;
		Text.Font = GameFont.Small;

		if (target != null && Widgets.ButtonText(new Rect(inRect.width - 120f, inRect.height - 32f, 120f, 28f), "JumpToLocation".Translate()))
			CameraJumper.TryJumpAndSelect(target);
	}

	private static bool NeedsSpecialDisclaimer(ScoutThreatMode mode) =>
		mode is ScoutThreatMode.Fleshbeasts or ScoutThreatMode.AmbushDeferred or ScoutThreatMode.Unknown
			or ScoutThreatMode.ConditionCauser or ScoutThreatMode.Abandoned;

	private float MeasureContentHeight(float width)
	{
		float y = 0f;
		y += 36f;
		y += 22f;
		if (!intel.modeBannerKey.NullOrEmpty())
			y += 44f;
		y += 22f;
		y += 28f;
		y += 30f;
		y += 24f * 2f + 8f;
		if (intel.ShowsGearPanel)
		{
			y += 30f;
			y += 24f;
			y += MeasureIconBlock(width, intel.armorSamples);
			y += MeasureIconBlock(width, intel.armorCategories);
			y += 30f;
			y += MeasureIconBlock(width, intel.weaponSamples);
			y += MeasureIconBlock(width, intel.weaponCategories);
		}
		y += 30f;
		y += Mathf.Max(22f, intel.kindBreakdown.Count * 36f);
		y += 12f;
		return y;
	}

	private static float MeasureIconBlock(float width, System.Collections.IList items)
	{
		float y = 20f;
		if (items == null || items.Count == 0)
			return y + 24f;
		int perRow = Mathf.Max(1, Mathf.FloorToInt((width - 8f) / (IconSize + IconPad)));
		int rows = Mathf.CeilToInt(items.Count / (float)perRow);
		return y + rows * (IconSize + IconPad) + 8f;
	}

	private void DrawContent(ref float y, float width)
	{
		Text.Font = GameFont.Medium;
		Widgets.Label(new Rect(0f, y, width, 32f), "WTS_ReportTitle".Translate(intel.targetLabel));
		y += 36f;

		Text.Font = GameFont.Small;
		if (!intel.factionName.NullOrEmpty())
		{
			string factionLine = intel.threatMode == ScoutThreatMode.Fleshbeasts
				? "WTS_ReportSignalFaction".Translate(intel.factionName)
				: "WTS_ReportFaction".Translate(intel.factionName);
			Widgets.Label(new Rect(0f, y, width, 22f), factionLine);
			y += 22f;
		}

		if (!intel.modeBannerKey.NullOrEmpty())
		{
			GUI.color = new Color(0.95f, 0.75f, 0.45f);
			float bh = Text.CalcHeight(intel.modeBannerKey.Translate(), width);
			Widgets.Label(new Rect(0f, y, width, bh), intel.modeBannerKey.Translate());
			GUI.color = Color.white;
			y += bh + 4f;
		}

		string countLabel = intel.threatMode is ScoutThreatMode.Fleshbeasts or ScoutThreatMode.Manhunters
			or ScoutThreatMode.Mechanoids or ScoutThreatMode.Insects
			? "WTS_ReportExpectedThreats".Translate(intel.expectedCount)
			: "WTS_ReportExpected".Translate(intel.expectedCount);
		Widgets.Label(new Rect(0f, y, width, 22f), countLabel);
		y += 22f;

		int daysLeft = Mathf.Max(0, (intel.expireTick - Find.TickManager.TicksGame + GenDate.TicksPerDay - 1) / GenDate.TicksPerDay);
		Widgets.Label(new Rect(0f, y, width, 22f), "WTS_ReportFreshness".Translate(daysLeft));
		y += 28f;

		DrawSectionHeader(ref y, width, "WTS_SectionCombat".Translate());
		DrawBar(ref y, width, "WTS_Ranged".Translate(), intel.rangedShare, new Color(0.45f, 0.7f, 0.95f));
		DrawBar(ref y, width, "WTS_Melee".Translate(), intel.meleeShare, new Color(0.9f, 0.55f, 0.4f));
		y += 8f;

		if (intel.ShowsGearPanel)
		{
			DrawSectionHeader(ref y, width, "WTS_SectionArmor".Translate());
			Widgets.Label(new Rect(0f, y, width, 22f),
				intel.hasArmor ? "WTS_ArmorYes".Translate() : "WTS_ArmorNo".Translate());
			y += 24f;
			y = DrawThingIconBlock(y, width, "WTS_ArmorSamples".Translate(), intel.armorSamples);
			y = DrawCategoryIconBlock(y, width, "WTS_ArmorCats".Translate(), intel.armorCategories);

			DrawSectionHeader(ref y, width, "WTS_SectionWeapons".Translate());
			y = DrawThingIconBlock(y, width, "WTS_WeaponSamples".Translate(), intel.weaponSamples);
			y = DrawCategoryIconBlock(y, width, "WTS_WeaponCats".Translate(), intel.weaponCategories);
		}

		DrawSectionHeader(ref y, width, "WTS_SectionKinds".Translate());
		foreach (var row in intel.kindBreakdown)
		{
			Rect rowRect = new(0f, y, width, 34f);
			var kind = row.KindDef;
			if (kind?.race != null)
			{
				Rect icon = new(rowRect.x, rowRect.y + 1f, 32f, 32f);
				Widgets.DefIcon(icon, kind.race);
				AttachDefTooltip(icon, kind.race);
			}
			Widgets.Label(new Rect(40f, y + 6f, width - 40f, 22f), $"{row.count}× {row.label}");
			y += 36f;
		}
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
		Text.Font = GameFont.Small;
		Widgets.Label(new Rect(0f, y, 90f, 22f), label);
		Rect bar = new(96f, y + 4f, width - 160f, 14f);
		Widgets.DrawBoxSolid(bar, new Color(0.15f, 0.15f, 0.15f));
		Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(share), bar.height), color);
		Widgets.Label(new Rect(width - 58f, y, 58f, 22f), share.ToStringPercent());
		y += 24f;
	}

	private static float DrawThingIconBlock(float y, float width, string title, List<ThingDef> items)
	{
		Text.Font = GameFont.Small;
		Widgets.Label(new Rect(0f, y, width, 20f), title);
		y += 20f;
		if (items.NullOrEmpty())
		{
			GUI.color = new Color(0.6f, 0.6f, 0.6f);
			Widgets.Label(new Rect(8f, y, width - 8f, 20f), "WTS_None".Translate());
			GUI.color = Color.white;
			return y + 24f;
		}

		float x = 8f;
		float rowY = y;
		foreach (var def in items)
		{
			if (def == null)
				continue;
			if (x + IconSize > width)
			{
				x = 8f;
				rowY += IconSize + IconPad;
			}

			Rect icon = new(x, rowY, IconSize, IconSize);
			Widgets.DefIcon(icon, def);
			AttachDefTooltip(icon, def);
			x += IconSize + IconPad;
		}

		return rowY + IconSize + IconPad + 8f;
	}

	private static float DrawCategoryIconBlock(float y, float width, string title, List<ThingCategoryDef> items)
	{
		Text.Font = GameFont.Small;
		Widgets.Label(new Rect(0f, y, width, 20f), title);
		y += 20f;
		if (items.NullOrEmpty())
		{
			GUI.color = new Color(0.6f, 0.6f, 0.6f);
			Widgets.Label(new Rect(8f, y, width - 8f, 20f), "WTS_None".Translate());
			GUI.color = Color.white;
			return y + 24f;
		}

		float x = 8f;
		float rowY = y;
		foreach (var cat in items)
		{
			if (cat == null)
				continue;
			if (x + IconSize > width)
			{
				x = 8f;
				rowY += IconSize + IconPad;
			}

			Rect icon = new(x, rowY, IconSize, IconSize);
			Widgets.DefIcon(icon, cat);
			AttachCategoryTooltip(icon, cat);
			x += IconSize + IconPad;
		}

		return rowY + IconSize + IconPad + 8f;
	}

	private static void AttachDefTooltip(Rect rect, Def def)
	{
		if (def == null)
			return;

		TooltipHandler.TipRegion(rect, new TipSignal(() =>
		{
			var sb = new StringBuilder();
			sb.AppendLine(def.LabelCap);
			if (!def.description.NullOrEmpty())
			{
				sb.AppendLine();
				sb.Append(def.description);
			}
			sb.AppendLine();
			sb.AppendLine();
			sb.Append("WTS_ClickForInfoCard".Translate());
			return sb.ToString();
		}, def.shortHash ^ 917340));

		if (Widgets.ButtonInvisible(rect))
			Find.WindowStack.Add(new Dialog_InfoCard(def));
	}

	private static void AttachCategoryTooltip(Rect rect, ThingCategoryDef cat)
	{
		TooltipHandler.TipRegion(rect, new TipSignal(() =>
		{
			var sb = new StringBuilder();
			sb.AppendLine(cat.LabelCap);
			if (!cat.description.NullOrEmpty())
			{
				sb.AppendLine();
				sb.Append(cat.description);
			}

			var examples = cat.DescendantThingDefs?
				.Where(d => d.PlayerAcquirable)
				.Take(8)
				.Select(d => d.LabelCap.Resolve())
				.ToList();
			if (examples != null && examples.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine();
				sb.Append("WTS_CategoryExamples".Translate());
				sb.AppendLine();
				sb.Append(string.Join(", ", examples));
			}

			sb.AppendLine();
			sb.AppendLine();
			sb.Append("WTS_ClickForInfoCard".Translate());
			return sb.ToString();
		}, cat.shortHash ^ 44102));

		if (Widgets.ButtonInvisible(rect))
			Find.WindowStack.Add(new Dialog_InfoCard(cat));
	}
}
