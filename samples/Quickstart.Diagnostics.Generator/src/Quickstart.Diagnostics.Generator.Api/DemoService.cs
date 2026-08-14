// Since the generator expects Muonroi.Core.Abstractions.Diagnostics.MTraceableAttribute, we define it here if the package isn't referenced directly
namespace Muonroi.Core.Abstractions.Diagnostics
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MTraceableAttribute : Attribute { }

    public enum MTraceNodeType { Custom }

    public static class Context
    {
        public static class MTraceContextHolder
        {
            public static CurrentValue Current { get; } = new CurrentValue();
        }

        public class CurrentValue
        {
            public MTraceContext? Value { get; set; } = new MTraceContext();
        }

        public class MTraceContext
        {
            public IDisposable BeginNode(string name, MTraceNodeType type)
            {
                Console.WriteLine($"[TRACE START] {name}");
                return new TraceScope(name);
            }
        }

        public class TraceScope : IDisposable
        {
            private readonly string _name;
            public TraceScope(string name) { _name = name; }
            public void Dispose() => Console.WriteLine($"[TRACE END] {_name}");
        }
    }
}

namespace Quickstart.Diagnostics.Generator.Api
{
    public partial class DemoService
    {
        [Muonroi.Core.Abstractions.Diagnostics.MTraceable]
        public void DoHeavyWork()
        {
            Console.WriteLine("Doing heavy work...");
        }
    }
}

namespace Quickstart.Diagnostics.Generator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TraceController : ControllerBase
    {
        private readonly DemoService _service;

        public TraceController(DemoService service)
        {
            _service = service;
        }

        [HttpPost("run")]
        public IActionResult RunTraced()
        {
            // Call the generated wrapper method
            _service.DoHeavyWork_TraceWrapper();
            return Ok("Executed traced method. Check console output.");
        }
    }
}
