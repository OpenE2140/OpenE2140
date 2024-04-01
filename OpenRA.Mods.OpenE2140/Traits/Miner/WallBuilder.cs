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

	[Desc("Sprite overlay to use for valid wall cells.")]
	public readonly string TileValidName = "build-valid";

	[Desc("Sprite overlay to use for invalid wall cells.")]
	public readonly string TileInvalidName = "build-invalid";

	[Desc("Sprite overlay to use for wall cells hidden behind fog or shroud.")]
	public readonly string TileUnknownName = "build-unknown";

	public override object Create(ActorInitializer init)
	{
		return new WallBuilder(this);
	}
}

public class WallBuilder : ConditionalTrait<WallBuilderInfo>, IResolveOrder, ITick, IOrderVoice, INotifyWallBuilding
{
	public const string BuildWallOrderID = "BuildWall";
	public const string BuildWallLineOrderID = "BuildWallLine";

	private CPos? newWallLocation;
	private Actor? newWallActor;

	public WallBuilder(WallBuilderInfo info)
		: base(info)
	{
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString == BuildWallOrderID)
		{
			var targetCell = self.World.Map.CellContaining(order.Target.CenterPosition);
			if (!this.IsCellAcceptable(self, targetCell))
				return;

			self.QueueActivity(order.Queued, new BuildWall(self, targetCell));
		}
		else if (order.OrderString == BuildWallLineOrderID)
		{
			var startPosition = order.ExtraLocation;
			var endPosition = self.World.Map.CellContaining(order.Target.CenterPosition);

			var cells = this.GetLineBuildCells(self, startPosition, endPosition);
			var queued = order.Queued;
			foreach (var cell in cells)
			{
				if (!this.IsCellAcceptable(self, cell))
					return;

				self.QueueActivity(queued, new BuildWall(self, cell));

				queued = true;
			}
		}
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

	public CPos? GetNearestValidLineBuildPosition(Actor self, CPos startPosition, CPos endPosition)
	{
		if (!self.World.Map.Contains(startPosition) || !self.World.Map.Contains(endPosition))
			return null;

		var diff = startPosition - endPosition;
		if (diff.X == 0 || diff.Y == 0)
			return endPosition;

		var candidate = diff.X > diff.Y ? new CPos(startPosition.X, endPosition.Y) : new CPos(endPosition.X, startPosition.Y);

		return !self.World.Map.Contains(candidate) ? null : candidate;
	}

	public IEnumerable<CPos> GetLineBuildCells(Actor self, CPos startPosition, CPos endPosition)
	{
		if (startPosition == endPosition)
			yield break;

		var validEndPosition = this.GetNearestValidLineBuildPosition(self, startPosition, endPosition);
		if (validEndPosition == null)
			yield break;

		var diff = validEndPosition.Value - startPosition;
		var norm = diff / diff.Length;
		var x = startPosition.X;
		var y = startPosition.Y;
		for (var i = 0; i <= diff.Length; i++)
		{
			yield return new CPos(x + norm.X * i, y + norm.Y * i);
		}
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
		if (order.OrderString == BuildWallOrderID || order.OrderString == BuildWallLineOrderID)
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
