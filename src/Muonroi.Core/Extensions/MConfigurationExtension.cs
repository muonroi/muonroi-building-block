using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IConfiguration"/> and <see cref="IServiceCollection"/> for configuration management.
/// </summary>
public static class MConfigurationExtension
{
    private const string ConfigKey = "SecretKey";
    private const string EnableEncryptionKey = "EnableEncryption";

    /// <summary>
    /// Gets options of type <typeparamref name="T"/> from the specified configuration section.
    /// </summary>
    /// <typeparam name="T">The type of the options.</typeparam>
    /// <param name="configuration">The configuration to get options from.</param>
    /// <param name="section">The name of the configuration section.</param>
    /// <returns>An instance of <typeparamref name="T"/> populated with configuration values.</returns>
    public static T GetOptions<T>(this IConfiguration configuration, string section)
        where T : new()
    {
        MGuard.NotNull(configuration);
        MGuard.NotEmpty(section);

        T model = new();
        try
        {
            configuration.GetSection(section).Bind(model);
        }
        catch
        {
            // ignore binding errors and return default model
        }

        return model;
    }

    /// <summary>
    /// Configures a dictionary of options from the specified configuration section.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options dictionary.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
    /// <param name="section">The configuration section containing the dictionary values.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection ConfigureDictionary<TOptions>(this IServiceCollection services,
        IConfigurationSection section) where TOptions : class, IDictionary<string, string>
    {
        List<IConfigurationSection> values = [.. section.GetChildren()];
        HashSet<string> keys = [];
        foreach (IConfigurationSection? v in values.Where(v => !keys.Add(v.Key)))
        {
            MGuard.Against(true,
                $"An item with the same key has already been added. Key: {section.Path}:{v.Key}");
        }

        services.Configure<TOptions>(x =>
            values.ForEach(v => { x.Add(v.Key, v.Value ?? string.Empty); }));

        return services;
    }

    /// <summary>
    /// Configures startup configuration of type <typeparamref name="TConfig"/>.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
    /// <param name="configuration">The configuration to bind to the <typeparamref name="TConfig"/>.</param>
    /// <returns>An instance of <typeparamref name="TConfig"/> populated with configuration values.</returns>
    public static TConfig ConfigureStartupConfig<TConfig>(this IServiceCollection services,
        IConfiguration configuration) where TConfig : class, new()
    {
        MGuard.NotNull(services);
        MGuard.NotNull(configuration);

        TConfig config = new();
        try
        {
            configuration.Bind(config);
        }
        catch
        {
            // Ignore conversion errors and use default values
        }

        return config;
    }

    /// <summary>
    /// Gets a configuration value, optionally decrypting it if encryption is enabled.
    /// </summary>
    /// <param name="configuration">The configuration to get the value from.</param>
    /// <param name="keyOfConfig">The key of the configuration value.</param>
    /// <returns>The configuration value, decrypted if necessary, or an empty string if not found.</returns>
    public static string GetConfigHelper(this IConfiguration configuration, string keyOfConfig)
    {
        bool enableEncryption = configuration.GetValue(EnableEncryptionKey, false);
        string? configValue;
        if (enableEncryption)
        {
            try
            {
                string? secretKeyOpt = configuration.GetCryptConfigValue(ConfigKey);
                string secretKey = MGuard.Configured(secretKeyOpt, ConfigKey);

                configValue = configuration.GetCryptConfigValue(keyOfConfig, secretKey);
            }
            catch (CryptographicException)
            {
                configValue = configuration[keyOfConfig];
            }
        }
        else
        {
            configValue = configuration[keyOfConfig];
        }

        return configValue ?? string.Empty;
    }

    /// <summary>
    /// Gets a cryptographic configuration value for the specified key.
    /// </summary>
    /// <param name="configuration">The configuration to get the value from.</param>
    /// <param name="configKey">The key of the configuration value.</param>
    /// <returns>The configuration value, or null if not found.</returns>
    public static string? GetCryptConfigValue(this IConfiguration configuration, string configKey)
    {
        MGuard.NotEmpty(configKey);
        return configuration[configKey];
    }

    /// <summary>
    /// Gets a cryptographic configuration value and decrypts it using the provided secret key.
    /// </summary>
    /// <param name="configuration">The configuration to get the value from.</param>
    /// <param name="configKey">The key of the configuration value.</param>
    /// <param name="secretKey">The secret key for decryption.</param>
    /// <param name="fingerprintSalt">The fingerprint salt for decryption.</param>
    /// <returns>The decrypted configuration value, or the original cipher text if encryption is disabled.</returns>
    public static string? GetCryptConfigValue(this IConfiguration configuration, string configKey, string secretKey, string fingerprintSalt = "")
    {
        MGuard.NotEmpty(configKey);

        string? cipherText = configuration.GetCryptConfigValue(configKey);
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        bool enableEncryption = configuration.GetValue(EnableEncryptionKey, false);
        if (!enableEncryption)
        {
            return cipherText;
        }

        MGuard.NotEmpty(secretKey);

        return MCryptographyExtension.Decrypt(secretKey, cipherText, fingerprintSalt);
    }

    /// <summary>
    /// Gets a cryptographic configuration value and decrypts it, optionally using a configured secret key.
    /// </summary>
    /// <param name="configuration">The configuration to get the value from.</param>
    /// <param name="configKey">The key of the configuration value.</param>
    /// <param name="useConfiguredSecretKey">True to use the secret key from configuration; otherwise, false.</param>
    /// <param name="secretKey">The secret key for decryption if <paramref name="useConfiguredSecretKey"/> is false.</param>
    /// <param name="fingerprintSalt">The fingerprint salt for decryption.</param>
    /// <returns>The decrypted configuration value, or the original cipher text if encryption is disabled.</returns>
    public static string? GetCryptConfigValue(this IConfiguration configuration, string configKey, bool useConfiguredSecretKey, string secretKey, string fingerprintSalt = "")
    {
        MGuard.NotEmpty(configKey);

        string? cipherText = configuration.GetCryptConfigValue(configKey);
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        bool enableEncryption = configuration.GetValue(EnableEncryptionKey, false);
        if (!enableEncryption)
        {
            return cipherText;
        }

        if (useConfiguredSecretKey)
        {
            secretKey = configuration.GetCryptConfigValue(ConfigKey) ?? string.Empty;
            MGuard.NotEmpty(secretKey);
        }
        else
        {
            MGuard.NotEmpty(secretKey);
        }

        return MCryptographyExtension.Decrypt(secretKey, cipherText, fingerprintSalt);
    }

    /// <summary>
    /// Decrypts the provided cipher text using the configured secret key.
    /// </summary>
    /// <param name="configuration">The configuration to get the secret key from.</param>
    /// <param name="cipherText">The cipher text to decrypt.</param>
    /// <param name="fingerprintSalt">The fingerprint salt for decryption.</param>
    /// <returns>The decrypted text, or the original cipher text if encryption is disabled.</returns>
    public static string? GetCryptConfigValueCipherText(this IConfiguration configuration, string? cipherText, string fingerprintSalt = "")
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        bool enableEncryption = configuration.GetValue(EnableEncryptionKey, false);
        if (!enableEncryption)
        {
            return cipherText;
        }

        string secretKey = configuration.GetCryptConfigValue(ConfigKey) ?? string.Empty;
        MGuard.NotEmpty(secretKey);

        if (string.IsNullOrEmpty(fingerprintSalt))
        {
            fingerprintSalt = configuration.GetValue<string>("LicenseConfigs:ProjectSeed") ?? string.Empty;
        }

        return MCryptographyExtension.Decrypt(secretKey, cipherText, fingerprintSalt);
    }
}
