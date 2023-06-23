using System.Runtime.CompilerServices;
using OpenRA.Support;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class VariableExpressionExtensions
{
	public static IntegerExpression Concat(this IntegerExpression expression, FormattableString format)
	{
		var args = format.GetArguments()
			.Select(arg => arg switch
				{
					VariableExpression variable => variable.Expression,
					_ => arg
				})
			.ToArray();

		 return new IntegerExpression(expression.Expression + FormattableStringFactory.Create(format.Format, args).ToString());
	}
}
