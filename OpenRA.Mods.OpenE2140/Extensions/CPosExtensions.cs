namespace OpenRA.Mods.OpenE2140.Extensions;

public static class CPosExtensions
{
	/// <summary>
	/// Calculates bounding box from list of cells.
	/// </summary>
	/// <returns>Tuple of top left and bottom right world positions.</returns>
	public static (WPos TopLeft, WPos BottomRight) GetBounds(this IEnumerable<CPos> cells)
	{
		var left = int.MaxValue;
		var right = int.MinValue;
		var top = int.MaxValue;
		var bottom = int.MinValue;

		foreach (var cell in cells)
		{
			left = Math.Min(left, cell.X);
			right = Math.Max(right, cell.X);
			top = Math.Min(top, cell.Y);
			bottom = Math.Max(bottom, cell.Y);
		}

		return (
			TopLeft: new WPos(1024 * left, 1024 * top, 0),
			BottomRight: new WPos(1024 * right + 1024, 1024 * bottom + 1024, 0)
		);
	}
}
