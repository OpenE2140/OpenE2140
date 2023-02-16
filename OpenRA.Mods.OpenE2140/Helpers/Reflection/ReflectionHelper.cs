using System.Reflection;

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection
{
	public class ReflectionHelper<T>
		where T : class
	{
		public T ThisObject { get; }

		public ReflectionHelper(T thisObject)
		{
			this.ThisObject = thisObject ?? throw new ArgumentNullException(nameof(thisObject));
		}

		public ObjectFieldHelper<TField> GetField<TField>(ObjectFieldHelper<TField>? _, string fieldName)
		{
			if (string.IsNullOrWhiteSpace(fieldName))
				throw new ArgumentException($"'{nameof(fieldName)}' cannot be null or whitespace.", nameof(fieldName));

			var fieldInfo = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Single(f => f.Name == fieldName);

			return new ObjectFieldHelper<TField>(fieldInfo, this.ThisObject!);
		}
	}

	public class ReflectionHelper
	{
		public static ObjectFieldHelper<T> GetFieldHelper<T>(object thisObject, string fieldName)
		{
			if (thisObject is null)
				throw new ArgumentNullException(nameof(thisObject));
			if (string.IsNullOrEmpty(fieldName))
				throw new ArgumentException($"'{nameof(fieldName)}' cannot be null or empty.", nameof(fieldName));

			var fieldInfo = thisObject.GetType().GetAllInstanceFields()
				.Single(f => f.Name == fieldName);
			return new ObjectFieldHelper<T>(fieldInfo, thisObject);
		}

		public static ObjectFieldHelper<T> GetFieldHelper<T>(object thisObject, ObjectFieldHelper<T>? _, string fieldName)
		{
			return GetFieldHelper<T>(thisObject, fieldName);
		}

		public static TypeFieldHelper<T> GetTypeFieldHelper<T>(Type thisObjectType, string fieldName)
		{
			if (thisObjectType is null)
				throw new ArgumentNullException(nameof(thisObjectType));
			if (string.IsNullOrEmpty(fieldName))
				throw new ArgumentException($"'{nameof(fieldName)}' cannot be null or empty.", nameof(fieldName));

			var fieldInfo = thisObjectType.GetAllInstanceFields()
				.Single(f => f.Name == fieldName);
			return new TypeFieldHelper<T>(fieldInfo);
		}
	}
}
