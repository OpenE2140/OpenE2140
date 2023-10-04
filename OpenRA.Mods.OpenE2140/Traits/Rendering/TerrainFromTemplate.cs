using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Replaces tiles from specified terrain template underneath the actor. Trait is conditional, so original tiles can be reverted if necessary.")]
public class TerrainFromTemplateInfo : ConditionalTraitInfo, Requires<IOccupySpaceInfo>
{
	[Desc("ID of template to render tiles from.")]
	[FieldLoader.Require]
	public readonly ushort TemplateId;

	public override object Create(ActorInitializer init)
	{
		return new TerrainFromTemplate(init.Self, this);
	}
}

public class TerrainFromTemplate : ConditionalTrait<TerrainFromTemplateInfo>
{
	private readonly DefaultTerrainTemplateInfo terrainTemplate;

	private readonly Dictionary<CPos, TerrainTile> originalTerrainTiles;
	private readonly Dictionary<CPos, TerrainTile> newTerrainTiles;

	public TerrainFromTemplate(Actor self, TerrainFromTemplateInfo info)
		: base(info)
	{
		var terrainInfo = (ITemplatedTerrainInfo)self.World.Map.Rules.TerrainInfo;
		this.terrainTemplate = (DefaultTerrainTemplateInfo)terrainInfo.Templates[this.Info.TemplateId];

		this.originalTerrainTiles = this.CreateTerrainDictionary(self.Location, t => self.World.Map.Tiles[t.pos]);
		this.newTerrainTiles = this.CreateTerrainDictionary(self.Location, t => new TerrainTile(this.terrainTemplate.Id, (byte)t.i));
	}

	/// <summary>
	/// Creates terrain cell dictionary from all tiles of terrain template, transforming each tile into <typeparamref name="T"/>.
	/// </summary>
	/// <param name="action">Delegate which receives tuple with cell position (in the map) and index of tile in the template.</param>
	private Dictionary<CPos, T> CreateTerrainDictionary<T>(CPos basePosition, Func<(CPos pos, int i), T> action)
	{
		var width = this.terrainTemplate.Size.X;
		var height = this.terrainTemplate.Size.Y;
		var result = new Dictionary<CPos, T>(width * height);

		var i = 0;
		for (var y = 0; y < this.terrainTemplate.Size.Y; y++)
		{
			for (var x = 0; x < this.terrainTemplate.Size.X; x++, i++)
			{
				var pos = new CPos(basePosition.X + x, basePosition.Y + y);
				result[pos] = action((pos, i));
			}
		}

		return result;
	}

	protected override void TraitEnabled(Actor self)
	{
		foreach (var (pos, tile) in this.newTerrainTiles)
		{
			self.World.Map.Tiles[pos] = tile;
		}
	}

	protected override void TraitDisabled(Actor self)
	{
		foreach (var (pos, tile) in this.originalTerrainTiles)
		{
			self.World.Map.Tiles[pos] = tile;
		}
	}
}
