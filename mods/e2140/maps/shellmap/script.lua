SetupUnits = function()
	Trigger.AfterDelay(1, function()
		Utils.Do(Map.NamedActors, function(a)
			if a.HasProperty("TransportCrates") then
				a.TransportCrates()
			end
		end)
	end)
end



WorldLoaded = function()
	Camera.Position = Viewport.CenterPosition

	SetupUnits()
end
