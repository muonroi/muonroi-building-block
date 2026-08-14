namespace Quickstart.Data.EntityFrameworkCore.Api.Controllers;

/// <summary>
/// Exercises an <see cref="Muonroi.Data.EntityFrameworkCore.Entity.MDbContext"/>-derived context (<see cref="SampleNotesDbContext"/>):
/// audit timestamping on insert and soft-delete on remove.
///
/// MEntity carries IsDeleted/DeletionTime; MDbContext.SaveChangesAsync() converts an
/// EF Delete into a soft-delete (IsDeleted=true, state Modified). The package's
/// MRepository&lt;T&gt;.Queryable filters out deleted rows with Where(m =&gt; !m.IsDeleted);
/// this controller applies that same predicate explicitly so the behaviour is visible
/// without wiring the full repository (which requires the license guard + auth context).
/// </summary>
[ApiController]
[Route("api/notes")]
public class NotesController(SampleNotesDbContext db) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. Create — audit fields stamped automatically by MDbContext.SaveChangesAsync
    //    POST /api/notes
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request, CancellationToken token)
    {
        var note = new Note { Title = request.Title, Body = request.Body };

        await db.Notes.AddAsync(note, token);

        // SaveChangesAsync stamps CreationTime + CreatorUserId via MDbContext.UpdateTimestamps.
        await db.SaveChangesAsync(token);

        return Ok(new
        {
            note.Id,
            note.EntityId,
            note.Title,
            note.CreationTime,   // stamped by MDbContext
            note.IsDeleted       // false
        });
    }

    // ---------------------------------------------------------------------------
    // 2. List — only non-deleted rows
    //    GET /api/notes
    // ---------------------------------------------------------------------------
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken token)
    {
        List<Note> notes = await db.Notes
            .Where(n => !n.IsDeleted) // same predicate as MRepository<T>.Queryable
            .OrderByDescending(n => n.CreationTime)
            .ToListAsync(token);

        return Ok(notes.Select(n => new { n.Id, n.Title, n.Body, n.CreationTime }));
    }

    // ---------------------------------------------------------------------------
    // 3. Soft-delete — DELETE becomes UPDATE IsDeleted=true
    //    DELETE /api/notes/{id}
    // ---------------------------------------------------------------------------
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken token)
    {
        Note? note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, token);
        if (note is null)
        {
            return NotFound(new { message = $"Note {id} not found." });
        }

        // Remove() marks the entity Deleted; MDbContext.SaveChangesAsync converts it
        // to a soft-delete (IsDeleted=true, DeletionTime set) instead of a physical row delete.
        db.Notes.Remove(note);
        await db.SaveChangesAsync(token);

        return Ok(new
        {
            note.Id,
            note.IsDeleted,      // true
            note.DeletionTime,   // stamped
            note = "Row is retained; subsequent GET /api/notes excludes it via the !IsDeleted filter."
        });
    }
}

/// <summary>Request body for creating a note.</summary>
public record CreateNoteRequest(string Title, string Body);
