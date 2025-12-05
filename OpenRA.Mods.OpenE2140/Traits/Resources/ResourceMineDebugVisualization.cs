using System.Text;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class ResourceMineDebugVisualizationInfo : TraitInfo, Requires<ResourceMineInfo>
{
	public override object Create(ActorInitializer init)
	{
		return new ResourceMineDebugVisualization(init.Self);
	}
}

public class ResourceMineDebugVisualization : IRenderAnnotations
{
	private readonly ResourceMine resourceMine;
	private readonly IResourceLayer resourceLayer;
	private readonly SpriteFont font;
	private readonly int resourceValue;

	public ResourceMineDebugVisualization(Actor self)
	{
		this.resourceMine = self.Trait<ResourceMine>();
		this.resourceLayer = self.World.WorldActor.TraitOrDefault<ResourceLayer>();

		this.font = Game.Renderer.Fonts["Bold"];
		this.resourceValue = self.Owner.PlayerActor.Trait<PlayerResources>().Info.ResourceValues.First().Value;
	}

	bool IRenderAnnotations.SpatiallyPartitionable => true;

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		var debugInfo = this.resourceMine.GetDebugInfo();
		var crateBeingMined = debugInfo.CrateBeingMined;
		var availableCrate = debugInfo.AvailableCrate;
		var mineTick = debugInfo.MineTick;
		var initialDelay = this.resourceMine.IsDepleted ? this.resourceMine.Info.DelayWhenEmpty : this.resourceMine.Info.Delay;

		var resources = this.resourceMine.CellsInMiningArea
			.Sum(c => this.resourceLayer!.GetResource(c).Density);

		var totalCash = this.resourceValue * resources;

		var builder = new StringBuilder();
		if (crateBeingMined != null)
			builder.AppendLine($"Crate being mined: (#{crateBeingMined.Actor.ActorID}): {crateBeingMined.Resources.ToStringInvariant()}");
		else
			builder.AppendLine("Crate being mined: <null>");

		if (availableCrate != null)
			builder.AppendLine($"Crate available: (#{availableCrate.Actor.ActorID}): {availableCrate.Resources.ToStringInvariant()}");
		else
			builder.AppendLine("Crate available: <null>");

		builder.AppendLine($"Tick/InitialDelay: {mineTick}/{initialDelay}");
		builder.AppendLine($"Total resources: {resources}");
		builder.AppendLine($"Total cash: {totalCash}");

		var debugString = builder.ToString();
		yield return new TextAnnotationRenderable(this.font, self.CenterPosition, 0, Color.White, builder.ToString());
	}
}
