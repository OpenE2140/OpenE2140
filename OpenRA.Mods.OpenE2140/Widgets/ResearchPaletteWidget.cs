#region Copyright & License Information

/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Traits.Research;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets;

public record ResearchIcon(
	Researchable Researchable,
	HotkeyReference? Hotkey,
	string Image,
	PaletteReference IconClockPalette,
	PaletteReference IconDarkenPalette,
	float2 Pos
);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ResearchPaletteWidget : Widget, IFactionSpecificWidget
{
	public readonly Color TextColor = Color.White;
	public readonly int Columns = 3;
	public readonly int2 IconSize = new int2(64, 48);
	public readonly int2 IconMargin;
	public readonly int2 IconSpriteOffset;

	public readonly string ClickSound = ChromeMetrics.Get<string>("ClickSound");
	public readonly string ClickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");
	public readonly string? TooltipContainer;
	public readonly string TooltipTemplate = "RESEARCH_TOOLTIP";

	public readonly string? HotkeyPrefix;
	public readonly int HotkeyCount;

	public string ClockAnimation = "clock";
	public readonly string ClockSequence = "idle";
	public readonly string ClockPalette = "chrome";

	public readonly string NotResearchableAnimation = "clock";
	public readonly string NotResearchableSequence = "idle";
	public readonly string NotResearchablePalette = "chrome";

	public readonly string OverlayFont = "TinyBold";

	public string Icons = "";

	public ResearchIcon? TooltipIcon { get; private set; }
	public Func<ResearchIcon?> GetTooltipIcon;
	public readonly World World;
	private readonly ModData modData;

	public int IconRowOffset;
	public int MaxIconRowOffset = int.MaxValue;

	private readonly Lazy<TooltipContainerWidget> tooltipContainer;
	private HotkeyReference[] hotkeys = Array.Empty<HotkeyReference>();

	public override Rectangle EventBounds => this.eventBounds;
	private readonly Dictionary<Rectangle, ResearchIcon> icons = new Dictionary<Rectangle, ResearchIcon>();
	public Animation? NotResearchable;
	public Animation? Clock;
	private Rectangle eventBounds;

	private readonly WorldRenderer worldRenderer;

	private SpriteFont? overlayFont;
	private float2 iconOffset;
	private float2 timeOffset;

	public Researchable[] Researchables = Array.Empty<Researchable>();
	private readonly Research research;
	private readonly DeveloperMode developerMode;

	private bool CanScrollDown => this.IconRowOffset < (this.Researchables.Length + this.Columns - 1) / this.Columns - this.MaxIconRowOffset;
	private bool CanScrollUp => this.IconRowOffset > 0;

	public readonly string Identifier = string.Empty;

	string[] IFactionSpecificWidget.FieldsToOverride => new[] { nameof(this.TextColor) };

	string IFactionSpecificWidget.Identifier => this.Identifier;

	[ObjectCreator.UseCtor]
	public ResearchPaletteWidget(ModData modData, World world, WorldRenderer worldRenderer)
	{
		this.modData = modData;
		this.World = world;
		this.worldRenderer = worldRenderer;
		this.GetTooltipIcon = () => this.TooltipIcon;
		this.tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(this.TooltipContainer));
		this.research = world.LocalPlayer.PlayerActor.TraitOrDefault<Research>();
		this.developerMode = world.LocalPlayer.PlayerActor.TraitOrDefault<DeveloperMode>();
	}

	public override void Initialize(WidgetArgs args)
	{
		base.Initialize(args);

		this.Clock = new Animation(this.World, this.ClockAnimation);
		this.NotResearchable = new Animation(this.World, this.NotResearchableAnimation);
		this.NotResearchable.PlayFetchIndex(this.NotResearchableSequence, () => 0);
		this.hotkeys = Exts.MakeArray(this.HotkeyCount, i => this.modData.Hotkeys[this.HotkeyPrefix + (i + 1).ToStringInvariant("D2")]);
		this.overlayFont = Game.Renderer.Fonts[this.OverlayFont];
		this.iconOffset = 0.5f * this.IconSize.ToFloat2() + this.IconSpriteOffset;
	}

	private void ScrollDown()
	{
		if (this.CanScrollDown)
			this.IconRowOffset++;
	}

	private void ScrollUp()
	{
		if (this.CanScrollUp)
			this.IconRowOffset--;
	}

	public override void Tick()
	{
		var newResearchables = this.World.LocalPlayer.PlayerActor.TraitsImplementing<Researchable>()
			.Where(
				e =>
				{
					if (e.RemainingDuration == 0)
						return true;

					if (e.Info.Factions.Length == 0)
						return true;

					if (e.Info.Factions.Contains(this.World.LocalPlayer.Faction.InternalName))
						return true;

					if (this.developerMode is { AllTech: true })
						return true;

					return false;
				}
			)
			.ToArray();

		if (this.Researchables.SequenceEqual(newResearchables))
			return;

		this.Researchables = newResearchables;
		this.RefreshIcons();
	}

	public override void MouseEntered()
	{
		if (this.TooltipContainer == null)
			return;

		this.tooltipContainer.Value.SetTooltip(
			this.TooltipTemplate,
			new WidgetArgs { { "player", this.World.LocalPlayer }, { "getTooltipIcon", this.GetTooltipIcon }, { "world", this.World } }
		);
	}

	public override void MouseExited()
	{
		if (this.TooltipContainer != null)
			this.tooltipContainer.Value.RemoveTooltip();
	}

	public override bool HandleMouseInput(MouseInput mouseInput)
	{
		var icon = this.icons.Where(i => i.Key.Contains(mouseInput.Location)).Select(i => i.Value).FirstOrDefault();

		if (mouseInput.Event == MouseInputEvent.Move)
			this.TooltipIcon = icon;

		if (mouseInput.Event == MouseInputEvent.Scroll)
		{
			if (mouseInput.Delta.Y < 0 && this.CanScrollDown)
			{
				this.ScrollDown();
				Ui.ResetTooltips();
				Game.Sound.PlayNotification(this.World.Map.Rules, this.World.LocalPlayer, "Sounds", this.ClickSound, null);
			}
			else if (mouseInput.Delta.Y > 0 && this.CanScrollUp)
			{
				this.ScrollUp();
				Ui.ResetTooltips();
				Game.Sound.PlayNotification(this.World.Map.Rules, this.World.LocalPlayer, "Sounds", this.ClickSound, null);
			}
		}

		if (icon == null)
			return false;

		if (mouseInput.Event != MouseInputEvent.Down)
			return true;

		return this.HandleEvent(icon.Researchable, mouseInput.Button);
	}

	private bool HandleLeftClick(Researchable researchable)
	{
		if (researchable.RemainingDuration == 0 || !this.research.HasRequirements(researchable) || this.research.Current == researchable)
			return false;

		this.World.IssueOrder(new Order(Research.StartResearchOrder, this.World.LocalPlayer.PlayerActor, false) { TargetString = researchable.Info.Id });
		Game.Sound.PlayNotification(this.World.Map.Rules, this.World.LocalPlayer, "Sounds", this.ClickSound, null);

		return true;
	}

	private bool HandleRightClick(Researchable researchable)
	{
		if (this.research.Current != researchable)
			return false;

		this.World.IssueOrder(new Order(Research.StopResearchOrder, this.World.LocalPlayer.PlayerActor, false) { TargetString = researchable.Info.Id });
		Game.Sound.PlayNotification(this.World.Map.Rules, this.World.LocalPlayer, "Sounds", this.ClickSound, null);

		return true;
	}

	private bool HandleEvent(Researchable researchable, MouseButton mouseButton)
	{
		var handled = mouseButton == MouseButton.Left
			? this.HandleLeftClick(researchable)
			: mouseButton == MouseButton.Right && this.HandleRightClick(researchable);

		if (!handled)
			Game.Sound.PlayNotification(this.World.Map.Rules, this.World.LocalPlayer, "Sounds", this.ClickDisabledSound, null);

		return true;
	}

	public override bool HandleKeyPress(KeyInput e)
	{
		if (e.Event == KeyInputEvent.Up)
			return false;

		var toResearch = this.icons.Values.FirstOrDefault(i => i.Hotkey != null && i.Hotkey.IsActivatedBy(e));

		return toResearch != null && this.HandleEvent(toResearch.Researchable, MouseButton.Left);
	}

	private void RefreshIcons()
	{
		this.icons.Clear();

		foreach (var item in this.Researchables.Skip(this.IconRowOffset * this.Columns).Take(this.MaxIconRowOffset * this.Columns))
		{
			var x = this.icons.Count % this.Columns;
			var y = this.icons.Count / this.Columns;

			var rectangle = new Rectangle(
				this.RenderBounds.X + x * (this.IconSize.X + this.IconMargin.X),
				this.RenderBounds.Y + y * (this.IconSize.Y + this.IconMargin.Y),
				this.IconSize.X,
				this.IconSize.Y
			);

			this.icons.Add(
				rectangle,
				new ResearchIcon(
					item,
					this.icons.Count < this.HotkeyCount ? this.hotkeys[this.icons.Count] : null,
					item.Info.Id,
					this.worldRenderer.Palette(this.ClockPalette),
					this.worldRenderer.Palette(this.NotResearchablePalette),
					new float2(rectangle.Location)
				)
			);
		}

		this.eventBounds = this.icons.Keys.Union();
	}

	public override void Draw()
	{
		this.timeOffset = this.iconOffset;

		if (this.overlayFont != null)
			this.timeOffset -= this.overlayFont.Measure(WidgetUtils.FormatTime(0, this.World.Timestep)) / 2;

		Game.Renderer.EnableAntialiasingFilter();

		foreach (var icon in this.icons.Values)
		{
			var wrongFaction = icon.Researchable.RemainingDuration > 0
				&& icon.Researchable.Info.Factions.Any()
				&& !icon.Researchable.Info.Factions.Contains(this.World.LocalPlayer.Faction.InternalName);

			WidgetUtils.DrawSpriteCentered(ChromeProvider.GetImage(this.Icons, icon.Image), null, icon.Pos + this.iconOffset);

			if (this.research.Current == icon.Researchable)
			{
				if (this.Clock == null)
					continue;

				this.Clock.PlayFetchIndex(
					this.ClockSequence,
					() => (icon.Researchable.Info.Duration - icon.Researchable.RemainingDuration)
						* (this.Clock.CurrentSequence.Length - 1)
						/ Math.Max(icon.Researchable.Info.Duration, 1)
				);

				this.Clock.Tick();

				WidgetUtils.DrawSpriteCentered(this.Clock.Image, icon.IconClockPalette, icon.Pos + this.iconOffset);
			}
			else if ((icon.Researchable.RemainingDuration == 0 || !this.research.HasRequirements(icon.Researchable) || wrongFaction)
				&& this.NotResearchable != null)
				WidgetUtils.DrawSpriteCentered(this.NotResearchable.Image, icon.IconDarkenPalette, icon.Pos + this.iconOffset);
		}

		Game.Renderer.DisableAntialiasingFilter();

		var speed = this.World.ActorsWithTrait<Researches>().Count(e => e.Actor.Owner == this.World.LocalPlayer && !e.Trait.IsTraitDisabled);

		foreach (var icon in this.icons.Values)
		{
			if (this.research.Current != icon.Researchable || this.overlayFont == null)
				continue;

			this.overlayFont.DrawTextWithContrast(
				WidgetUtils.FormatTime(icon.Researchable.RemainingDuration / Math.Max(speed, 1), this.World.Timestep),
				icon.Pos + this.timeOffset,
				this.TextColor,
				Color.Black,
				1
			);
		}
	}

	public override string? GetCursor(int2 pos)
	{
		var icon = this.icons.Where(i => i.Key.Contains(pos)).Select(i => i.Value).FirstOrDefault();

		return icon != null ? base.GetCursor(pos) : null;
	}
}
