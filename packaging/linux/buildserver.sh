#!/bin/bash
# OpenRA packaging script for server Docker image
set -e

command -v curl >/dev/null 2>&1 || command -v wget > /dev/null 2>&1 || { echo >&2 "The OpenRA mod SDK Linux packaging requires curl or wget."; exit 1; }

require_variables() {
	missing=""
	for i in "$@"; do
		eval check="\$$i"
		[ -z "${check}" ] && missing="${missing}   ${i}\n"
	done
	if [ -n "${missing}" ]; then
		printf "Required mod.config variables are missing:\n%sRepair your mod.config (or user.config) and try again.\n" "${missing}"
		exit 1
	fi
}

if [ $# -ne "2" ]; then
	echo "Usage: $(basename "$0") version outputdir"
	exit 1
fi

PACKAGING_DIR=$(dirname $(readlink -f "$0"))
TEMPLATE_ROOT="$(readlink -f ${PACKAGING_DIR}/../../)"

# shellcheck source=mod.config
. "${TEMPLATE_ROOT}/mod.config"

if [ -f "${TEMPLATE_ROOT}/user.config" ]; then
	# shellcheck source=user.config
	. "${TEMPLATE_ROOT}/user.config"
fi

require_variables "MOD_ID" "ENGINE_DIRECTORY" "PACKAGING_DISPLAY_NAME" "PACKAGING_OVERWRITE_MOD_VERSION"

TAG="$1"
OUTPUTDIR=$(readlink -f "$2")

echo "Fetching engine"
${TEMPLATE_ROOT}/fetch-engine.sh || (echo "Unable to continue without engine files"; exit 1)

# Set the working dir to the location of this script
cd "${PACKAGING_DIR}"

. "${TEMPLATE_ROOT}/${ENGINE_DIRECTORY}/packaging/functions.sh"
. "${TEMPLATE_ROOT}/packaging/functions.sh"

mkdir -p ${OUTPUTDIR}

echo "Building core files"
install_assemblies "${TEMPLATE_ROOT}/${ENGINE_DIRECTORY}" "${OUTPUTDIR}" "linux-x64" "True" "False" "False"
install_data "${TEMPLATE_ROOT}/${ENGINE_DIRECTORY}" "${OUTPUTDIR}"

for f in ${PACKAGING_COPY_ENGINE_FILES}; do
	mkdir -p "${OUTPUTDIR}/$(dirname "${f}")"
	cp -r "${TEMPLATE_ROOT}/${ENGINE_DIRECTORY}/${f}" "${OUTPUTDIR}/${f}"
done

echo "Building mod files"
install_mod_assemblies "${TEMPLATE_ROOT}" "${OUTPUTDIR}" "linux-x64" "${TEMPLATE_ROOT}/${ENGINE_DIRECTORY}"

cp -Lr "${TEMPLATE_ROOT}/mods/"* "${OUTPUTDIR}/mods"

set_engine_version "${ENGINE_VERSION}" "${OUTPUTDIR}"
if [ "${PACKAGING_OVERWRITE_MOD_VERSION}" == "True" ]; then
	set_mod_version "${TAG}" "${OUTPUTDIR}/mods/${MOD_ID}/mod.yaml"
else
	MOD_VERSION=$(grep 'Version:' "${OUTPUTDIR}/mods/${MOD_ID}/mod.yaml" | awk '{print $2}')
	echo "Mod version ${MOD_VERSION} will remain unchanged.";
fi

echo "Build complete"

