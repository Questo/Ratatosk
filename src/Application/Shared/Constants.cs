using Ratatosk.Core.Primitives;

public static class Errors
{
    public static class Authentication
    {
        public static readonly Error InvalidCredentials = new(
            "auth.invalid_credentials",
            "Invalid email or password."
        );

        public static readonly Error AccountDisabled = new(
            "auth.account_disabled",
            "Account is disabled."
        );

        public static readonly Error AccountAlreadyExists = new(
            "auth.account_already_exists",
            "Account already exists."
        );
    }
}
