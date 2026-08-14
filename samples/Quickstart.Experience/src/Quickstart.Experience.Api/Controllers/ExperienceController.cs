using Microsoft.AspNetCore.Mvc;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime.Extraction;

namespace Quickstart.Experience.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperienceController : ControllerBase
{
    private readonly IExperienceStore _store;
    private readonly IExperienceBrain _brain;
    private readonly MistakeDetector _mistakeDetector;

    public ExperienceController(IExperienceStore store, IExperienceBrain brain, MistakeDetector mistakeDetector)
    {
        _store = store;
        _brain = brain;
        _mistakeDetector = mistakeDetector;
    }

    [HttpPost("extract")]
    public async Task<IActionResult> ExtractExperience([FromBody] string sessionLog)
    {
        // Use MistakeDetector to find errors
        var mistakes = _mistakeDetector.Detect(sessionLog);
        if (!mistakes.Any())
        {
            return Ok(new { message = "No mistakes found in session." });
        }

        // Use Brain to extract a structured experience
        var experiences = await _brain.ExtractAsync(sessionLog);

        foreach (var exp in experiences)
        {
            await _store.StoreAsync(exp);
        }

        return Ok(experiences);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var results = await _store.FindRelevantAsync(query);
        return Ok(results);
    }
}
