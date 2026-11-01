// The Keycloak popup ends here: the session already has its new tokens, so the app behind only has to reload.
if (window.opener) {
  window.opener.location.reload();
  window.close();
} else {
  window.location.replace("/");
}
