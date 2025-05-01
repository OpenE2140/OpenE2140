using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Misc;

public class ConditionWatcher : IObservesVariables
{
	private readonly Dictionary<BooleanExpression, Handler> handlers = [];

	public ConditionWatcher Watch(BooleanExpression condition, Action<bool> onChange)
	{
		if (this.handlers.ContainsKey(condition))
			throw new InvalidOperationException($"Handler already defined for condition: {condition}");

		this.handlers.Add(condition, new Handler(condition, onChange));
		return this;
	}

	public IEnumerable<VariableObserver> GetVariableObservers()
	{
		foreach (var handler in this.handlers.Values)
		{
			yield return handler.CreateObserver();
		}
	}

	public bool IsEnabled(BooleanExpression condition)
	{
		if (!this.handlers.TryGetValue(condition, out var handler))
			throw new InvalidOperationException($"Could not find handler for condition: {condition}");

		return handler.IsEnabled;
	}

	private record Handler(BooleanExpression Condition, Action<bool> OnChange)
	{
		public bool IsEnabled { get; private set; }

		public VariableObserver CreateObserver()
		{
			return new VariableObserver(this.Observer, this.Condition.Variables);
		}

		private void Observer(Actor self, IReadOnlyDictionary<string, int> conditions)
		{
			var newState = this.Condition.Evaluate(conditions);
			if (newState != this.IsEnabled)
			{
				this.IsEnabled = newState;

				this.OnChange(newState);
			}
		}
	}
}
