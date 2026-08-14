namespace Muonroi.Rules.Tests;

[Collection("NonParallel")]
public class HmacSha256RuleSetSignerTests
{
    [Fact]
    public void Sign_And_Verify_RoundTrip_Succeeds()
    {
        byte[] key = Encoding.UTF8.GetBytes("super-secret");
        HmacSha256RuleSetSigner signer = new(key);

        const string content = "{\"WorkflowName\":\"wf\"}";
        string signature = signer.Sign(content);

        Assert.False(string.IsNullOrWhiteSpace(signature));
        Assert.True(signer.Verify(content, signature));
    }

    [Fact]
    public void Verify_Fails_For_Tampered_Content()
    {
        byte[] key = Encoding.UTF8.GetBytes("another-secret");
        HmacSha256RuleSetSigner signer = new(key);

        const string original = "{\"Rules\":[\"A\"]}";
        string signature = signer.Sign(original);

        bool result = signer.Verify("{\"Rules\":[\"B\"]}", signature);

        Assert.False(result);
    }

    [Fact]
    public void Signatures_Differ_For_Different_Content()
    {
        byte[] key = Encoding.UTF8.GetBytes("key");
        HmacSha256RuleSetSigner signer = new(key);

        string sig1 = signer.Sign("content-1");
        string sig2 = signer.Sign("content-2");

        Assert.NotEqual(sig1, sig2);
    }
}
