// The register button stays off until the agreement is accepted. Keycloak enforces it on the server too.
document.addEventListener("DOMContentLoaded", () => {
  const terms = document.querySelector('input[name="termsAccepted"]');
  const submit = document.querySelector('#kc-register-form [type="submit"]');
  if (!terms || !submit) return;

  const update = () => { submit.disabled = !terms.checked; };
  terms.addEventListener("change", update);
  update();
});
