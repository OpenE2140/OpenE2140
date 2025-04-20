using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.OpenE2140.Traits.World;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Overlay for Rush Protection feature visualization. Attach this to the mpspawn actor.")]
public class RushProtectionOverlayInfo : TraitInfo
{
	[Desc("Color of the circle around the protected area")]
	public readonly Color ProtectedAreaCircleColor = Color.Gray;

	public override object Create(ActorInitializer init)
	{
		return new RushProtectionOverlay(init.Self, this);
	}
}

public class RushProtectionOverlay : IRenderAnnotations
{
	private readonly RushProtectionOverlayInfo info;
	private readonly RushProtection? rushProtection;

	public RushProtectionOverlay(Actor self, RushProtectionOverlayInfo rushProtectionOverlayInfo)
	{
		this.info = rushProtectionOverlayInfo;
		this.rushProtection = self.World.WorldActor.TraitOrDefault<RushProtection>();
	}

	bool IRenderAnnotations.SpatiallyPartitionable => true;

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		if (this.rushProtection == null)
			yield break;

		yield return new CircleAnnotationRenderable(
			self.World.Map.CenterOfCell(self.Location), this.rushProtection.Info.RushProtectionRange, 1, this.info.ProtectedAreaCircleColor);
	}
}
