using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Miner;

public class WallBuilderInfo : ConditionalTraitInfo, Requires<MobileInfo>
{
	[ActorReference]
	[Desc("Wall actor.")]
	[FieldLoader.Require]
	public readonly string Wall = "";

	[GrantedConditionReference]
	[Desc("Condition to grant, when the wall's construction should begin.")]
	public readonly string? WallConstructionCondition;

	[VoiceReference]
	[Desc("Voice to use when ordered build a wall.")]
	public readonly string Voice = "Action";

	[Desc("Number of ticks it takes to build a wall.")]
	public readonly int PreBuildDelay = 0;

	[Desc("Color to use for the target line when building walls.")]
	public readonly Color TargetLineColor = Color.Crimson;

	[Desc("Only allow building walls on listed terrain types. Leave empty to allow all terrain types.")]
	public readonly HashSet<string> TerrainTypes = new();

	[CursorReference]
	[Desc("Cursor to display when able to build a wall.")]
	public readonly string BuildCursor = "wallPlace";

	[CursorReference]
	[Desc("Cursor to display when unable to build a wall.")]
	public readonly string BuildBlockedCursor = "generic-blocked";

	[Desc("Name of the ammo pool the wall builder uses.")]
	public readonly string AmmoPoolName = "primary";

	[Desc("Ammo the wall builder consumes per wall node.")]
	public readonly int AmmoUsage = 1;

	public override object Create(ActorInitializer init)
	{
		return new WallBuilder(this);
	}
}

public class WallBuilder : ConditionalTrait<WallBuilderInfo>, IResolveOrder, ITick, IOrderVoice, INotifyWallBuilding
{
	public const string BuildWallOrderID = "BuildWall";

	private CPos? newWallLocation;
	private Actor? newWallActor;

	public WallBuilder(WallBuilderInfo info)
		: base(info)
	{
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString != BuildWallOrderID)
			return;

		var targetCell = self.World.Map.CellContaining(order.Target.CenterPosition);
		if (!this.IsCellAcceptable(self, targetCell))
			return;

		self.QueueActivity(order.Queued, new BuildWall(self, targetCell));
	}

	public bool IsCellAcceptable(Actor self, CPos cell)
	{
		if (!self.World.Map.Contains(cell) || this.newWallLocation == cell)
			return false;

		if (this.Info.TerrainTypes.Count == 0)
			return true;

		var terrainType = self.World.Map.GetTerrainInfo(cell).Type;
		return this.Info.TerrainTypes.Contains(terrainType);
	}

	void ITick.Tick(Actor self)
	{
		if (this.newWallLocation != null && this.newWallLocation.Value != self.Location)
		{
			self.World.AddFrameEndTask(w =>
			{
				if (!self.World.ActorMap.GetActorsAt(this.newWallLocation.Value).All(a => a == self))
				{
					this.newWallLocation = null;
					return;
				}

				this.newWallActor = w.CreateActor(this.Info.Wall, new TypeDictionary
				{
					new LocationInit(this.newWallLocation.Value),
					new OwnerInit(self.Owner)
				});

				foreach (var t in self.TraitsImplementing<INotifyWallBuilding>())
					t.WallCreated(self, this.newWallActor);

				this.newWallLocation = null;
			});
		}

		if (this.newWallActor != null && this.newWallActor.Location != self.World.Map.CellContaining(self.CenterPosition))
		{
			if (this.Info.WallConstructionCondition != null)
				this.newWallActor.GrantCondition(this.Info.WallConstructionCondition);

			this.newWallActor = null;
		}
	}

	string? IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
	{
		if (order.OrderString == BuildWallOrderID)
			return this.Info.Voice;

		return null;
	}

	void INotifyWallBuilding.WallBuilding(Actor self, CPos location) { }

	void INotifyWallBuilding.WallBuildingCompleted(Actor self, CPos location)
	{
		this.newWallLocation = location;
	}

	void INotifyWallBuilding.WallCreated(Actor self, Actor wall) { }

	void INotifyWallBuilding.WallBuildingCanceled(Actor self, CPos location) { }
}
