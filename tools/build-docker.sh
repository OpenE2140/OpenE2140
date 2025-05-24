#!/usr/bin/sh

if [ $# -ne "1" ]; then
	echo "Usage: $(basename "$0") version"
	exit 1
fi

TAG="$1"
TOOLS_DIR=$(dirname $(readlink -f "$0"))
PROJECT_ROOT="$(readlink -f ${TOOLS_DIR}/../)"

podman build -t opene2140/server --build-arg "TAG=${TAG}" -f "${PROJECT_ROOT}/docker/server.Dockerfile" "${PROJECT_ROOT}"
