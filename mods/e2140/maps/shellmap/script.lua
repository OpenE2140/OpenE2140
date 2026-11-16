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
	ED = Player.GetPlayer("ED")
	UCS = Player.GetPlayer("UCS")
	Trigger.AfterDelay(DateTime.Seconds(0.5), function()
		ED.GrantCondition("enable-ed-ai")
		UCS.GrantCondition("enable-ucs-ai")
	end)
end
