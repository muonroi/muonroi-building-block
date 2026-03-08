using Muonroi.Core.Extensions;

namespace Muonroi.Core.Helpers;

/// <summary>
/// Implements <see cref="IGuidGenerator"/> by creating sequential Guids.
/// This code is taken from https://github.com/jhtodd/SequentialGuid/blob/master/SequentialGuid/Classes/SequentialGuid.cs
/// </summary>
public class MSequentialGuidGenerator : IGuidGenerator
{
    /// <summary>
    /// Gets the singleton <see cref="MSequentialGuidGenerator"/> instance.
    /// </summary>
    public static MSequentialGuidGenerator Instance { get; } = new();

    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    /// <summary>
    /// Gets or sets the database type for generating sequential GUIDs.
    /// </summary>
    public SequentialGuidDatabaseType DatabaseType { get; set; }

    /// <summary>
    /// Prevents a default instance of the <see cref="MSequentialGuidGenerator"/> class from being created.
    /// Use <see cref="Instance"/>.
    /// </summary>
    private MSequentialGuidGenerator()
    {
        DatabaseType = SequentialGuidDatabaseType.SqlServer;
    }

    /// <summary>
    /// Creates a new sequential GUID based on the configured <see cref="DatabaseType"/>.
    /// </summary>
    /// <returns>A new sequential GUID.</returns>
    public Guid Create()
    {
        return Create(DatabaseType);
    }

    /// <summary>
    /// Creates a new sequential GUID for the specified database type.
    /// </summary>
    /// <param name="databaseType">The database type to generate the GUID for.</param>
    /// <returns>A new sequential GUID.</returns>
    public static Guid Create(SequentialGuidDatabaseType databaseType)
    {
        return databaseType switch
        {
            SequentialGuidDatabaseType.SqlServer => Create(SequentialGuidType.SequentialAtEnd),
            SequentialGuidDatabaseType.Oracle => Create(SequentialGuidType.SequentialAsBinary),
            SequentialGuidDatabaseType.MySql => Create(SequentialGuidType.SequentialAsString),
            SequentialGuidDatabaseType.PostgreSql => Create(SequentialGuidType.SequentialAsString),
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>
    /// Creates a new sequential GUID for the specified <see cref="SequentialGuidType"/>.
    /// </summary>
    /// <param name="guidType">The type of sequential GUID to generate.</param>
    /// <returns>A new sequential GUID.</returns>
    public static Guid Create(SequentialGuidType guidType)
    {
        // We start with 16 bytes of cryptographically strong random data.
        byte[] randomBytes = new byte[10];
        _rng.Locking(r => r.GetBytes(randomBytes));

        // Now we have the random basis for our GUID.  Next, we need to
        // create the six-byte block which will be our timestamp.

        long timestamp = DateTime.UtcNow.Ticks / 10000L; // MBB001-exempt: static-class boundary

        // Then get the bytes
        byte[] timestampBytes = BitConverter.GetBytes(timestamp);

        // Since we're converting from an Int64, we have to reverse on
        // little-endian systems.
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes);
        }

        byte[] guidBytes = new byte[16];

        switch (guidType)
        {
            case SequentialGuidType.SequentialAsString:
            case SequentialGuidType.SequentialAsBinary:

                // For string and byte-array version, we copy the timestamp first, followed
                // by the random data.
                Buffer.BlockCopy(timestampBytes, 2, guidBytes, 0, 6);
                Buffer.BlockCopy(randomBytes, 0, guidBytes, 6, 10);

                // If formatting as a string, we have to compensate for the fact
                // that .NET regards the Data1 and Data2 block as an Int32 and an Int16,
                // respectively.  That means that it switches the order on little-endian
                // systems.  So again, we have to reverse.
                if (guidType == SequentialGuidType.SequentialAsString && BitConverter.IsLittleEndian)
                {
                    Array.Reverse(guidBytes, 0, 4);
                    Array.Reverse(guidBytes, 4, 2);
                }

                break;

            case SequentialGuidType.SequentialAtEnd:

                // For sequential-at-the-end versions, we copy the random data first,
                // followed by the timestamp.
                Buffer.BlockCopy(randomBytes, 0, guidBytes, 0, 10);
                Buffer.BlockCopy(timestampBytes, 2, guidBytes, 10, 6);
                break;
        }

        return new Guid(guidBytes);
    }

    /// <summary>
    /// Database type to generate GUIDs.
    /// </summary>
    public enum SequentialGuidDatabaseType
    {
        /// <summary>
        /// SQL Server.
        /// </summary>
        SqlServer,

        /// <summary>
        /// Oracle.
        /// </summary>
        Oracle,

        /// <summary>
        /// MySQL.
        /// </summary>
        MySql,

        /// <summary>
        /// PostgreSQL.
        /// </summary>
        PostgreSql
    }

    /// <summary>
    /// Describes the type of a sequential GUID value.
    /// </summary>
    public enum SequentialGuidType
    {
        /// <summary>
        /// The GUID should be sequential when formatted using the
        /// <see cref="Guid.ToString()" /> method.
        /// </summary>
        SequentialAsString,

        /// <summary>
        /// The GUID should be sequential when formatted using the
        /// <see cref="Guid.ToByteArray" /> method.
        /// </summary>
        SequentialAsBinary,

        /// <summary>
        /// The sequential portion of the GUID should be located at the end
        /// of the Data4 block.
        /// </summary>
        SequentialAtEnd
    }
}
