using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MarksApi.Api;

internal static class JwtSecurity
{
    public static SymmetricSecurityKey Key(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must contain at least 32 UTF-8 bytes.");
        }

        return new SymmetricSecurityKey(bytes);
    }
}
