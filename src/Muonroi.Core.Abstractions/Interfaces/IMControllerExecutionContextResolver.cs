using Microsoft.AspNetCore.Http;
using Muonroi.Core.Abstractions.Models;

namespace Muonroi.Core.Abstractions.Interfaces;

public interface IMControllerExecutionContextResolver
{
    MControllerExecutionContext Resolve(HttpContext httpContext);
}
