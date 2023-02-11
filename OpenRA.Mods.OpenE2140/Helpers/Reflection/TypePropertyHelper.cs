using System.Reflection;

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection
{
	public class TypePropertyHelper<T>
	{
		public PropertyInfo PropertyInfo { get; }

		public TypePropertyHelper(PropertyInfo fieldInfo)
		{
			this.PropertyInfo = fieldInfo ?? throw new ArgumentNullException(nameof(fieldInfo));
		}

		public T? GetValue(object thisObject)
		{
			return (T?)this.PropertyInfo.GetValue(thisObject);
		}
	}
}
