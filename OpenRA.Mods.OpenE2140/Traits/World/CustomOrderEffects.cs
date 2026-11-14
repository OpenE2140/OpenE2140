using OpenRA.Mods.Common.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public class CustomOrderEffectsInfo : TraitInfo
{
	[Desc("The image to use.")]
	[FieldLoader.Require]
	public readonly string? TerrainFlashImage = null;

	[Desc("The sequence to use.")]
	[FieldLoader.Require]
	public readonly string? TerrainFlashSequence = null;

	[Desc("The palette to use.")]
	public readonly string? TerrainFlashPalette = null;

	[Desc("The type of effect to apply to targeted (frozen) actors. Accepts values Overlay and Tint.")]
	public readonly ActorFlashType ActorFlashType = ActorFlashType.Overlay;

	[Desc("The overlay color to display when ActorFlashType is Overlay.")]
	public readonly Color ActorFlashOverlayColor = Color.White;

	[Desc("The overlay transparency to display when ActorFlashType is Overlay.")]
	public readonly float ActorFlashOverlayAlpha = 0.5f;

	[Desc("The tint to apply when ActorFlashType is Tint.")]
	public readonly float3 ActorFlashTint = new(1.4f, 1.4f, 1.4f);

	[Desc("Number of times to flash (frozen) actors.")]
	public readonly int ActorFlashCount = 2;

	[Desc("Number of ticks between (frozen) actor flashes.")]
	public readonly int ActorFlashInterval = 2;

	[Desc("Order name(s) this applies for (empty if applies to all).")]
	public readonly List<string> ValidOrders = new();

	public override object Create(ActorInitializer init)
	{
		return new CustomOrderEffects(this);
	}
}

public class CustomOrderEffects : INotifyOrderIssued
{
	protected readonly CustomOrderEffectsInfo Info;

	public CustomOrderEffects(CustomOrderEffectsInfo info)
	{
		this.Info = info;
	}

	bool INotifyOrderIssued.OrderIssued(OpenRA.World world, string orderString, Target target)
	{
		if (this.Info.ValidOrders.Count > 0 && !this.Info.ValidOrders.Contains(orderString, StringComparer.Ordinal))
			return false;

		switch (target.Type)
		{
			case TargetType.Actor:
			case TargetType.FrozenActor:
			case TargetType.Terrain:
			world.AddFrameEndTask(w => w.Add(new SpriteAnnotation(
					target.CenterPosition, world, this.Info.TerrainFlashImage, this.Info.TerrainFlashSequence, this.Info.TerrainFlashPalette)));
				return true;

			default:
				return false;
		}
	}
}
