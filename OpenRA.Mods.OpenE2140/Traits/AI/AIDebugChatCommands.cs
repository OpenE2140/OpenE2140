using OpenRA.Graphics;
using OpenRA.Mods.Common.Commands;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.AI;

[TraitLocation(SystemActors.World)]
[Desc("Enables AI Debug commands via the chatbox. Attach this to the world actor.")]
public class AIDebugChatCommandsInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new AIDebugChatCommands(init.Self);
	}
}

public class AIDebugChatCommands : IChatCommand, IWorldLoaded
{
	private readonly Dictionary<string, (string Description, Action<AIDebugChatCommands, string> Handler)> commandHandlers =
		new Dictionary<string, (string Description, Action<AIDebugChatCommands, string> Handler)>
		{
			{ "ai-debug", ("Enables AI debug UI", AIDebug) },
		};

	private readonly OpenRA.World world;
	private DeveloperMode? devMode;
	private AIDebugMode? aiDebugMode;

	public AIDebugChatCommands(Actor self)
	{
		this.world = self.World;
	}

	void IWorldLoaded.WorldLoaded(OpenRA.World world, WorldRenderer wr)
	{
		this.aiDebugMode = world.WorldActor.TraitOrDefault<AIDebugMode>();
		if (this.aiDebugMode == null)
			return;

		if (this.world.LocalPlayer != null)
			this.devMode = this.world.LocalPlayer.PlayerActor.Trait<DeveloperMode>();

		var console = this.world.WorldActor.Trait<ChatCommands>();
		var help = this.world.WorldActor.Trait<HelpCommand>();

		foreach (var command in this.commandHandlers)
		{
			console.RegisterCommand(command.Key, this);
			help.RegisterHelp(command.Key, command.Value.Description);
		}
	}

	private static void AIDebug(AIDebugChatCommands @this, string arg)
	{
		if (@this.aiDebugMode == null)
			return;

		if (@this.devMode?.Enabled == true || @this.world.LocalPlayer == null || @this.world.LocalPlayer.Spectating)
		{
			@this.aiDebugMode.Enable ^= true;

			TextNotificationsManager.Debug($"AI debug mode {(@this.aiDebugMode.Enable ? "enabled" : "disabled")}");
		}
		else
		{
			TextNotificationsManager.Debug("AI debug visualization only available in replays, spectator mode, or with developer mode enabled");
		}
	}

	void IChatCommand.InvokeCommand(string name, string arg)
	{
		if (this.commandHandlers.TryGetValue(name, out var command))
			command.Handler(this, arg);
	}
}
