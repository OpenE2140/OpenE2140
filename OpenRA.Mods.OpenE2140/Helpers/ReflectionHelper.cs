using System.Reflection;

namespace OpenRA.Mods.OpenE2140.Helpers
{
	public class ReflectionHelper<T>
		where T : class
	{
		public T ThisObject { get; }

		public ReflectionHelper(T thisObject)
		{
			this.ThisObject = thisObject ?? throw new ArgumentNullException(nameof(thisObject));
		}

		public FieldHelper<TField> GetField<TField>(FieldHelper<TField>? _, string fieldName)
		{
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"'{nameof(fieldName)}' cannot be null or whitespace.", nameof(fieldName));

			var fieldInfo = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Single(f => f.Name == fieldName);

			return new FieldHelper<TField>(fieldInfo, this.ThisObject!);
		}
	}

	public class ReflectionHelper
	{
		public static FieldHelper<T> GetFieldHelper<T>(object thisObject, string fieldName)
		{
			if (thisObject is null)
				throw new ArgumentNullException(nameof(thisObject));
			if (string.IsNullOrEmpty(fieldName))
				throw new ArgumentException($"'{nameof(fieldName)}' cannot be null or empty.", nameof(fieldName));

			var fieldInfo = thisObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Single(f => f.Name == fieldName);
			return new FieldHelper<T>(fieldInfo, thisObject);
		}

		public static FieldHelper<T> GetFieldHelper<T>(object thisObject, FieldHelper<T>? _, string fieldName)
		{
			return GetFieldHelper<T>(thisObject, fieldName);
		}
	}
}
