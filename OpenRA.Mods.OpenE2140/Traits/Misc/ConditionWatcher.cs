﻿#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

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
