// Polyfill required for 'record' and 'init' property support on netstandard2.0.
// The compiler uses this internal marker type when lowering records and init-only setters.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
