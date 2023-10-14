#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

public class WithBuildingCrewPipsDecorationInfo : WithDecorationBaseInfo, Requires<BuildingCrewInfo>
{
	[Desc("If non-zero, override the spacing between adjacent pips.")]
	public readonly int2 PipStride = int2.Zero;

	[Desc("Image that defines the pip sequences.")]
	public readonly string Image = "pips";

	[SequenceReference(nameof(Image))]
	[Desc("Sequence used for empty pips.")]
	public readonly string EmptySequence = "pip-empty";

	[SequenceReference(nameof(Image))]
	[Desc("Sequence used for full pips that aren't defined in CustomPipSequences.")]
	public readonly string FullSequence = "pip-green";

	[PaletteReference]
	public readonly string Palette = "chrome";

	public override object Create(ActorInitializer init) { return new WithBuildingCrewPipsDecoration(init.Self, this); }
}

public class WithBuildingCrewPipsDecoration : WithDecorationBase<WithBuildingCrewPipsDecorationInfo>
{
	private readonly BuildingCrew buildingCrew;
	private readonly Animation pips;
	private readonly int pipCount;

	public WithBuildingCrewPipsDecoration(Actor self, WithBuildingCrewPipsDecorationInfo info)
		: base(self, info)
	{
		this.buildingCrew = self.Trait<BuildingCrew>();
		this.pipCount = this.buildingCrew.Info.MaxPopulation;
		this.pips = new Animation(self.World, info.Image);
	}

	protected override IEnumerable<IRenderable> RenderDecoration(Actor self, WorldRenderer wr, int2 screenPos)
	{
		this.pips.PlayRepeating(this.Info.EmptySequence);

		var palette = wr.Palette(this.Info.Palette);
		var pipSize = this.pips.Image.Size.XY.ToInt2();
		var pipStride = this.Info.PipStride != int2.Zero ? this.Info.PipStride : new int2(pipSize.X, 0);

		screenPos -= pipSize / 2;
		for (var i = 0; i < this.pipCount; i++)
		{
			var sequenceName = i < this.buildingCrew.CrewMembers.Count ? this.Info.FullSequence : this.Info.EmptySequence;
			this.pips.PlayRepeating(sequenceName);
			yield return new UISpriteRenderable(this.pips.Image, self.CenterPosition, screenPos, 0, palette);

			screenPos += pipStride;
		}
	}
}
