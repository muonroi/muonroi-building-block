


namespace Muonroi.BuildingBlock.Test
{
    public class MOidcConfigTests
    {
        [Fact]
        public void ClientId_Getter_Returns_Value_Or_Empty()
        {
            MOidcConfig cfg = new()
            {
                ClientId = "id"
            };
            Assert.Equal("id", cfg.ClientId);

            MOidcConfig cfg2 = new();
            Assert.Equal(string.Empty, cfg2.ClientId);
        }

        [Fact]
        public void ClientSecret_Getter_Returns_Value_Or_Empty()
        {
            MOidcConfig cfg = new()
            {
                ClientSecret = "sec"
            };
            Assert.Equal("sec", cfg.ClientSecret);

            MOidcConfig cfg2 = new();
            Assert.Equal(string.Empty, cfg2.ClientSecret);
        }

        [Fact]
        public void Scopes_Getter_Returns_Value_Or_Empty()
        {
            MOidcConfig cfg = new()
            {
                Scopes = ["s1", "s2"]
            };
            string[] expected = ["s1", "s2"];
            Assert.Equal(expected, cfg.Scopes);

            MOidcConfig cfg2 = new();
            Assert.Empty(cfg2.Scopes);
        }

        [Fact]
        public void CallbackPath_Getter_Returns_Value_Or_Default()
        {
            MOidcConfig cfg = new()
            {
                CallbackPath = "/cb"
            };
            Assert.Equal("/cb", cfg.CallbackPath);

            MOidcConfig cfg2 = new();
            Assert.Equal("/signin-oidc", cfg2.CallbackPath);
        }

        [Fact]
        public void Authority_Returns_Value_Or_Empty()
        {
            MOidcConfig cfg = new();
            Assert.Equal(string.Empty, cfg.Authority);
            cfg.Authority = "https://example";
            Assert.Equal("https://example", cfg.Authority);
        }
    }
}
