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

		if (this.economyManager != null)
			return this.RenderEconomyAnnotations(this.economyManager);

		return [];
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
}
