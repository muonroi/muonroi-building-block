

namespace Muonroi.Auth.Keys;

[ApiController]
[Route(".well-known/jsonWebKeySet.json")]
public class JsonWebKeySetController(JwtService jwtService) : ControllerBase
{
    [HttpGet]
    public JsonWebKeySet Get()
    {
        return jwtService.GetJsonWebKeySet();
    }
}