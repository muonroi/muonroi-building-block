namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class TraceRedactorTests
{
    private static DefaultTraceRedactor CreateDefaultRedactor(Action<RuleTracingOptions>? configure = null)
    {
        RuleTracingOptions opts = new();
        configure?.Invoke(opts);
        return new DefaultTraceRedactor(Options.Create(opts));
    }

    // Test 1: DefaultTraceRedactor replaces deny-listed JSON field values with "***REDACTED***" in InputFactsJson
    [Fact]
    public void Redact_ReplacesDenyListedField_InInputFactsJson()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            InputFactsJson = """{"username":"john","password":"secret123","orderId":"O-001"}"""
        };

        RuleTraceEntry result = sut.Redact(entry);

        using JsonDocument doc = JsonDocument.Parse(result.InputFactsJson!);
        doc.RootElement.GetProperty("password").GetString().Should().Be("***REDACTED***");
        doc.RootElement.GetProperty("username").GetString().Should().Be("john");
        doc.RootElement.GetProperty("orderId").GetString().Should().Be("O-001");
    }

    // Test 2: DefaultTraceRedactor replaces deny-listed JSON field values with "***REDACTED***" in OutputFactsJson
    [Fact]
    public void Redact_ReplacesDenyListedField_InOutputFactsJson()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            OutputFactsJson = """{"result":"approved","creditCard":"4111-1111-1111-1111","amount":500}"""
        };

        RuleTraceEntry result = sut.Redact(entry);

        using JsonDocument doc = JsonDocument.Parse(result.OutputFactsJson!);
        doc.RootElement.GetProperty("creditCard").GetString().Should().Be("***REDACTED***");
        doc.RootElement.GetProperty("result").GetString().Should().Be("approved");
        doc.RootElement.GetProperty("amount").GetDouble().Should().Be(500);
    }

    // Test 3: DefaultTraceRedactor leaves non-deny-listed fields untouched
    [Fact]
    public void Redact_LeavesNonDenyListedFields_Unchanged()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        string inputJson = """{"orderId":"O-001","amount":250.0,"status":"pending"}""";
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            InputFactsJson = inputJson
        };

        RuleTraceEntry result = sut.Redact(entry);

        using JsonDocument doc = JsonDocument.Parse(result.InputFactsJson!);
        doc.RootElement.GetProperty("orderId").GetString().Should().Be("O-001");
        doc.RootElement.GetProperty("amount").GetDouble().Should().Be(250.0);
        doc.RootElement.GetProperty("status").GetString().Should().Be("pending");
    }

    // Test 4: DefaultTraceRedactor handles null InputFactsJson/OutputFactsJson gracefully
    [Fact]
    public void Redact_WithNullJsonFields_ReturnsEntryUnchanged()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            InputFactsJson = null,
            OutputFactsJson = null
        };

        RuleTraceEntry result = sut.Redact(entry);

        result.InputFactsJson.Should().BeNull();
        result.OutputFactsJson.Should().BeNull();
    }

    // Test 5: Empty JSON object returns entry unchanged (no fields to redact)
    [Fact]
    public void Redact_WithEmptyJsonObject_ReturnsEntryUnchanged()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            InputFactsJson = "{}",
            OutputFactsJson = "{}"
        };

        RuleTraceEntry result = sut.Redact(entry);

        result.InputFactsJson.Should().Be("{}");
        result.OutputFactsJson.Should().Be("{}");
    }

    // Test 6: Nested objects — only top-level fields are redacted
    [Fact]
    public void Redact_WithNestedJson_OnlyRedactsTopLevelDenyListedFields()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        // The nested "password" inside "user" should NOT be redacted (top-level only scope)
        // But the top-level "token" SHOULD be redacted
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            InputFactsJson = """{"token":"abc.def.ghi","user":{"name":"john","password":"nested-secret"}}"""
        };

        RuleTraceEntry result = sut.Redact(entry);

        using JsonDocument doc = JsonDocument.Parse(result.InputFactsJson!);
        doc.RootElement.GetProperty("token").GetString().Should().Be("***REDACTED***");
        // Nested password is preserved (out of scope for top-level-only redaction)
        doc.RootElement.GetProperty("user").GetProperty("name").GetString().Should().Be("john");
    }

    // Test 7: Per-tenant TTL dictionary resolves correctly
    [Fact]
    public void TenantRetentionOverrides_ResolvesCorrectTtl_ForKnownTenant()
    {
        RuleTracingOptions opts = new()
        {
            TenantRetentionOverrides = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                ["acme"] = TimeSpan.FromHours(72)
            }
        };

        opts.TenantRetentionOverrides.TryGetValue("acme", out TimeSpan acmeTtl).Should().BeTrue();
        acmeTtl.Should().Be(TimeSpan.FromHours(72));

        opts.TenantRetentionOverrides.TryGetValue("unknown-tenant", out _).Should().BeFalse();
    }

    // Test 8: Case-insensitive field name matching
    [Fact]
    public void Redact_WithMixedCaseDenyListedField_RedactsRegardlessOfCase()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            InputFactsJson = """{"Password":"MyPass","SSN":"123-45-6789"}"""
        };

        RuleTraceEntry result = sut.Redact(entry);

        using JsonDocument doc = JsonDocument.Parse(result.InputFactsJson!);
        doc.RootElement.GetProperty("Password").GetString().Should().Be("***REDACTED***");
        doc.RootElement.GetProperty("SSN").GetString().Should().Be("***REDACTED***");
    }

    // Test 9: Original entry is not mutated (record immutability)
    [Fact]
    public void Redact_DoesNotMutateOriginalEntry()
    {
        DefaultTraceRedactor sut = CreateDefaultRedactor();
        const string originalJson = """{"password":"secret","orderId":"O-001"}""";
        RuleTraceEntry entry = new()
        {
            TenantId = "tenant-a",
            InputFactsJson = originalJson
        };

        RuleTraceEntry result = sut.Redact(entry);

        entry.InputFactsJson.Should().Be(originalJson);
        result.Should().NotBeSameAs(entry);
    }
}
