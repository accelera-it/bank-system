package uz.abs.kyc;

import org.keycloak.authentication.RequiredActionContext;
import org.keycloak.authentication.RequiredActionProvider;
import org.keycloak.authentication.requiredactions.UpdatePassword;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.UserCredentialModel;
import org.keycloak.models.UserModel;

/** Keycloak's password form, which also has to get the current password right. */
public class CurrentPasswordAction extends UpdatePassword {
    public static final String ID = "ABS_UPDATE_PASSWORD";

    private static final String CURRENT_PASSWORD = "password-current";

    @Override
    public void processAction(RequiredActionContext context) {
        var form = context.getHttpRequest().getDecodedFormParameters();
        var cancelled = form.containsKey("cancel-aia");
        var current = form.getFirst(CURRENT_PASSWORD);

        if (!cancelled && !isCurrentPassword(context.getUser(), current)) {
            var response = context.form()
                    .setError("The current password is wrong")
                    .createResponse(UserModel.RequiredAction.UPDATE_PASSWORD);
            context.challenge(response);
            return;
        }

        super.processAction(context);
    }

    private static boolean isCurrentPassword(UserModel user, String password) {
        return password != null && user.credentialManager().isValid(UserCredentialModel.password(password));
    }

    @Override
    public RequiredActionProvider create(KeycloakSession session) {
        return new CurrentPasswordAction();
    }

    @Override
    public String getId() {
        return ID;
    }

    @Override
    public String getDisplayText() {
        return "Update password with the current one";
    }
}
