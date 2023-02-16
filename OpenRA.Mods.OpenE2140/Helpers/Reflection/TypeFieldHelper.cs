using System.Reflection;

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection
{
	public class TypeFieldHelper<T>
	{
		public FieldInfo FieldInfo { get; }

		public TypeFieldHelper(FieldInfo fieldInfo)
		{
			this.FieldInfo = fieldInfo ?? throw new ArgumentNullException(nameof(fieldInfo));
		}

		public T? GetValue(object thisObject)
		{
			return (T?)this.FieldInfo.GetValue(thisObject);
		}

		public void SetValue(object thisObject, T? value)
		{
			this.FieldInfo.SetValue(thisObject, value);
		}
	}
}
