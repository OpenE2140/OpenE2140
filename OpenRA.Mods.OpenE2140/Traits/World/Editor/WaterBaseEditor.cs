using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.WaterBase;
using OpenRA.Primitives;
using OpenRA.Traits;

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

public class WaterBaseEditor : ITickRender
{
	private readonly List<EditorActorPreview> addedActors = new List<EditorActorPreview>();

	private readonly EditorActorLayer editorActorLayer;
	private readonly CachedTransform<int2, Rectangle> mapRectangle;
	private readonly WDist maximumDockDistance;

	public WaterBaseEditor(Actor self)
	{
		this.editorActorLayer = self.Trait<EditorActorLayer>();

		var tileSize = self.World.Map.Grid.TileSize;
		this.mapRectangle = new CachedTransform<int2, Rectangle>(size => Rectangle.FromLTRB(0, 0, size.X * tileSize.Width, size.Y * tileSize.Height));

		this.maximumDockDistance = self.World.Map.Rules.Actors.Values
			.Where(a => a.HasTraitInfo<Mcu.McuInfo>())
			.Select(a => a.TraitInfoOrDefault<WaterBaseTransformsInfo>())
			.OfType<WaterBaseTransformsInfo>()
			.Max(i => i.MaximumDockDistance);
	}

	void ITickRender.TickRender(WorldRenderer wr, Actor self)
	{
		if (this.addedActors.Count > 0)
		{
			this.ProcessNewActors(self, this.addedActors);
			this.addedActors.Clear();
		}
	}

	private void ProcessNewActors(Actor self, IEnumerable<EditorActorPreview> addedActors)
	{
		var allActors = Exts.Lazy(() => this.GetAllActors(self.World).ToArray());

		foreach (var actor in addedActors)
		{
			if (!actor.Info.HasTraitInfo<WaterBaseDockInfo>())
				continue;

			// Check if this Dock is already linked to a Water Base.
			if (actor.GetInitOrDefault<WaterBaseDockInit>() != null)
				continue;

			// Try to find Water Bases, which are not linked with any docks.
			var pairedWaterBases = allActors.Value.Where(a => a.Info.HasTraitInfo<WaterBaseDockInfo>())
				.Select(a => a.GetInitOrDefault<WaterBaseDockInit>()?.Value?.InternalName)
				.OfType<string>()
				.ToArray();

			var freeWaterBases = allActors.Value
				.Where(a => a.Info.HasTraitInfo<WaterBaseBuildingInfo>())
				.Except(allActors.Value.Where(p => pairedWaterBases.Contains(p.ID)));

			// Only try to link Water Base, which is in acceptable range of the Dock
			var freeWaterBaseInRange = freeWaterBases
				.Select(a => new { FootprintCenter = a.Info.TraitInfo<WaterBaseBuildingInfo>().GetCenterOfFootprint(a.GetInitOrDefault<LocationInit>().Value), Actor = a })
				.Where(x => (x.FootprintCenter - actor.CenterPosition).ToWDist() <= this.maximumDockDistance)
				.Select(x => x.Actor)
				.ToArray();
			if (freeWaterBaseInRange.Length > 0)
			{
				// Automatically link first free Water Base to this Dock.
				actor.ReplaceInit(new WaterBaseDockInit(new ActorInitActorReference(freeWaterBaseInRange.First().ID)));
			}
		}
	}

	public void OnActorAdded(EditorActorPreview editorActor)
	{
		this.addedActors.Add(editorActor);
	}

	public void OnActorRemoved(EditorActorPreview preview) { }

	private IEnumerable<EditorActorPreview> GetAllActors(OpenRA.World world)
	{
		return this.editorActorLayer.PreviewsInBox(this.mapRectangle.Update(world.Map.MapSize));
	}

	public IEnumerable<EditorActorOption> GetWaterDockActorOptions(ActorInfo ai, OpenRA.World world, WaterBaseDockInfo info)
	{
		var labels = this.GetAllActors(world)
			.Where(p => p.Info.HasTraitInfo<WaterBaseBuildingInfo>())
			.ToDictionary(p => p.ID, p => p.ID);
		labels.Add("", "");

		yield return new EditorActorDropdown("Water Base", 11,
			labels,
			(actor) =>
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
