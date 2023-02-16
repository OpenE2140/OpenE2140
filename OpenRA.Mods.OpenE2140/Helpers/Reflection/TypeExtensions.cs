using System.Reflection;

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection
{
	public static class TypeExtensions
	{
		public static IEnumerable<FieldInfo> GetAllInstanceFields(this Type type)
		{
			if (type is null)
				throw new ArgumentNullException(nameof(type));

			foreach (var item in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				yield return item;
			}

			var currentType = type.BaseType;

			while (currentType != null && currentType != typeof(object))
			{
				foreach (var item in currentType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					// Ignore non-private fields
					if (item.IsPrivate)
					{
						yield return item;
					}
				}

				currentType = currentType.BaseType;
			}
		}
	}
}
