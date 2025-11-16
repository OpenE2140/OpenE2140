#!/usr/bin/sh

dir=$(dirname "$(readlink -f "$0")")

echo "Starting dedicated server"

/srv/opene2140/OpenRA.Server \
	Engine.EngineDir="." Game.Mod="e2140" \
	Server.ListenPort="${LISTEN_PORT}" \
	Server.Name="${NAME}" \
	Server.Map="${MAP}" \
	Server.AdvertiseOnline="${ADVERTISE_ONLINE}" \
	Server.Password="${PASSWORD}" \
	Server.RecordReplays="${RECORD_REPLAYS}" \
	Server.RequireAuthentication="${REQUIRE_AUTHENTICATION}" \
	Server.ProfileIDBlacklist="${PROFILE_ID_BLACKLIST}" \
	Server.ProfileIDWhitelist="${PROFILE_ID_WHITELIST}" \
	Server.EnableSingleplayer="${ENABLE_SINGLE_PLAYER}" \
	Server.EnableSyncReports="${ENABLE_SYNC_REPORTS}" \
	Server.EnableGeoIP="${ENABLE_GEOIP}" \
	Server.EnableLintChecks="${ENABLE_LINT_CHECKS}" \
	Server.ShareAnonymizedIPs="${SHARE_ANONYMISED_IPS}" \
	Server.FloodLimitJoinCooldown="${FLOOD_LIMIT_JOIN_COOLDOWN}" \
	Engine.SupportDir="/srv/opene2140/.openra/"
