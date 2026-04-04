using System.Collections.Generic;

namespace Muonroi.RuleEngine.SourceGenerators.Models;

internal sealed record ParameterModel(
    string Name,
    string TypeName,
    bool HasDefaultValue,
    string? DefaultValueExpression);

internal sealed record ServiceDependency(
    string TypeName,
    string FieldName,
    string ConstructorParameterName);

internal sealed record HelperMethodDefinition(
    string MethodName,
    string ReturnType,
    IReadOnlyList<ParameterModel> Parameters,
    string? MethodBody,
    string? ExpressionBody,
    bool IsAsync);

internal sealed record ExtractedRuleDefinition(
    string Code,
    string MethodName,
    string ClassName,
    string? SourceNamespace,
    string OutputNamespace,
    string ContextType,
    string ReturnType,
    int Order,
    string HookPoint,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> Usings,
    IReadOnlyList<string> CustomAttributes,
    IReadOnlyList<ParameterModel> Parameters,
    IReadOnlyList<ServiceDependency> Dependencies,
    IReadOnlyList<HelperMethodDefinition> HelperMethods,
    string? DocumentationComment,
    string? MethodBody,
    string? ExpressionBody,
    string? FeelExpression,
    bool IsAsync,
    string SourceFile,
    int SourceLine,
    bool UseFactBagAware = false);
