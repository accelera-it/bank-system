#!/usr/bin/env bash
# Generates an RSA key pair in .NET <RSAKeyValue> XML format (as used in appsettings.json).
# Usage: ./rsa-keygen.sh [bits]   (default: 4096)
set -euo pipefail

bits="${1:-4096}"
pem="$(openssl genpkey -algorithm RSA -pkeyopt "rsa_keygen_bits:${bits}" 2>/dev/null)"
text="$(printf '%s\n' "$pem" | openssl pkey -text -noout)"

# Extract a hex field from `openssl pkey -text` output (multi-line, colon-separated).
field_hex() {
  printf '%s\n' "$text" | awk -v name="$1:" '
    $1 == name { found = 1; next }
    found && /^ / { gsub(/[ :]/, ""); printf "%s", $0; next }
    found { exit }'
}

exponent_hex() {
  printf '%s\n' "$text" | sed -nE 's/^publicExponent: [0-9]+ \(0x([0-9a-fA-F]+)\).*/\1/p'
}

# Hex -> base64 of an unsigned big-endian integer, left-padded to $2 bytes (0 = no padding).
to_b64() {
  local hex="$1" len="$2"
  hex="$(printf '%s' "$hex" | sed -E 's/^(00)+//')"
  (( ${#hex} % 2 )) && hex="0$hex"
  if (( len > 0 )); then
    while (( ${#hex} < len * 2 )); do hex="00$hex"; done
  fi
  printf '%s' "$hex" | xxd -r -p | base64 | tr -d '\n'
}

full=$(( bits / 8 ))
half=$(( full / 2 ))

modulus="$(to_b64 "$(field_hex modulus)" "$full")"
exponent="$(to_b64 "$(exponent_hex)" 0)"
p="$(to_b64 "$(field_hex prime1)" "$half")"
q="$(to_b64 "$(field_hex prime2)" "$half")"
dp="$(to_b64 "$(field_hex exponent1)" "$half")"
dq="$(to_b64 "$(field_hex exponent2)" "$half")"
inverse_q="$(to_b64 "$(field_hex coefficient)" "$half")"
d="$(to_b64 "$(field_hex privateExponent)" "$full")"

public_key="<RSAKeyValue><Modulus>${modulus}</Modulus><Exponent>${exponent}</Exponent></RSAKeyValue>"
private_key="<RSAKeyValue><Modulus>${modulus}</Modulus><Exponent>${exponent}</Exponent><P>${p}</P><Q>${q}</Q><DP>${dp}</DP><DQ>${dq}</DQ><InverseQ>${inverse_q}</InverseQ><D>${d}</D></RSAKeyValue>"

echo "Public key: ${public_key}"
echo "====================================================================="
echo "Private key: ${private_key}"
