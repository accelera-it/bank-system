// The tab icon of ABS ID, in the same colors.
const icon = document.createElement("link");
icon.rel = "icon";
icon.type = "image/svg+xml";
icon.href = "data:image/svg+xml," + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" rx="14" fill="#ffc700"/><path fill="#212529" d="M32 10 14 18v14c0 11 8 19 18 22 10-3 18-11 18-22V18z"/></svg>');
document.querySelectorAll('link[rel~="icon"]').forEach((link) => link.remove());
document.head.append(icon);

// The colors of cbu.uz, always light.
const colors = document.createElement("style");
colors.textContent = `
  :root { --pf-v5-global--primary-color--100: #e4a216; --pf-v5-global--link--Color: #8a5a00; --pf-v5-global--link--Color--hover: #8a5a00; }
  html, body, .pf-v5-c-login, .login-pf body { background: #f6f5e7 !important; }
  #kc-header, #kc-header-wrapper, .pf-v5-c-login__header, .pf-v5-c-login__main-header { color: #212529 !important; }
  .pf-v5-c-login__main, .card-pf { border-top: 4px solid #ffc700 !important; border-radius: 8px; box-shadow: 0 4px 16px rgba(33, 37, 41, 0.08); }
  input[type=checkbox] { accent-color: #e4a216; }
  .pf-v5-c-login__main::before, .pf-v5-c-login__main::after { background: #ffc700 !important; }
  .pf-v5-c-form__label-text { font-weight: 600; }
  .pf-v5-c-button.pf-m-secondary { color: #8a5a00; }
  .pf-v5-c-button.pf-m-secondary::after { border-color: #e4a216; }
  .pf-v5-c-button.pf-m-primary { background: #ffc700; color: #212529; }
  .pf-v5-c-button.pf-m-primary:hover { background: #e4a216; color: #212529; }
`;
document.head.append(colors);

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

// Keycloak's password form has no current password. CurrentPasswordAction checks it on the server.
document.addEventListener("DOMContentLoaded", () => {
  const form = document.querySelector("#kc-passwd-update-form");
  const newPassword = form?.querySelector("#password-new");
  if (!newPassword) return;

  const group = newPassword.closest(".pf-v5-c-form__group");
  const current = group.cloneNode(true);
  const input = current.querySelector("input");
  input.id = "password-current";
  input.name = "password-current";
  input.value = "";
  input.autocomplete = "current-password";
  input.required = true;
  current.querySelector("label").setAttribute("for", "password-current");
  current.querySelector("label").textContent = "Current password";
  current.querySelector("button")?.setAttribute("aria-controls", "password-current");
  current.querySelector("button")?.setAttribute("data-password-toggle", "password-current");
  group.before(current);
  input.focus();
});
