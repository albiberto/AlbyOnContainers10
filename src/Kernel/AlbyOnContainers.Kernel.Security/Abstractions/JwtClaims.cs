namespace AlbyOnContainers.Kernel.Security.Abstractions;

public static class JwtClaims
{
    // Identity Claims
    public const string Subject = "sub";
    public const string PreferredUsername = "preferred_username";
    public const string Name = "name";
    public const string GivenName = "given_name";
    public const string FamilyName = "family_name";
    
    // Contact Claims
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string PhoneNumber = "phone_number";
    public const string PhoneNumberVerified = "phone_number_verified";
    public const string Address = "address";
    
    // Authorization & Protocol Claims
    public const string Roles = "roles";
    public const string Issuer = "iss";
    public const string Audience = "aud";
}