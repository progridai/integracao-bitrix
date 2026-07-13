namespace WebApolice.BitrixIntegration.Infrastructure.Security;

using System.Text.RegularExpressions;

public static class SensitiveDataMasker
{
    public static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        if (token.Length <= 6)
            return new string('*', token.Length);

        return $"{token.Substring(0, 3)}***{token.Substring(token.Length - 3)}";
    }

    public static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        var pattern = @"(Password|Username|Database)=[^;]+";
        return Regex.Replace(connectionString, pattern, "$1=***");
    }
}
