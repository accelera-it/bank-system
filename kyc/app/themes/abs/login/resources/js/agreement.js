// Phone and address are edited on the account page, not when registering.
const hidden = document.createElement("style");
hidden.textContent = "#kc-register-form .pf-v5-c-form__group:has(#phone, #address) { display: none; }";
document.head.append(hidden);

// The register button stays off until the agreement is accepted. Keycloak enforces it on the server too.
document.addEventListener("DOMContentLoaded", () => {
  const terms = document.querySelector('input[name="termsAccepted"]');
  const submit = document.querySelector('#kc-register-form [type="submit"]');
  if (!terms || !submit) return;

  const update = () => { submit.disabled = !terms.checked; };
  terms.addEventListener("change", update);
  update();
});
