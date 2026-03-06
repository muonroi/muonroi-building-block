namespace Muonroi.BuildingBlock.Test;

public class MCryptographyExtensionTests
{
    [Fact]
    public void GenerateHmacSha512_ReturnsExpectedHash()
    {
        string key = "secret";
        string data = "message";
        string expected;
        using (HMACSHA512 hmac = new(Encoding.UTF8.GetBytes(key)))
        {
            expected = string.Concat(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)).Select(b => b.ToString("x2")));
        }

        string actual = MCryptographyExtension.GenerateHmacSha512(key, data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GenerateHmacSha512_NullInput_Throws()
    {
        string key = "secret";
        Assert.Throws<ArgumentNullException>(() => MCryptographyExtension.GenerateHmacSha512(key, null!));
    }

    [Fact]
    public void GenerateHmacSha512_WrongKey_ProducesDifferentHash()
    {
        string key1 = "key1";
        string key2 = "key2";
        string data = "message";
        string hash1 = MCryptographyExtension.GenerateHmacSha512(key1, data);
        string hash2 = MCryptographyExtension.GenerateHmacSha512(key2, data);
        Assert.NotEqual(hash1, hash2);
    }

    // --- MD5Hash ---
    [Fact]
    public void MD5Hash_ReturnsExpectedHash()
    {
        string data = "input";
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        string expected = Convert.ToHexString(hashBytes).ToLowerInvariant();
        string actual = MCryptographyExtension.Md5Hash(data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MD5Hash_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MCryptographyExtension.Md5Hash(null!));
    }

    [Fact]
    public void MD5Hash_LargeInput_Succeeds()
    {
        string data = new('a', 10000);
        string result = MCryptographyExtension.Md5Hash(data);
        Assert.False(string.IsNullOrEmpty(result));
    }

    // --- EncryptMd5 ---
    [Fact]
    public void EncryptMd5_ReturnsExpectedHash()
    {
        string data = "text";
        byte[] bytes = Encoding.Unicode.GetBytes(data);
        byte[] hash = SHA256.HashData(bytes);
        string expected = Convert.ToBase64String(hash);
        string actual = MCryptographyExtension.EncryptMd5(data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EncryptMd5_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MCryptographyExtension.EncryptMd5(null!));
    }

    [Fact]
    public void EncryptMd5_LargeInput_Succeeds()
    {
        string data = new('b', 10000);
        string result = MCryptographyExtension.EncryptMd5(data);
        Assert.False(string.IsNullOrEmpty(result));
    }

    // --- Sha256 ---
    [Fact]
    public void Sha256_ReturnsExpectedHash()
    {
        string data = "abc";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        string expected = string.Concat(hash.Select(b => b.ToString("x2")));
        string actual = MCryptographyExtension.Sha256(data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Sha256_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MCryptographyExtension.Sha256(null!));
    }

    [Fact]
    public void Sha256_LargeInput_Succeeds()
    {
        string data = new('c', 10000);
        string result = MCryptographyExtension.Sha256(data);
        Assert.Equal(64, result.Length);
    }

    // --- EncryptMd5Sha256WithSalt ---
    [Fact]
    public void EncryptMd5Sha256WithSalt_ReturnsExpectedHash()
    {
        string data = "password";
        string salt = "salt";
        string expected = MCryptographyExtension.Sha256(MCryptographyExtension.EncryptMd5(data.Trim()) + salt);
        string actual = MCryptographyExtension.EncryptMd5Sha256WithSalt(data, salt);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EncryptMd5Sha256WithSalt_NullData_Throws()
    {
        Assert.Throws<NullReferenceException>(() => MCryptographyExtension.EncryptMd5Sha256WithSalt(null!, "salt"));
    }

    [Fact]
    public void EncryptMd5Sha256WithSalt_DifferentSalt_ProducesDifferentHash()
    {
        string data = "password";
        string salt1 = "salt1";
        string salt2 = "salt2";
        string hash1 = MCryptographyExtension.EncryptMd5Sha256WithSalt(data, salt1);
        string hash2 = MCryptographyExtension.EncryptMd5Sha256WithSalt(data, salt2);
        Assert.NotEqual(hash1, hash2);
    }

    // --- GenerateSha256String ---
    [Fact]
    public void GenerateSha256String_Returns_Correct_Hash()
    {
        string hash = MCryptographyExtension.GenerateSha256String("abc");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void GenerateSha256String_Null_Input_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MCryptographyExtension.GenerateSha256String(null!));
    }

    [Fact]
    public void GenerateSha256String_Empty_String_Works()
    {
        string hash = MCryptographyExtension.GenerateSha256String(string.Empty);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [Fact]
    public void GenerateSha256String_Large_Input_Works()
    {
        string input = new('a', 1024 * 1024);
        string hash = MCryptographyExtension.GenerateSha256String(input);
        Assert.Equal(64, hash.Length);
    }
}
