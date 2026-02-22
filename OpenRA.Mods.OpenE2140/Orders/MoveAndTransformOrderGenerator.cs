using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using Transforms = OpenRA.Mods.OpenE2140.Traits.Mcu.Transforms;

namespace OpenRA.Mods.OpenE2140.Orders;

public class MoveAndTransformOrderGenerator : ExtendedUnitOrderGenerator
{
	public readonly Actor Self;

	private readonly bool queued;
	private readonly Transforms transforms;
	private readonly ActorInfo targetActorInfo;
	private readonly BuildingInfo? targetBuildingInfo;

	protected override MouseActionType ActionType => MouseActionType.PlaceBuilding;

	public MoveAndTransformOrderGenerator(Actor self, bool queued)
		: base(self.World)
	{
		this.Self = self;
		this.queued = queued;
		this.transforms = self.Trait<Transforms>();
		this.targetActorInfo = self.World.Map.Rules.Actors[this.transforms.Info.IntoActor];
		this.targetBuildingInfo = this.targetActorInfo.TraitInfoOrDefault<BuildingInfo>();
	}

	protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
	{
		if (mi.Modifiers.HasModifier(Modifiers.Ctrl) || this.transforms.CanDeploy(this.Self, cell))
		{
			yield return new Order(OrderConstants.MoveAndDeployTransformOrderID, this.Self, Target.FromCell(world, cell), this.queued);

			world.CancelInputMode();
		}
	}

	public override void SelectionChanged(World world, IEnumerable<Actor> selected)
	{
		world.CancelInputMode();
	}

	public override IEnumerable<IRenderable> Render(WorldRenderer wr, World world)
	{
		if (this.targetBuildingInfo == null)
			return [];

		return this.RenderFromTrait(wr, world, (r, t) => r.Render(this.Self, wr, t));
	}

	public override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world)
	{
		if (this.targetBuildingInfo == null)
			return [];

		return this.RenderFromTrait(wr, world, (r, t) => r.RenderAboveShroud(this.Self, wr, t));
	}

	public override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
	{
		if (this.targetBuildingInfo == null)
			return [];

		return this.RenderFromTrait(wr, world, (r, t) => r.RenderAnnotations(this.Self, wr, t));
	}

	private IEnumerable<IRenderable> RenderFromTrait(WorldRenderer wr, World world, Func<IOrderPreviewRender, Target, IEnumerable<IRenderable>> selector)
	{
		var hoveredCell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
		var target = Target.FromCell(world, hoveredCell);
		if (!world.Map.Contains(hoveredCell))
			return [];

		return this.Self.TraitsImplementing<ITransforms>().OfType<IOrderPreviewRender>().SelectMany(r => selector(r, target));
	}

	public override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
	{
		if (!world.Map.Contains(cell))
			return this.transforms.Info.DeployBlockedCursor;

		if (mi.Modifiers.HasModifier(Modifiers.Ctrl) || this.transforms.CanDeploy(this.Self, cell))
			return this.transforms.Info.DeployCursor;

		return this.transforms.Info.DeployBlockedCursor;
	}
}
