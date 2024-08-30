namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public static class CrateLoadUnloadHelpers
{
	public static readonly int NonDiagonalDockDistance = 405;
	public static readonly int DiagonalDockDistance = 570;

	public static WVec GetDockVector(CVec vector)
	{
		var isDiagonal = vector.X != 0 && vector.Y != 0;

		return new WVec(vector.X, vector.Y, 0) * (isDiagonal ? DiagonalDockDistance : NonDiagonalDockDistance);
	}
}
