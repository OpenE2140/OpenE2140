using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class NumberExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBetween<T>(this T number, T lowerBound, T upperBound)
		where T : INumber<T>
	{
		return lowerBound <= number && number < upperBound;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T PowerOf2<T>(this T number)
		where T : INumber<T>
	{
		return number * number;
	}
}
