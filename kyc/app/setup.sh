#!/bin/bash
# Keycloak marks the root user from the env file as temporary, so this makes it permanent.
set -e
kcadm=/opt/keycloak/bin/kcadm.sh
$kcadm config credentials --config /tmp/kcadm --server http://kyc-proxy:5000 --realm master \
  --user "$KC_BOOTSTRAP_ADMIN_USERNAME" --password "$KC_BOOTSTRAP_ADMIN_PASSWORD"
id=$($kcadm get users --config /tmp/kcadm -r master -q username="$KC_BOOTSTRAP_ADMIN_USERNAME" --fields id --format csv --noquotes)
$kcadm update "users/$id" --config /tmp/kcadm -r master -s 'attributes={}'
