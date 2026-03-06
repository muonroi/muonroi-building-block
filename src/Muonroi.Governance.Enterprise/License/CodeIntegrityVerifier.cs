using System.Reflection;
using System.Security.Cryptography;

namespace Muonroi.Governance.License;

/// <summary>
/// Verifies the integrity of the BuildingBlock assembly at runtime.
/// Detects if the DLL has been modified by an attacker.
///
/// TIER 3+: Code Integrity Protection
/// This provides an additional layer of defense against DLL modification attacks.
/// However, note that determined attackers can still bypass this by removing the check entirely.
/// </summary>
public static class CodeIntegrityVerifier
{
    // This hash will be replaced at build time with the actual assembly hash.
    // For development, we use a placeholder that allows the check to pass.
    // In production builds, use a build script to calculate and inject the real hash.
    private const string ExpectedHash = "kaFTOOn9nFkIwWJ93ys72hjk1o+efVYeXrA/8ghMk40=";

    private static bool? _cachedResult;

    /// <summary>
    /// Verifies the integrity of the executing assembly.
    /// Returns true if the assembly hash matches the expected hash, false otherwise.
    /// </summary>
    /// <param name="throwOnFailure">If true, throws SecurityException on mismatch. Default: false.</param>
    /// <returns>True if integrity check passes, false otherwise.</returns>
    public static bool VerifyIntegrity(bool throwOnFailure = false)
    {
        // Use cached result to avoid repeated hash calculations
        if (_cachedResult.HasValue)
        {
            if (throwOnFailure && !_cachedResult.Value)
            {
                throw new SecurityException(
                    "BuildingBlock assembly integrity check FAILED (cached result). " +
                    "The DLL may have been modified by an attacker. " +
                    "Please reinstall from official NuGet source.");
            }
            return _cachedResult.Value;
        }

        try
        {
            // Skip check in development mode
            if (string.Equals(ExpectedHash, "DEVELOPMENT_MODE_SKIP_CHECK", StringComparison.Ordinal))
            {
                _cachedResult = true;
                return true;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();

            // Check if assembly location is available
            if (string.IsNullOrEmpty(assembly.Location))
            {
                // Single-file deployment or other scenario where location is not available
                // Skip integrity check in this case
                _cachedResult = true;
                return true;
            }

            // Calculate SHA256 hash of the assembly file
            byte[] assemblyBytes = File.ReadAllBytes(assembly.Location);
            using SHA256 sha256 = SHA256.Create();
            byte[] actualHashBytes = sha256.ComputeHash(assemblyBytes);
            string actualHash = Convert.ToBase64String(actualHashBytes);

            // Compare with expected hash
            bool isValid = actualHash.Equals(ExpectedHash, StringComparison.Ordinal);

            if (!isValid)
            {
                // Log security event (visible in debug output)
                System.Diagnostics.Debug.WriteLine("============================================================");
                System.Diagnostics.Debug.WriteLine("[SECURITY ALERT] Assembly integrity check FAILED!");
                System.Diagnostics.Debug.WriteLine($"Expected hash: {ExpectedHash}");
                System.Diagnostics.Debug.WriteLine($"Actual hash:   {actualHash}");
                System.Diagnostics.Debug.WriteLine($"Assembly:      {assembly.Location}");
                System.Diagnostics.Debug.WriteLine("============================================================");

                if (throwOnFailure)
                {
                    throw new SecurityException(
                        "BuildingBlock assembly integrity check FAILED! " +
                        "The DLL may have been modified by an attacker. " +
                        "Please reinstall from official NuGet source.");
                }
            }

            _cachedResult = isValid;
            return isValid;
        }
        catch (SecurityException)
        {
            // Re-throw security exceptions
            throw;
        }
        catch (Exception ex)
        {
            // Log error but don't fail (fail-open for compatibility)
            System.Diagnostics.Debug.WriteLine($"[Security] Error during integrity check: {ex.Message}");
            _cachedResult = true; // Fail-open: allow execution if check fails
            return true;
        }
    }

    /// <summary>
    /// Gets the current assembly's SHA256 hash.
    /// Useful for generating the expected hash during build time.
    /// </summary>
    /// <returns>Base64-encoded SHA256 hash of the assembly.</returns>
    public static string? CalculateCurrentHash()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            if (string.IsNullOrEmpty(assembly.Location))
            {
                return null;
            }

            byte[] assemblyBytes = File.ReadAllBytes(assembly.Location);
            using SHA256 sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(assemblyBytes);
            return Convert.ToBase64String(hashBytes);
        }
        catch
        {
            return null;
        }
    }
}
