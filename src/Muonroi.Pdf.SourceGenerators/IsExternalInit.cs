// Polyfill required for C# 9+ init-only setters and records when targeting netstandard2.0.
// The compiler emits a reference to this type; netstandard2.1+ includes it in the BCL.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
