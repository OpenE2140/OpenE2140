namespace OpenRA.Mods.OpenE2140.Extensions;

public static class CVecExtensions
{
	public static WVec ToWVec(this CVec vec)
	{
		return new WVec(vec.X, vec.Y, 0);
	}
}
