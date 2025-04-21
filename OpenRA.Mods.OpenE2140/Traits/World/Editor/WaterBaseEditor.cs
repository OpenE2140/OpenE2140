using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.WaterBase;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Traits.World.Editor;

[TraitLocation(SystemActors.EditorWorld)]
[Desc("Required for custom support of Water Base in the editor to work. Attach this to the world actor.")]
public class WaterBaseEditorInfo : TraitInfo, Requires<EditorActorLayerInfo>
{
	public override object Create(ActorInitializer init)
	{
		return new WaterBaseEditor(init.Self);
	}
}

public class WaterBaseEditor : ITickRender, IPostWorldLoaded
{
	private readonly List<EditorActorPreview> addedActors = new List<EditorActorPreview>();

	private readonly Lazy<EditorViewportControllerWidget> editor = Exts.Lazy(() => Ui.Root.Get<EditorViewportControllerWidget>("MAP_EDITOR"));

	private EditorViewportControllerWidget Editor => this.editor.Value;

	private readonly EditorActorLayer editorActorLayer;
	private readonly ActorInfo waterBaseDockActor;
	private readonly CachedTransform<int2, Rectangle> mapRectangle;
	private readonly WDist maximumDockDistance;

	private bool worldLoaded;

	public WaterBaseEditor(Actor self)
	{
		this.editorActorLayer = self.Trait<EditorActorLayer>();

		this.waterBaseDockActor = WaterBaseUtils.FindWaterBaseDockActor(self.World.Map.Rules);

		var tileSize = self.World.Map.Rules.TerrainInfo.TileSize;
		this.mapRectangle = new CachedTransform<int2, Rectangle>(size => Rectangle.FromLTRB(0, 0, size.X * tileSize.Width, size.Y * tileSize.Height));

		this.maximumDockDistance = self.World.Map.Rules.Actors.Values
			.Where(a => a.HasTraitInfo<Mcu.McuInfo>())
			.Select(a => a.TraitInfoOrDefault<WaterBaseTransformsInfo>())
			.OfType<WaterBaseTransformsInfo>()
			.Max(i => i.MaximumDockDistance);
	}

	void ITickRender.TickRender(WorldRenderer wr, Actor self)
	{
		if (!this.worldLoaded)
			return;

		if (this.addedActors.Count > 0)
		{
			this.ProcessNewActors(wr, self, this.addedActors);
			this.addedActors.Clear();
		}
	}

	void IPostWorldLoaded.PostWorldLoaded(OpenRA.World w, WorldRenderer wr)
	{
		this.worldLoaded = true;
	}

	private void ProcessNewActors(WorldRenderer wr, Actor self, IEnumerable<EditorActorPreview> addedActors)
	{
		var allActors = Exts.Lazy(() => this.GetAllActors(self.World).ToArray());

		foreach (var actor in addedActors)
		{
			if (actor.Info.HasTraitInfo<WaterBaseDockInfo>())
				this.TryLinkingDock(allActors.Value, actor);
			else if (actor.Info.HasTraitInfo<WaterBaseBuildingInfo>())
				this.TrySelectingDockActor(wr, allActors.Value, actor);
		}
	}

	private void TrySelectingDockActor(WorldRenderer wr, IEnumerable<EditorActorPreview> allActors, EditorActorPreview actor)
	{
		var pairedWaterBases = allActors.Where(a => a.Info.HasTraitInfo<WaterBaseDockInfo>())
			.Select(a => a.GetInitOrDefault<WaterBaseDockInit>()?.Value?.InternalName)
			.OfType<string>()
			.ToArray();

		if (!pairedWaterBases.Contains(actor.ID))
			this.Editor.SetBrush(new EditorActorBrush(this.Editor, this.waterBaseDockActor, actor.Owner, wr));
	}

	private void TryLinkingDock(IEnumerable<EditorActorPreview> allActors, EditorActorPreview actor)
	{
		// Check if this Dock is already linked to a Water Base.
		if (actor.GetInitOrDefault<WaterBaseDockInit>() == null)
		{
			var freeWaterBaseInRange = this.GetFreeWaterBasesInRange(allActors, actor);
			if (freeWaterBaseInRange.Length > 0)
			{
				// Automatically link first free Water Base to this Dock.
				actor.ReplaceInit(new WaterBaseDockInit(new ActorInitActorReference(freeWaterBaseInRange.First().ID)));
			}
		}
	}

	private EditorActorPreview[] GetFreeWaterBasesInRange(IEnumerable<EditorActorPreview> allActors, EditorActorPreview actor)
	{
		// Try to find Water Bases, which are not linked with any docks.
		var pairedWaterBases = allActors.Where(a => a.Info.HasTraitInfo<WaterBaseDockInfo>())
			.Select(a => a.GetInitOrDefault<WaterBaseDockInit>()?.Value?.InternalName)
			.OfType<string>()
			.ToArray();

		var freeWaterBases = allActors
			.Where(a => a.Info.HasTraitInfo<WaterBaseBuildingInfo>())
			.Except(allActors.Where(p => pairedWaterBases.Contains(p.ID)));

		// Only try linking Water Base, which is in acceptable range of the Dock
		var freeWaterBaseInRange = freeWaterBases
			.Select(a => new { FootprintCenter = a.Info.TraitInfo<WaterBaseBuildingInfo>().GetCenterOfFootprint(a.GetInitOrDefault<LocationInit>().Value), Actor = a })
			.Where(x => (x.FootprintCenter - actor.CenterPosition).ToWDist() <= this.maximumDockDistance)
			.Select(x => x.Actor)
			.ToArray();
		return freeWaterBaseInRange;
	}

	public void OnActorAdded(EditorActorPreview editorActor)
	{
		// ignore newly added actors, while the world is loading
		if (!this.worldLoaded)
			return;

		this.addedActors.Add(editorActor);
	}

	public void OnActorRemoved(EditorActorPreview _) { }

	private IEnumerable<EditorActorPreview> GetAllActors(OpenRA.World world)
	{
		return this.editorActorLayer.PreviewsInScreenBox(this.mapRectangle.Update(world.Map.MapSize));
	}

	public IEnumerable<EditorActorOption> GetWaterDockActorOptions(ActorInfo _, OpenRA.World world, WaterBaseDockInfo info)
	{
		var allActors = this.GetAllActors(world);

		yield return new EditorActorDropdown("Water Base", 11,
			actor =>
			{
				// Allow linking only Water Bases, which are in acceptable range of the Dock.
				var labels = this.GetFreeWaterBasesInRange(allActors, actor).ToDictionary(p => p.ID, p => p.ID);

				// Include currently linked Water Base.
				var init = actor.GetInitOrDefault<WaterBaseDockInit>();
				if (!string.IsNullOrEmpty(init?.Value?.InternalName))
					labels.Add(init.Value.InternalName, init.Value.InternalName);

				// Add empty value to allow unlinking the Water Base.
				labels.Add("", "");

				return labels;
			},
			(actor, labels) =>
			{
				var init = actor.GetInitOrDefault<WaterBaseDockInit>(info);
				var actorName = init?.Value?.InternalName ?? "";
				return labels.GetValueOrDefault(actorName, "");
			},
			(actor, value) =>
			{
				if (!string.IsNullOrEmpty(value))
					actor.ReplaceInit(new WaterBaseDockInit(new ActorInitActorReference(value)));
				else
					actor.RemoveInit<WaterBaseDockInit>();
			});
	}
}
