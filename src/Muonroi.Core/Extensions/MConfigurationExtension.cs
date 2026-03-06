namespace Muonroi.Core.Extensions;

public static class MConfigurationExtension
{
    private const string ConfigKey = "SecretKey";
    private const string EnableEncryptionKey = "EnableEncryption";
    private const string MessageException = "Value cannot be null or empty.";

    public static T GetOptions<T>(this IConfiguration configuration, string section)
        where T : new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrEmpty(section))
        {
            throw new ArgumentNullException(nameof(section));
        }

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

    public static IServiceCollection ConfigureDictionary<TOptions>(this IServiceCollection services,
        IConfigurationSection section) where TOptions : class, IDictionary<string, string>
    {
        List<IConfigurationSection> values = [.. section.GetChildren()];
        HashSet<string> keys = [];
        foreach (IConfigurationSection? v in values.Where(v => !keys.Add(v.Key)))
        {
            throw new ArgumentException(
                $"An item with the same key has already been added. Key: {section.Path}:{v.Key}");
        }

        services.Configure<TOptions>(x =>
            values.ForEach(v => { x.Add(v.Key, v.Value ?? string.Empty); }));

        return services;
    }

    public static TConfig ConfigureStartupConfig<TConfig>(this IServiceCollection services,
        IConfiguration configuration) where TConfig : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

    public static string GetConfigHelper(this IConfiguration configuration, string keyOfConfig)
    {
        bool enableEncryption = configuration.GetValue(EnableEncryptionKey, false);
        string? configValue;
        if (enableEncryption)
        {
            try
            {
                string? secretKey = configuration.GetCryptConfigValue(ConfigKey);
                if (string.IsNullOrEmpty(secretKey))
                {
                    throw new InvalidOperationException("SecretKey cannot be an empty string");
                }

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

    public static string? GetCryptConfigValue(this IConfiguration configuration, string configKey)
    {
        return string.IsNullOrEmpty(configKey)
            ? throw new ArgumentException(MessageException, nameof(configKey))
            : configuration[configKey];
    }

    public static string? GetCryptConfigValue(this IConfiguration configuration, string configKey, string secretKey, string fingerprintSalt = "")
    {
        if (string.IsNullOrEmpty(configKey))
        {
            throw new ArgumentException(MessageException, nameof(configKey));
        }

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

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new ArgumentException(MessageException, nameof(secretKey));
        }

        return MCryptographyExtension.Decrypt(secretKey, cipherText, fingerprintSalt);
    }

    public static string? GetCryptConfigValue(this IConfiguration configuration, string configKey, bool useConfiguredSecretKey, string secretKey, string fingerprintSalt = "")
    {
        if (string.IsNullOrEmpty(configKey))
        {
            throw new ArgumentException(MessageException, nameof(configKey));
        }

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
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new ArgumentException(MessageException, nameof(secretKey));
            }
        }
        else if (string.IsNullOrEmpty(secretKey))
        {
            throw new ArgumentException(MessageException, nameof(secretKey));
        }

        return MCryptographyExtension.Decrypt(secretKey, cipherText, fingerprintSalt);
    }

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
        ArgumentException.ThrowIfNullOrEmpty(secretKey);

        if (string.IsNullOrEmpty(fingerprintSalt))
        {
            fingerprintSalt = configuration.GetValue<string>("LicenseConfigs:ProjectSeed") ?? string.Empty;
        }

        return MCryptographyExtension.Decrypt(secretKey, cipherText, fingerprintSalt);
    }
}
