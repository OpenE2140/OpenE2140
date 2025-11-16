FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

RUN apt-get update && \
  apt-get install -y \
  ca-certificates lsb-release curl unzip libfreetype6 libopenal1 liblua5.1-0 libsdl2-2.0-0 \
  --no-install-recommends && \
  apt-get clean && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

RUN mkdir /srv/opene2140

RUN useradd opene2140 -r -d /srv/opene2140 -s /bin/bash
RUN chown opene2140:opene2140 /srv/opene2140

USER opene2140

COPY --chown=opene2140:opene2140 . /srv/opene2140/src/
WORKDIR /srv/opene2140/

ARG TAG

RUN ./src/packaging/linux/buildserver.sh $TAG ./build/

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0

COPY --from=build /srv/opene2140/build /srv/opene2140/
COPY ./docker/opene2140-server.sh /usr/local/bin/

ENV MOD_SEARCH_PATHS=/srv/opene2140/mods/
ENV NAME="Dedicated Server" \
    ADVERTISE_ONLINE="False" \
    LISTEN_PORT="1234" \
    PASSWORD="" \
    RECORD_REPLAYS="False" \
    REQUIRE_AUTHENTICATION="False" \
    PROFILE_ID_BLACKLIST="" \
    PROFILE_ID_WHITELIST="" \
    ENABLE_SINGLE_PLAYER="False" \
    ENABLE_SYNC_REPORTS="False" \
    ENABLE_GEOIP="False" \
    ENABLE_LINT_CHECKS="False" \
    SHARE_ANONYMISED_IPS="False" \
    FLOOD_LIMIT_JOIN_COOLDOWN="5000"

USER $APP_UID
VOLUME ["/srv/opene2140/.openra"]

ENTRYPOINT ["/usr/local/bin/opene2140-server.sh"]

LABEL org.opencontainers.image.title="OpenE2140 dedicated server" \
      org.opencontainers.image.description="Image to run a server instance for OpenE2140" \
      org.opencontainers.image.url="https://github.com/OpenE2140/OpenE2140" \
      org.opencontainers.image.licenses="GPL-3.0"
