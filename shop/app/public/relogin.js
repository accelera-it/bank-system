// The profile or password changed and the Keycloak session ended, so the app behind has to sign in again.
if (window.opener) {
  window.opener.location.assign("/api/auth/login?next=/cabinet");
  window.close();
} else {
  window.location.replace("/api/auth/login?next=/cabinet");
}
