#!/bin/bash
# Starts Keycloak. It marks the root user from the env file as temporary, so this also makes it permanent.
kcadm=/opt/keycloak/bin/kcadm.sh

make_root_permanent() {
  for _ in {1..40}; do
    if $kcadm config credentials --config /tmp/kcadm --server http://127.0.0.1:8080 --realm master \
      --user "$KC_BOOTSTRAP_ADMIN_USERNAME" --password "$KC_BOOTSTRAP_ADMIN_PASSWORD" 2>/dev/null; then
      id=$($kcadm get users --config /tmp/kcadm -r master -q username="$KC_BOOTSTRAP_ADMIN_USERNAME" --fields id --format csv --noquotes)
      $kcadm update "users/$id" --config /tmp/kcadm -r master -s 'attributes={}'
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
  make_root_permanent &
fi

wait "$server"
wait "$server"
