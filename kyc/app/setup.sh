#!/bin/bash
# Starts Keycloak. It marks the root user from the env file as temporary, so this also makes it permanent.
# The master realm is not imported, so its settings are applied here on every start.
kcadm=/opt/keycloak/bin/kcadm.sh

set_up_master() {
  for _ in {1..40}; do
    if $kcadm config credentials --config /tmp/kcadm --server http://127.0.0.1:8080 --realm master \
      --user "$KC_BOOTSTRAP_ADMIN_USERNAME" --password "$KC_BOOTSTRAP_ADMIN_PASSWORD" 2>/dev/null; then
      id=$($kcadm get users --config /tmp/kcadm -r master -q username="$KC_BOOTSTRAP_ADMIN_USERNAME" --fields id --format csv --noquotes)
      # The mark is an unmanaged attribute, which an update leaves alone unless the policy allows them
      $kcadm get users/profile --config /tmp/kcadm -r master > /tmp/user-profile.json
      sed '0,/{/s//{"unmanagedAttributePolicy":"ENABLED",/' /tmp/user-profile.json > /tmp/user-profile-open.json
      $kcadm update users/profile --config /tmp/kcadm -r master -f /tmp/user-profile-open.json
      $kcadm update "users/$id" --config /tmp/kcadm -r master -s 'attributes={}'
      $kcadm update users/profile --config /tmp/kcadm -r master -f /tmp/user-profile.json
      $kcadm update realms/master --config /tmp/kcadm -s rememberMe=false -s loginWithEmailAllowed=false -s sslRequired=none
      return
    fi
    sleep 3
  done
  echo "The root user was not made permanent: Keycloak refused the login" >&2
}

/opt/keycloak/bin/kc.sh "$@" &
server=$!
trap 'kill -TERM "$server"' TERM INT

if [ -n "$KC_BOOTSTRAP_ADMIN_USERNAME" ]; then
  set_up_master &
fi

wait "$server"
wait "$server"
