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

using System.Collections.Frozen;
using System.Collections.Immutable;
using OpenRA.Mods.Common.MapGenerator;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Support;
using OpenRA.Traits;
using static OpenRA.Mods.Common.Traits.ResourceLayerInfo;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.EditorWorld)]
	public sealed class OpenE2140MapGeneratorInfo : TraitInfo, IEditorMapGeneratorInfo
	{
		[FieldLoader.Require]
		public readonly string? Type = null;

		[FieldLoader.Require]
		[FluentReference]
		public readonly string? Name = null;

		[FieldLoader.Require]
		[Desc("Tilesets that are compatible with this map generator.")]
		public readonly ImmutableArray<string> Tilesets = default;

		[FluentReference]
		[Desc("The title to use for generated maps.")]
		public readonly string MapTitle = "label-random-map";

		[Desc("The widget tree to open when the tool is selected.")]
		public readonly string PanelWidget = "MAP_GENERATOR_TOOL_PANEL";

		// This is purely of interest to the linter.
		[FieldLoader.LoadUsing(nameof(FluentReferencesLoader))]
		[FluentReference]
		public readonly ImmutableArray<string> FluentReferences = default;

		[FieldLoader.LoadUsing(nameof(SettingsLoader))]
		public readonly MiniYaml? Settings;

		string IMapGeneratorInfo.Type => Type!;
		string IMapGeneratorInfo.Name => Name!;
		string IMapGeneratorInfo.MapTitle => MapTitle;
		ImmutableArray<string> IEditorMapGeneratorInfo.Tilesets => Tilesets;

		private static MiniYaml SettingsLoader(MiniYaml my)
		{
			return my.NodeWithKey("Settings").Value;
		}

		private static object FluentReferencesLoader(MiniYaml my)
		{
			return new MapGeneratorSettings(null, my.NodeWithKey("Settings").Value)
				.Options.SelectMany(o => o.GetFluentReferences()).ToImmutableArray();
		}

		private const int FractionMax = Terraformer.FractionMax;

		private sealed class Parameters
		{
			[FieldLoader.Require]
			public readonly int Seed = default;
			[FieldLoader.Require]
			public readonly int Rotations = default;
			[FieldLoader.LoadUsing(nameof(MirrorLoader))]
			public readonly Symmetry.Mirror Mirror = default;
			[FieldLoader.Require]
			public readonly int Players = default;
			[FieldLoader.Require]
			public readonly int TerrainFeatureSize = default;
			[FieldLoader.Require]
			public readonly int SandFeatureSize = default;
			[FieldLoader.Require]
			public readonly int ForestFeatureSize = default;
			[FieldLoader.Require]
			public readonly int ResourceFeatureSize = default;
			[FieldLoader.Require]
			public readonly int CivilianBuildingsFeatureSize = default;
			[FieldLoader.Require]
			public readonly int Water = default;
			[FieldLoader.Require]
			public readonly int Mountains = default;
			[FieldLoader.Require]
			public readonly int Sand = default;
			[FieldLoader.Require]
			public readonly int Forests = default;
			[FieldLoader.Require]
			public readonly int ForestCutout = default;
			[FieldLoader.Require]
			public readonly int MaximumCutoutSpacing = default;
			[FieldLoader.Require]
			public readonly int ExternalCircularBias = default;
			[FieldLoader.Require]
			public readonly int TerrainSmoothing = default;
			[FieldLoader.Require]
			public readonly int SandSmoothing = default;
			[FieldLoader.Require]
			public readonly int SmoothingThreshold = default;
			[FieldLoader.Require]
			public readonly int MinimumLandSeaThickness = default;
			[FieldLoader.Require]
			public readonly int MinimumMountainThickness = default;
			[FieldLoader.Require]
			public readonly int MinimumSandThickness = default;
			[FieldLoader.Require]
			public readonly int MaximumAltitude = default;
			[FieldLoader.Require]
			public readonly int RoughnessRadius = default;
			[FieldLoader.Require]
			public readonly int Roughness = default;
			[FieldLoader.Require]
			public readonly int MinimumTerrainContourSpacing = default;
			[FieldLoader.Require]
			public readonly int SandContourSpacing = default;
			[FieldLoader.Require]
			public readonly int MinimumCliffLength = default;
			[FieldLoader.Require]
			public readonly int ForestClumpiness = default;
			[FieldLoader.Require]
			public readonly bool DenyWalledAreas = default;
			[FieldLoader.Require]
			public readonly int EnforceSymmetry = default;
			[FieldLoader.Require]
			public readonly bool Roads = default;
			[FieldLoader.Require]
			public readonly int RoadSpacing = default;
			[FieldLoader.Require]
			public readonly int RoadShrink = default;
			[FieldLoader.Require]
			public readonly bool CreateEntities = default;
			[FieldLoader.Require]
			public readonly int AreaResourceMultiplier = default;
			[FieldLoader.Require]
			public readonly int PlayerCountResourceMultiplier = default;
			[FieldLoader.Require]
			public readonly int CentralSpawnReservationFraction = default;
			[FieldLoader.Require]
			public readonly int ResourceSpawnReservation = default;
			[FieldLoader.Require]
			public readonly int SpawnRegionSize = default;
			[FieldLoader.Require]
			public readonly int SpawnBuildSize = default;
			[FieldLoader.Require]
			public readonly int MinimumSpawnRadius = default;
			[FieldLoader.Require]
			public readonly int SpawnReservation = default;
			[FieldLoader.Require]
			public readonly int OreUniformity = default;
			[FieldLoader.Require]
			public readonly int OreClumpiness = default;
			[FieldLoader.Require]
			public readonly int ResourcesPerPatch = default;
			[FieldLoader.Require]
			public readonly int CivilianBuildings = default;
			[FieldLoader.Require]
			public readonly int CivilianBuildingDensity = default;
			[FieldLoader.Require]
			public readonly int MinimumCivilianBuildingDensity = default;
			[FieldLoader.Require]
			public readonly int CivilianBuildingDensityRadius = default;

			[FieldLoader.Require]
			public readonly ushort LandTile = default;
			[FieldLoader.Require]
			public readonly ushort WaterTile = default;
			[FieldLoader.Require]
			public readonly ushort SandTile = default;
			[FieldLoader.Ignore]
			public readonly IReadOnlyList<MultiBrush> SegmentedBrushes;
			[FieldLoader.Ignore]
			public readonly IReadOnlyList<MultiBrush> ForestObstacles;
			[FieldLoader.Ignore]
			public readonly IReadOnlyList<MultiBrush> UnplayableObstacles;
			[FieldLoader.Ignore]
			public readonly IReadOnlyList<MultiBrush> CivilianBuildingsObstacles;
			[FieldLoader.Ignore]
			public readonly IReadOnlyDictionary<ushort, IReadOnlyList<MultiBrush>> RepaintTiles;

			[FieldLoader.Ignore]
			public readonly ResourceTypeInfo DefaultResource;

			[FieldLoader.Ignore]
			public readonly IReadOnlySet<byte> ClearTerrain;
			[FieldLoader.Ignore]
			public readonly IReadOnlySet<byte> PlayableTerrain;
			[FieldLoader.Ignore]
			public readonly IReadOnlySet<byte> DominantTerrain;
			[FieldLoader.Ignore]
			public readonly IReadOnlySet<byte> ZoneableTerrain;
			[FieldLoader.Require]
			public readonly string? ClearSegmentType = default;
			[FieldLoader.Require]
			public readonly string? BeachSegmentType = default;
			[FieldLoader.Require]
			public readonly string? CliffSegmentType = default;
			[FieldLoader.Require]
			public readonly string? SandSegmentType = default;
			[FieldLoader.Require]
			public readonly string? RoadSegmentType = default;

			public Parameters(Map map, MiniYaml my)
			{
				FieldLoader.Load(this, my);

				var terrainInfo = (ITemplatedTerrainInfo)map.Rules.TerrainInfo;
				this.SegmentedBrushes = MultiBrush.LoadCollection(map, "Segmented");
				this.ForestObstacles = MultiBrush.LoadCollection(map, my.NodeWithKey("ForestObstacles").Value.Value);
				this.UnplayableObstacles = MultiBrush.LoadCollection(map, my.NodeWithKey("UnplayableObstacles").Value.Value);
				this.CivilianBuildingsObstacles = MultiBrush.LoadCollection(map, my.NodeWithKey("CivilianBuildingsObstacles").Value.Value);
				IReadOnlyDictionary<ushort, IReadOnlyList<MultiBrush>>? repaintTiles =
					my.NodeWithKeyOrDefault("RepaintTiles")?.Value.ToDictionary(
						k =>
						{
							if (Exts.TryParseUshortInvariant(k, out var tile))
								return tile;
							else
								throw new YamlException($"RepaintTile {k} is not a ushort");
						},
						v => MultiBrush.LoadCollection(map, v.Value) as IReadOnlyList<MultiBrush>);
				this.RepaintTiles = repaintTiles ?? ImmutableDictionary<ushort, IReadOnlyList<MultiBrush>>.Empty;

				var resourceTypes = map.Rules.Actors[SystemActors.World].TraitInfoOrDefault<ResourceLayerInfo>().ResourceTypes;
				if (!resourceTypes.TryGetValue(my.NodeWithKey("DefaultResource").Value.Value, out var defaultResource))
					throw new YamlException("DefaultResource is not valid");
				this.DefaultResource = defaultResource;
				var playerResourcesInfo = map.Rules.Actors[SystemActors.Player].TraitInfoOrDefault<PlayerResourcesInfo>();

				switch (this.Rotations)
				{
					case 1:
					case 2:
					case 4:
						break;
					default:
						this.EnforceSymmetry = 0;
						break;
				}

				IReadOnlySet<byte> ParseTerrainIndexes(string key)
				{
					return my.NodeWithKey(key).Value.Value
						.Split(',', StringSplitOptions.RemoveEmptyEntries)
						.Select(terrainInfo.GetTerrainIndex)
						.ToFrozenSet();
				}

				this.ClearTerrain = ParseTerrainIndexes("ClearTerrain");
				this.PlayableTerrain = ParseTerrainIndexes("PlayableTerrain");
				this.DominantTerrain = ParseTerrainIndexes("DominantTerrain");
				this.ZoneableTerrain = ParseTerrainIndexes("ZoneableTerrain");
			}

			private static object MirrorLoader(MiniYaml my)
			{
				if (Symmetry.TryParseMirror(my.NodeWithKey("Mirror").Value.Value, out var mirror))
					return mirror;
				else
					throw new YamlException($"Invalid Mirror value `{my.NodeWithKey("Mirror").Value.Value}`");
			}
		}

		public IMapGeneratorSettings GetSettings()
		{
			return new MapGeneratorSettings(this, this.Settings);
		}

		public Map Generate(ModData modData, MapGenerationArgs args)
		{
			var terrainInfo = modData.DefaultTerrainInfo[args.Tileset];
			var size = args.Size;

			var map = new Map(modData, terrainInfo, size);
			var actorPlans = new List<ActorPlan>();

			var param = new Parameters(map, args.Settings);

			var terraformer = new Terraformer(args, map, modData, actorPlans, param.Mirror, param.Rotations);

			var waterIsPlayable = param.PlayableTerrain.Contains(terrainInfo.GetTerrainIndex(new TerrainTile(param.WaterTile, 0)));

			var externalCircleRadius = CellLayerUtils.Radius(map) - new WDist((param.MinimumLandSeaThickness + param.MinimumMountainThickness) * 1024);
			if (param.ExternalCircularBias != 0 && externalCircleRadius.Length <= 0)
				throw new MapGenerationException("map is too small for circular shaping");

			CellLayer<MultiBrush.Replaceability> PlayableToReplaceable()
			{
				var playable = terraformer.CheckSpace(param.PlayableTerrain, true);
				var basicLand = terraformer.CheckSpace(param.LandTile);
				var replace = new CellLayer<MultiBrush.Replaceability>(map);
				foreach (var mpos in map.AllCells.MapCoords)
					if (playable[mpos])
					{
						if (basicLand[mpos])
							replace[mpos] = MultiBrush.Replaceability.Any;
						else
							replace[mpos] = MultiBrush.Replaceability.Actor;
					}
					else
					{
						replace[mpos] = MultiBrush.Replaceability.None;
					}

				return replace;
			}

			// Use `random` to derive separate independent random number generators.
			//
			// This prevents changes in one part of the algorithm from affecting randomness in
			// other parts and provides flexibility for future parallel processing.
			//
			// In order to maximize stability, additions should be appended only. Disused
			// derivatives may be deleted but should be replaced with their unused call to
			// random.Next(). All generators should be created unconditionally.
			var random = new MersenneTwister(param.Seed);

			var pickAnyRandom = new MersenneTwister(random.Next());
			var elevationRandom = new MersenneTwister(random.Next());
			var coastTilingRandom = new MersenneTwister(random.Next());
			var cliffTilingRandom = new MersenneTwister(random.Next());
			var forestRandom = new MersenneTwister(random.Next());
			var forestTilingRandom = new MersenneTwister(random.Next());
			var symmetryTilingRandom = new MersenneTwister(random.Next());
			var debrisTilingRandom = new MersenneTwister(random.Next());
			var resourceRandom = new MersenneTwister(random.Next());
			var roadTilingRandom = new MersenneTwister(random.Next());
			var playerRandom = new MersenneTwister(random.Next());
			var resourceSpawnRandom = new MersenneTwister(random.Next());
			var topologyRandom = new MersenneTwister(random.Next());
			var repaintRandom = new MersenneTwister(random.Next());
			var decorationRandom = new MersenneTwister(random.Next());
			var decorationTilingRandom = new MersenneTwister(random.Next());
			var sandRandom = new MersenneTwister(random.Next());
			var sandTilingRandom = new MersenneTwister(random.Next());

			terraformer.InitMap();

			foreach (var mpos in map.AllCells.MapCoords)
				map.Tiles[mpos] = terraformer.PickTile(pickAnyRandom, param.LandTile);

			var elevation = terraformer.ElevationNoiseMatrix(
				elevationRandom,
				param.TerrainFeatureSize,
				param.TerrainSmoothing);
			var roughnessMatrix = MatrixUtils.GridVariance(
				elevation,
				param.RoughnessRadius);

			Matrix<bool> mapShape;
			if (param.ExternalCircularBias == 0)
				mapShape = new Matrix<bool>(CellLayerUtils.CellBounds(map).Size.ToInt2()).Fill(true);
			else
				mapShape = CellLayerUtils.ToMatrix(terraformer.CenteredCircle(true, false, externalCircleRadius), false);

			var landPlan = terraformer.SliceElevation(elevation, mapShape, FractionMax - param.Water);

			if (param.ExternalCircularBias > 0)
			{
				for (var n = 0; n < landPlan.Data.Length; n++)
					landPlan[n] |= !mapShape[n];
				var ring = terraformer.CenteredCircle(false, true, externalCircleRadius + new WDist(param.MinimumMountainThickness * 1024));
				var path = TilingPath.QuickCreate(
					map,
					param.SegmentedBrushes,
					CellLayerUtils.BordersToPoints(ring)[0],
					(param.MinimumMountainThickness - 1) / 2,
					param.CliffSegmentType,
					param.CliffSegmentType);
				var brush = path.Tile(cliffTilingRandom)
					?? throw new MapGenerationException("Could not fit tiles for exterior circle cliffs");
				terraformer.PaintTiling(pickAnyRandom, brush);
			}

			landPlan = MatrixUtils.BooleanBlotch(
				landPlan,
				param.TerrainSmoothing,
				param.SmoothingThreshold, /*smoothingThresholdOutOf=*/FractionMax,
				param.MinimumLandSeaThickness,
				/*bias=*/param.Water <= FractionMax / 2);

			var coast = MatrixUtils.BordersToPoints(landPlan);
			var coastPaths = CellLayerUtils.FromMatrixPoints(coast, map.Tiles)
				.Select(beach =>
					TilingPath.QuickCreate(
							map,
							param.SegmentedBrushes,
							beach,
							param.MinimumLandSeaThickness - 1,
							param.BeachSegmentType,
							param.BeachSegmentType)
								.ExtendEdge(4))
				.ToList();

			var landCoastWater = terraformer.PaintLoopsAndFill(
				coastTilingRandom,
				coastPaths,
				landPlan[0] ? Terraformer.Side.In : Terraformer.Side.Out,
				[new MultiBrush().WithTemplate(map, param.WaterTile, CVec.Zero)],
				null)
					?? throw new MapGenerationException("Could not fit tiles for coast");

			if (param.Mountains > 0)
			{
				var cliffMask = MatrixUtils.CalibratedBooleanThreshold(
					roughnessMatrix,
					param.Roughness, FractionMax);
				var cliffPlan = Matrix<bool>.Zip(landPlan, mapShape, (a, b) => a && b);

				for (var altitude = 0; altitude < param.MaximumAltitude; altitude++)
				{
					cliffPlan = terraformer.SliceElevation(
						elevation,
						cliffPlan,
						param.Mountains,
						param.MinimumTerrainContourSpacing);
					cliffPlan = MatrixUtils.BooleanBlotch(
						cliffPlan,
						param.TerrainSmoothing,
						param.SmoothingThreshold, /*smoothingThresholdOutOf=*/FractionMax,
						param.MinimumMountainThickness,
						/*bias=*/false);
					var unmaskedCliffs = MatrixUtils.BordersToPoints(cliffPlan);
					var maskedCliffs = MatrixUtils.MaskPathPoints(unmaskedCliffs, cliffMask);
					var cliffs = CellLayerUtils.FromMatrixPoints(maskedCliffs, map.Tiles)
						.Where(cliff => cliff.Length >= param.MinimumCliffLength).ToArray();
					if (cliffs.Length == 0)
						break;
					foreach (var cliff in cliffs)
					{
						var cliffPath = TilingPath.QuickCreate(
							map,
							param.SegmentedBrushes,
							cliff,
							(param.MinimumMountainThickness - 1) / 2,
							param.CliffSegmentType,
							param.ClearSegmentType)
								.ExtendEdge(4);
						var brush = cliffPath.Tile(cliffTilingRandom)
							?? throw new MapGenerationException("Could not fit tiles for cliffs");
						terraformer.PaintTiling(pickAnyRandom, brush);
					}
				}
			}

			// Sand
			if (param.Sand > 0)
			{
				var sandNoise = terraformer.ElevationNoiseMatrix(
					sandRandom,
					param.SandFeatureSize,
					param.SandSmoothing);
				var sandable = terraformer.CheckSpace(param.LandTile, true);
				sandable = terraformer.ImproveSymmetry(sandable, true, (a, b) => a && b);
				var plan = terraformer.SliceElevation(
					sandNoise,
					CellLayerUtils.ToMatrix(sandable, true),
					param.Sand,
					param.SandContourSpacing);
				plan = MatrixUtils.BooleanBlotch(
					plan,
					param.SandSmoothing,
					param.SmoothingThreshold, /*smoothingThresholdOutOf=*/FractionMax,
					param.MinimumSandThickness,
					false);
				var contours = CellLayerUtils.FromMatrixPoints(
					MatrixUtils.BordersToPoints(plan),
					map.Tiles);
				var tilingPaths = contours
					.Select(contour =>
						TilingPath.QuickCreate(
								map,
								param.SegmentedBrushes,
								contour,
								(param.MinimumSandThickness - 1) / 2,
								param.SandSegmentType,
								param.SandSegmentType)
									.ExtendEdge(4))
					.ToArray();
				_ = terraformer.PaintLoopsAndFill(
					sandTilingRandom,
					tilingPaths,
					plan[0] ? Terraformer.Side.In : Terraformer.Side.Out,
					null,
					[new MultiBrush().WithTemplate(map, param.SandTile, CVec.Zero)])
						?? throw new MapGenerationException("Could not fit tiles for rock platforms");
			}

			if (param.Forests > 0)
			{
				var space = terraformer.CheckSpace(param.ClearTerrain);
				var passages = terraformer.PlanPassages(
					topologyRandom,
					terraformer.ImproveSymmetry(space, true, (a, b) => a && b),
					param.ForestCutout,
					param.MaximumCutoutSpacing);
				var forestNoise = terraformer.BooleanNoise(
					forestRandom,
					param.ForestFeatureSize,
					param.Forests,
					param.ForestClumpiness);
				var replace = PlayableToReplaceable();
				foreach (var mpos in map.AllCells.MapCoords)
					if (!forestNoise[mpos] || !space[mpos] || passages[mpos])
						replace[mpos] = MultiBrush.Replaceability.None;
				terraformer.PaintArea(forestTilingRandom, replace, param.ForestObstacles);
			}

			if (param.EnforceSymmetry != 0)
			{
				var asymmetries = terraformer.FindAsymmetries(param.DominantTerrain, true, param.EnforceSymmetry == 2);
				terraformer.PaintActors(symmetryTilingRandom, asymmetries, param.ForestObstacles);
			}

			CellLayer<bool> playable;
			{
				// For circle-in-mountains, the outside is unplayable and should never count as
				// the largest/preferred region.
				CellLayer<bool>? poison = null;
				if (param.ExternalCircularBias > 0)
					poison = terraformer.CenteredCircle(
						false, true, CellLayerUtils.Radius(map.Tiles) - new WDist(1024));

				playable = terraformer.ChoosePlayableRegion(
					terraformer.CheckSpace(param.PlayableTerrain, true, false, true),
					poison)
						?? throw new MapGenerationException("could not find a playable region");

				var minimumPlayableSpace = (int)(param.Players * Math.PI * param.SpawnBuildSize * param.SpawnBuildSize);
				if (playable.Count(p => p) < minimumPlayableSpace)
					throw new MapGenerationException("playable space is too small");

				if (param.DenyWalledAreas)
				{
					// Coast tiles are particularly problematic. If they're for unplayable bodies
					// of water, they should be obliterated. If they're just surrounded by rocks,
					// trees, etc, they should be filled in with actors.
					if (waterIsPlayable)
					{
						var mask = CellLayerUtils.Clone(playable);
						terraformer.ZoneFromOutOfBounds(mask, true);
						terraformer.FillUnmaskedSideAndBorder(
							mask,
							landCoastWater,
							Terraformer.Side.Out,
							cpos => map.Tiles[cpos] = terraformer.PickTile(pickAnyRandom, param.LandTile));
					}

					var replace = PlayableToReplaceable();
					foreach (var mpos in map.AllCells.MapCoords)
						if (playable[mpos] || !map.Bounds.Contains(mpos.U, mpos.V))
							replace[mpos] = MultiBrush.Replaceability.None;

					terraformer.PaintArea(debrisTilingRandom, replace, param.UnplayableObstacles);
				}
			}

			if (param.Roads)
			{
				// TODO: Move or collapse into configuration
				const int RoadMinimumShrinkLength = 12;
				const int RoadStraightenShrink = 4;
				const int RoadStraightenGrow = 2;
				const int RoadInertialRange = 8;

				var roadPaths = terraformer.PlanRoads(
					terraformer.CheckSpace(param.ClearTerrain, true, false),
					param.RoadSpacing,
					RoadMinimumShrinkLength + 2 * (RoadStraightenShrink + param.RoadShrink));
				foreach (var roadPath in roadPaths)
				{
					var tilingPath = TilingPath.QuickCreate(
						map,
						param.SegmentedBrushes,
						roadPath,
						param.RoadSpacing - 1,
						param.RoadSegmentType,
						param.ClearSegmentType)
							.StraightenEnds(
								RoadStraightenShrink + param.RoadShrink,
								RoadStraightenGrow,
								RoadMinimumShrinkLength,
								RoadInertialRange)
							.RetainIfValid();
					if (tilingPath.Points == null)
						continue;

					var brush = tilingPath.Tile(roadTilingRandom)
						?? throw new MapGenerationException("Could not fit tiles for roads");
					terraformer.PaintTiling(pickAnyRandom, brush);
				}
			}

			if (param.CreateEntities)
			{
				var zoneable = terraformer.GetZoneable(param.ZoneableTerrain, playable);

				var zoneableArea = zoneable.Count(v => v);
				var symmetryCount = Symmetry.RotateAndMirrorProjectionCount(param.Rotations, param.Mirror);

				// Spawn generation
				var symmetryPlayers = param.Players / symmetryCount;
				for (var iteration = 0; iteration < symmetryPlayers; iteration++)
				{
					var chosenCPos = terraformer.ChooseSpawnInZoneable(
						playerRandom,
						zoneable,
						param.CentralSpawnReservationFraction,
						param.MinimumSpawnRadius,
						param.SpawnRegionSize,
						param.SpawnReservation)
							?? throw new MapGenerationException("Not enough room for player spawns");

					var spawn = new ActorPlan(map, "mpspawn")
					{
						Location = chosenCPos,
					};

					terraformer.ProjectPlaceDezoneActor(spawn, zoneable, new WDist(param.SpawnReservation * 1024));
				}

				// Grow resources
				var targetResourceValue =
					(long)zoneableArea * param.AreaResourceMultiplier +
					(long)param.Players * param.PlayerCountResourceMultiplier;
				if (targetResourceValue > 0)
				{
					var resourceBiases = new List<Terraformer.ResourceBias>();
					var targetResourceBiases = (int)(targetResourceValue / param.ResourcesPerPatch);
					while (resourceBiases.Count < targetResourceBiases)
					{
						const int MinimumPatchRadius = 2;
						var (chosenCPos, chosenValue) = terraformer.ChooseInZoneable(
							resourceSpawnRandom, zoneable, MinimumPatchRadius);
						if (chosenValue < MinimumPatchRadius)
							break;

						var projections = Symmetry.RotateAndMirrorWPos(
							CellLayerUtils.CPosToWPos(chosenCPos, map.Grid.Type),
							map.Tiles,
							terraformer.Rotations,
							terraformer.WMirror);

						foreach (var wpos in projections)
						{
							CellLayerUtils.OverCircle(
								cellLayer: zoneable,
								wCenter: wpos,
								wRadius: new WDist(param.ResourceSpawnReservation * 1024),
								outside: false,
								action: (mpos, _, _, _) => zoneable[mpos] = false);
							resourceBiases.Add(
								new Terraformer.ResourceBias(wpos)
									{
										BiasRadius = new WDist(16 * 1024),
										Bias = (value, rSq) => value + (int)(1024 * 1024 / (1024 + rSq / 1024)),
										ResourceType = param.DefaultResource,
									});
						}
					}

					var resourcePattern = terraformer.ResourceNoise(
						resourceRandom,
						param.ResourceFeatureSize,
						param.OreClumpiness,
						param.OreUniformity * 1024 / FractionMax);

					var (plan, typePlan) = terraformer.PlanResources(
						resourcePattern,
						CellLayerUtils.Intersect([playable, terraformer.CheckSpace(null, true)]),
						param.DefaultResource,
						resourceBiases);
					terraformer.GrowResources(
						plan,
						typePlan,
						targetResourceValue);
					terraformer.ZoneFromResources(zoneable, false);
				}

				// CivilianBuildings
				if (param.CivilianBuildings > 0)
				{
					var decorationNoise = terraformer.DecorationPattern(
						decorationRandom,
						terraformer.CheckSpace(param.PlayableTerrain, true),
						CellLayerUtils.Intersect([zoneable, terraformer.CheckSpace(param.LandTile)]),
						param.CivilianBuildings,
						param.CivilianBuildingsFeatureSize,
						param.CivilianBuildingDensity,
						param.MinimumCivilianBuildingDensity,
						param.CivilianBuildingDensityRadius);
					terraformer.PaintActors(
						decorationTilingRandom,
						decorationNoise,
						param.CivilianBuildingsObstacles,
						alwaysPreferLargerBrushes: true);
				}
			}

			// Cosmetically repaint tiles
			terraformer.RepaintTiles(repaintRandom, param.RepaintTiles);

			terraformer.ReorderPlayerSpawns();
			terraformer.BakeMap();

			return map;
		}

		public bool TryGenerateMetadata(ModData modData, MapGenerationArgs args, out MapPlayers? players, out Dictionary<string, MiniYaml>? ruleDefinitions)
		{
			try
			{
				var playerCount = FieldLoader.GetValue<int>("Players", args.Settings.NodeWithKey("Players").Value.Value);

				// Generated maps use the default ruleset
				ruleDefinitions = [];
				players = new MapPlayers(modData.DefaultRules, playerCount);

				return true;
			}
			catch
			{
				players = null;
				ruleDefinitions = null;
				return false;
			}
		}

		public override object Create(ActorInitializer init)
		{
			return new OpenE2140MapGenerator(init, this);
		}
	}

	public class OpenE2140MapGenerator : IEditorTool
	{
		public string Label { get; }
		public string PanelWidget { get; }
		public TraitInfo TraitInfo { get; }
		public bool IsEnabled { get; }

		public OpenE2140MapGenerator(ActorInitializer init, OpenE2140MapGeneratorInfo info)
		{
			Label = info.Name!;
			PanelWidget = info.PanelWidget;
			TraitInfo = info;
			IsEnabled = info.Tilesets!.Contains(init.Self.World.Map.Tileset);
		}
	}
}
