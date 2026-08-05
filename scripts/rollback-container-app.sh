#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 ]]; then
  echo "Usage: rollback-container-app.sh <resource-group> <container-app-name> <image-ref> [label]" >&2
  exit 1
fi

RESOURCE_GROUP="$1"
CONTAINER_APP_NAME="$2"
IMAGE_REF="$3"
LABEL="${4:-previous}"

az containerapp update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$CONTAINER_APP_NAME" \
  --image "$IMAGE_REF"

echo "Rolled back $CONTAINER_APP_NAME to image $IMAGE_REF ($LABEL)"
