// READU.md — Licensed under the MIT License.

using System;
using System.Security.Cryptography;
using System.Text;

namespace ReadU.Helpers;

/// <summary>
/// Keeps secrets (the AI API key) in settings.json encrypted with Windows DPAPI for the current user:
/// stored as <c>dpapi:&lt;base64&gt;</c>, readable only by the same Windows account on the same PC.
/// </summary>
public static class SecretProtector
{
    private const string Prefix = "dpapi:";

    // Changing this makes saved keys unreadable.
    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("READU.md.AiApiKey.v1");

    /// <summary>True for a value written before keys were encrypted (plain text).</summary>
    public static bool IsPlainText(string stored) =>
        !string.IsNullOrEmpty(stored) && !stored.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return string.Empty;
        var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), s_entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(cipher);
    }

    /// <summary>
    /// The secret, or empty when it can't be decrypted (e.g. settings.json copied from another PC or
    /// account) — the user then enters the key again.
    /// </summary>
    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored))
            return string.Empty;
        if (IsPlainText(stored))
            return stored; // not migrated yet; SettingsWatcher encrypts it on the next read

        try
        {
            var cipher = Convert.FromBase64String(stored[Prefix.Length..]);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(cipher, s_entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            Logger.LogWarning($"The saved API key can't be decrypted on this PC/account: {ex.GetType().Name}");
            return string.Empty;
        }
    }
}
