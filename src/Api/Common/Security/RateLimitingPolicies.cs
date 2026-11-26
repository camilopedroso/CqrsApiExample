namespace CqrsApiExample.Api.Common.Security;

/// <summary>Names of the rate limiting policies, for <c>[EnableRateLimiting(...)]</c>.</summary>
public static class RateLimitingPolicies
{
    /// <summary>Login and registration: <see cref="RateLimitingOptions.Authentication"/>, per IP address.</summary>
    public const string Authentication = "authentication";
}
