using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WorldTileScouting;

public class WorldComponent_TileScouting : WorldComponent
{
	private List<ScoutIntel> intelRecords = new();
	private List<PendingScout> pending = new();

	public WorldComponent_TileScouting(World world) : base(world)
	{
	}

	public static WorldComponent_TileScouting Get() =>
		Find.World.GetComponent<WorldComponent_TileScouting>();

	public override void WorldComponentTick()
	{
		if (pending.Count == 0 && intelRecords.Count == 0)
			return;

		int now = Find.TickManager.TicksGame;
		for (int i = pending.Count - 1; i >= 0; i--)
		{
			var job = pending[i];
			if (now < job.finishTick)
				continue;

			pending.RemoveAt(i);
			var obj = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o.ID == job.worldObjectId);
			if (obj == null || !ScoutTargetUtility.IsScoutable(obj, out _))
			{
				Messages.Message("WTS_ScoutFailedGone".Translate(job.targetLabel), MessageTypeDefOf.RejectInput, historical: false);
				continue;
			}

			if (ScoutTargetUtility.Fingerprint(obj) != job.fingerprint)
			{
				Messages.Message("WTS_ScoutFailedChanged".Translate(job.targetLabel), MessageTypeDefOf.RejectInput, historical: false);
				continue;
			}

			var intel = ScoutIntelAnalyzer.Analyze(obj);
			UpsertIntel(intel);

			// Письмо + сообщение со звуком (PositiveEvent), чтобы не пропустить конец разведки.
			Messages.Message(
				"WTS_ScoutCompleteMsg".Translate(obj.LabelCap),
				obj,
				MessageTypeDefOf.PositiveEvent);
			Find.LetterStack.ReceiveLetter(
				"WTS_LetterLabel".Translate(),
				"WTS_LetterText".Translate(obj.LabelCap),
				LetterDefOf.PositiveEvent,
				obj);
			SoundDefOf.Quest_Succeded.PlayOneShotOnCamera();
			Find.WindowStack.Add(new Dialog_ScoutIntel(intel, obj));
		}

		PruneExpired();
	}

	public bool TryGetIntel(WorldObject obj, out ScoutIntel? intel)
	{
		intel = intelRecords.FirstOrDefault(r => r.worldObjectId == obj.ID);
		if (intel == null)
			return false;

		if (intel.IsExpired || intel.fingerprint != ScoutTargetUtility.Fingerprint(obj))
		{
			ClearIntel(obj.ID);
			intel = null;
			return false;
		}

		return true;
	}

	public bool IsPending(WorldObject obj) =>
		pending.Any(p => p.worldObjectId == obj.ID);

	public int PendingTicksLeft(WorldObject obj)
	{
		var job = pending.FirstOrDefault(p => p.worldObjectId == obj.ID);
		if (job == null)
			return 0;
		return System.Math.Max(0, job.finishTick - Find.TickManager.TicksGame);
	}

	public void StartScout(WorldObject obj)
	{
		if (!ScoutTargetUtility.IsScoutable(obj, out var failReason))
		{
			Messages.Message(failReason, MessageTypeDefOf.RejectInput, historical: false);
			return;
		}

		if (IsPending(obj))
		{
			Messages.Message("WTS_AlreadyScouting".Translate(obj.LabelCap), MessageTypeDefOf.RejectInput, historical: false);
			return;
		}

		if (TryGetIntel(obj, out _))
		{
			Messages.Message("WTS_IntelAlreadyFresh".Translate(obj.LabelCap), MessageTypeDefOf.NeutralEvent, historical: false);
			return;
		}

		pending.RemoveAll(p => p.worldObjectId == obj.ID);
		pending.Add(new PendingScout
		{
			worldObjectId = obj.ID,
			fingerprint = ScoutTargetUtility.Fingerprint(obj),
			targetLabel = obj.LabelCap,
			finishTick = Find.TickManager.TicksGame + GenDate.TicksPerDay
		});

		Messages.Message("WTS_ScoutStarted".Translate(obj.LabelCap), MessageTypeDefOf.TaskCompletion, historical: false);
	}

	public void ClearIntel(int worldObjectId)
	{
		intelRecords.RemoveAll(r => r.worldObjectId == worldObjectId);
		pending.RemoveAll(p => p.worldObjectId == worldObjectId);
	}

	public void NotifyAttackOrMapGenerated(WorldObject obj)
	{
		if (obj == null)
			return;
		ClearIntel(obj.ID);
	}

	private void UpsertIntel(ScoutIntel intel)
	{
		intelRecords.RemoveAll(r => r.worldObjectId == intel.worldObjectId);
		intelRecords.Add(intel);
	}

	private void PruneExpired()
	{
		if (Find.TickManager.TicksGame % 2000 != 0)
			return;
		intelRecords.RemoveAll(r => r.IsExpired);
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Collections.Look(ref intelRecords, "intelRecords", LookMode.Deep);
		Scribe_Collections.Look(ref pending, "pending", LookMode.Deep);
		intelRecords ??= new List<ScoutIntel>();
		pending ??= new List<PendingScout>();
	}
}
