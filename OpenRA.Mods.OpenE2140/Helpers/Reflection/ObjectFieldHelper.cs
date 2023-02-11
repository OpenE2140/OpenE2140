using System.Reflection;

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection
{
	public class ObjectFieldHelper<T>
	{
		public object ThisObject { get; }

		public FieldInfo FieldInfo { get; }

		public ObjectFieldHelper(FieldInfo fieldInfo, object thisObject)
		{
			this.FieldInfo = fieldInfo;
			this.ThisObject = thisObject;
		}

		public T? Value
		{
			get => (T?)this.FieldInfo.GetValue(this.ThisObject);
			set => this.FieldInfo.SetValue(this.ThisObject, value);
		}
	}
}
