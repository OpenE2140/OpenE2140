using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.OpenE2140.Traits.BotModules;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.AI;

[TraitLocation(SystemActors.Player)]
[Desc("Renders custom AI Debug UI. Attach this to the player actor. Needs to be enabled in config or chat command.")]
public class AIDebugRenderInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new AIDebugRender(init.Self);
	}
}

public class AIDebugRender : IRenderAnnotations
{
	private readonly AIDebugMode aiDebugMode;
	private readonly SpriteFont font;

	private EconomyManagerBotModule? economyManager;
	private McuDeployManagerBotModule? mcuDeployManager;

	public AIDebugRender(Actor self)
	{
		this.aiDebugMode = self.World.WorldActor.TraitOrDefault<AIDebugMode>();
		this.font = Game.Renderer.Fonts["Tiny"];
	}

	bool IRenderAnnotations.SpatiallyPartitionable => false;

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		if (this.aiDebugMode == null || !this.aiDebugMode.Enable || !self.Owner.IsBot)
			return [];

		this.economyManager ??= self.Owner.PlayerActor.TraitsImplementing<EconomyManagerBotModule>().FirstEnabledTraitOrDefault();
		this.mcuDeployManager ??= self.Owner.PlayerActor.TraitsImplementing<McuDeployManagerBotModule>().FirstEnabledTraitOrDefault();

		return Iterate();

		IEnumerable<IRenderable> Iterate()
		{
			if (this.economyManager != null)
			{
				foreach (var r in this.RenderEconomyAnnotations(this.economyManager))
					yield return r;
			}
			if (this.mcuDeployManager != null)
			{
				foreach (var r in this.RenderMcuDeployAnnotations(wr, this.mcuDeployManager))
					yield return r;
			}
		}
	}

	private IEnumerable<IRenderable> RenderEconomyAnnotations(EconomyManagerBotModule economyManager)
	{
		foreach (var assignment in economyManager.MineRefineryAssignments)
		{
			var crateTransportersLabel = "Crate Transporters: " +
				$"{assignment.CrateTransporters.Count}/{economyManager.Info.CrateTransporterPerRefineryMinePair}";
			if (assignment.Mine != null)
				yield return new TextAnnotationRenderable(this.font, assignment.Mine.CenterPosition, 0, Color.White, crateTransportersLabel);
			if (assignment.Refinery != null)
				yield return new TextAnnotationRenderable(this.font, assignment.Refinery.CenterPosition, 0, Color.White, crateTransportersLabel);

			if (assignment.Mine != null && assignment.Refinery != null)
			{
				var start = assignment.Mine.CenterPosition;
				var end = assignment.Refinery.CenterPosition;

				yield return new LineAnnotationRenderable(start, end, 1, Color.AliceBlue);
			}
		}
	}

	private IEnumerable<IRenderable> RenderMcuDeployAnnotations(WorldRenderer wr, McuDeployManagerBotModule mcuDeployManager)
	{
		foreach (var cell in mcuDeployManager.WaterBodyClusters.SelectMany(cl => cl.Cells))
		{
			yield return new ClusterCellRenderable(wr.World.Map.CenterOfCell(cell), Color.FromArgb(64, Color.Orange));
		}
	}

	private class ClusterCellRenderable : IRenderable, IFinalizedRenderable
	{
		private readonly Color color;

		public ClusterCellRenderable(WPos pos, Color color)
		{
			this.Pos = pos;
			this.color = color;
		}

		public WPos Pos { get; }

		public int ZOffset => 0;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return this; }
		public IRenderable OffsetBy(in WVec vec) { return new ClusterCellRenderable(this.Pos + vec, this.color); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var halfCell = wr.World.Map.Grid.TileScale / 2;
			var ts = new WVec(halfCell, halfCell, 0);

			var tl = wr.Viewport.WorldToViewPx(wr.ScreenPxPosition(this.Pos - ts)).ToVector3(); 
			var br = wr.Viewport.WorldToViewPx(wr.ScreenPxPosition(this.Pos + ts)).ToVector3(); 

			var cr = Game.Renderer.RgbaColorRenderer;
			cr.FillRect(tl, br, this.color);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
