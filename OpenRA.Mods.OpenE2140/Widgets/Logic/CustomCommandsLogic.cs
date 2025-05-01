using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Traits.Miner;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

/// <summary>
/// Logic for custom commands used in OpenE2140.
/// </summary>
public class CustomCommandsLogic : ChromeLogic
{
	private int? selectionHash;
	private Actor[] selectedActors = [];
	private bool buildWallDisabled = true;

	[ObjectCreator.UseCtor]
	public CustomCommandsLogic(Widget widget, World world)
	{
		var buildWallButton = widget.GetOrNull<ButtonWidget>("BUILD_WALL");
		if (buildWallButton != null)
		{
			WidgetUtils.BindButtonIcon(buildWallButton);

			buildWallButton.IsDisabled = () => { UpdateStateIfNecessary(); return this.buildWallDisabled; };
			buildWallButton.IsHighlighted = () => world.OrderGenerator is BuildWallOrderGenerator;

			void Toggle(bool allowCancel)
			{
				if (buildWallButton.IsHighlighted())
				{
					if (allowCancel)
						world.CancelInputMode();
				}
				else
				{
					world.OrderGenerator = new BuildWallOrderGenerator(this.selectedActors);
				}
			}

			buildWallButton.OnClick = () => Toggle(true);
			buildWallButton.OnKeyPress = _ => Toggle(false);
		}

		void UpdateStateIfNecessary()
		{
			if (this.selectionHash == world.Selection.Hash)
				return;

			this.selectedActors = world.Selection.Actors
				.Where(a => a.Owner == world.LocalPlayer && a.IsInWorld && !a.IsDead)
				.ToArray();

			this.buildWallDisabled = !this.selectedActors.Any(a => a.Info.HasTraitInfo<WallBuilderInfo>() );

			this.selectionHash = world.Selection.Hash;
		}
	}
}
