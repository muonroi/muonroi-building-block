# Rule Engine Short-Term Roadmap (Q1 2026)

## Timeline: Next 3 Months (March - May 2026)

```
March 2026          April 2026           May 2026
├─────────────┼─────────────┼─────────────┼─────────────┤
│  Week 1-2   │  Week 3-4   │  Week 5-8   │  Week 9-12  │
├─────────────┼─────────────┼─────────────┼─────────────┤
│ S1.1: DTD   │ S1.2: DTD   │ S2: FEEL    │ S4: Helm    │
│   Design    │   Impl      │   Full DMN  │   Charts    │
│             │ S3.1: Quota │ S3.2: Quota │             │
│             │   Design    │   Impl      │             │
└─────────────┴─────────────┴─────────────┴─────────────┘

S1 = Strategy 1: Visual Decision Table Designer
S2 = Strategy 2: Full DMN FEEL Compliance
S3 = Strategy 3: Multi-Tenant Quota Management
S4 = Strategy 4: Kubernetes Helm Charts
```

---

# Strategy 1: Visual Decision Table Designer

## 🎯 Objective
Build a web-based decision table editor that generates JSON workflows compatible with the existing rule engine.

## 📅 Timeline: Week 1-4 (March 1-28)

### Week 1-2: Design & Architecture
- [ ] **Day 1-2:** Requirements gathering
- [ ] **Day 3-5:** UI/UX mockups
- [ ] **Day 6-10:** Technical design

### Week 3-4: Implementation
- [ ] **Day 11-15:** Backend API
- [ ] **Day 16-20:** Frontend components
- [ ] **Day 21-25:** Integration & testing
- [ ] **Day 26-28:** Documentation

---

## 📦 Implementation Details

### A. Project Structure

```
src/
├── Muonroi.RuleEngine.DecisionTable/          # New package
│   ├── Muonroi.RuleEngine.DecisionTable.csproj
│   ├── Models/
│   │   ├── DecisionTable.cs                   # Core model
│   │   ├── DecisionTableCell.cs
│   │   ├── DecisionTableColumn.cs
│   │   ├── DecisionTableRow.cs
│   │   ├── HitPolicy.cs                       # FIRST, UNIQUE, PRIORITY, etc.
│   │   └── CellExpression.cs
│   ├── Converters/
│   │   ├── IDecisionTableConverter.cs
│   │   ├── DecisionTableToJsonConverter.cs    # DT → JSON workflow
│   │   ├── DecisionTableToRuleConverter.cs    # DT → IRule<T>
│   │   └── ExcelToDecisionTableConverter.cs   # Excel import
│   ├── Validators/
│   │   ├── DecisionTableValidator.cs          # Validate DT structure
│   │   ├── OverlapDetector.cs                 # Detect overlapping rules
│   │   └── GapDetector.cs                     # Detect coverage gaps
│   └── Serializers/
│       ├── DecisionTableJsonSerializer.cs
│       └── DecisionTableXmlSerializer.cs      # DMN XML export
│
├── Muonroi.RuleEngine.DecisionTable.Web/      # Web UI package
│   ├── Muonroi.RuleEngine.DecisionTable.Web.csproj
│   ├── wwwroot/
│   │   ├── css/
│   │   │   └── decision-table-editor.css
│   │   └── js/
│   │       ├── decision-table-editor.js       # Main editor
│   │       ├── cell-editor.js                 # FEEL expression editor
│   │       └── hit-policy-selector.js
│   ├── Controllers/
│   │   ├── DecisionTableController.cs         # CRUD API
│   │   ├── DecisionTableValidationController.cs
│   │   └── DecisionTableExportController.cs
│   ├── ViewModels/
│   │   ├── DecisionTableViewModel.cs
│   │   └── ValidationResultViewModel.cs
│   └── Views/
│       ├── DecisionTable/
│       │   ├── Editor.cshtml                  # Main editor view
│       │   ├── List.cshtml
│       │   └── Preview.cshtml
│       └── Shared/
│           └── _DecisionTableLayout.cshtml
│
└── tools/
    └── Muonroi.DecisionTableGen/              # CLI tool
        ├── Muonroi.DecisionTableGen.csproj
        └── Program.cs                         # dt-gen import/export/validate
```

---

### B. Core Models

```csharp
// src/Muonroi.RuleEngine.DecisionTable/Models/DecisionTable.cs
namespace Muonroi.RuleEngine.DecisionTable;

/// <summary>
/// Represents a DMN-style decision table.
/// </summary>
public sealed class DecisionTable
{
    /// <summary>Unique identifier for this decision table.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of what this table decides.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Hit policy: FIRST, UNIQUE, PRIORITY, COLLECT, etc.</summary>
    public HitPolicy HitPolicy { get; set; } = HitPolicy.First;

    /// <summary>Input columns (conditions).</summary>
    public List<DecisionTableColumn> InputColumns { get; set; } = [];

    /// <summary>Output columns (actions/results).</summary>
    public List<DecisionTableColumn> OutputColumns { get; set; } = [];

    /// <summary>Rows containing rules.</summary>
    public List<DecisionTableRow> Rows { get; set; } = [];

    /// <summary>Metadata for versioning.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Tenant ID for multi-tenant scenarios.</summary>
    public string? TenantId { get; set; }

    /// <summary>Created timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last modified timestamp.</summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a column in the decision table.
/// </summary>
public sealed class DecisionTableColumn
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DataType { get; set; } = "string"; // string, number, boolean, date
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Represents a row (rule) in the decision table.
/// </summary>
public sealed class DecisionTableRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Order { get; set; }
    public string? Description { get; set; }
    public List<DecisionTableCell> InputCells { get; set; } = [];
    public List<DecisionTableCell> OutputCells { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Represents a cell containing a FEEL expression.
/// </summary>
public sealed class DecisionTableCell
{
    public string ColumnId { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty; // FEEL expression
    public string? Comment { get; set; }
}

/// <summary>
/// DMN hit policies.
/// </summary>
public enum HitPolicy
{
    /// <summary>Return first matching rule.</summary>
    First,

    /// <summary>Only one rule should match (validation error if >1).</summary>
    Unique,

    /// <summary>Return all matching rules.</summary>
    Collect,

    /// <summary>Return highest priority matching rule.</summary>
    Priority,

    /// <summary>Return rule with highest output value.</summary>
    OutputOrder,

    /// <summary>Sum outputs of matching rules.</summary>
    CollectSum,

    /// <summary>Return minimum output.</summary>
    CollectMin,

    /// <summary>Return maximum output.</summary>
    CollectMax,

    /// <summary>Count matching rules.</summary>
    CollectCount
}
```

---

### C. Converter Implementation

```csharp
// src/Muonroi.RuleEngine.DecisionTable/Converters/DecisionTableToJsonConverter.cs
namespace Muonroi.RuleEngine.DecisionTable.Converters;

/// <summary>
/// Converts a DecisionTable to RulesEngineService-compatible JSON workflow.
/// </summary>
public sealed class DecisionTableToJsonConverter : IDecisionTableConverter
{
    public string Convert(DecisionTable table)
    {
        var workflow = new
        {
            WorkflowName = table.Id,
            Rules = table.Rows
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Order)
                .Select((row, index) => new
                {
                    RuleName = row.Id,
                    Description = row.Description,
                    Expression = BuildCombinedExpression(table, row),
                    Actions = new
                    {
                        OnSuccess = new
                        {
                            Name = "SetOutputs",
                            Context = BuildOutputContext(table, row)
                        }
                    },
                    SuccessEvent = row.Id,
                    Priority = table.Rows.Count - index // Higher priority for earlier rows
                })
                .ToArray()
        };

        return JsonSerializer.Serialize(workflow, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private string BuildCombinedExpression(DecisionTable table, DecisionTableRow row)
    {
        var conditions = row.InputCells
            .Select((cell, index) =>
            {
                var column = table.InputColumns[index];
                return $"({column.Name} {cell.Expression})";
            });

        return string.Join(" AND ", conditions);
    }

    private Dictionary<string, object> BuildOutputContext(DecisionTable table, DecisionTableRow row)
    {
        var outputs = new Dictionary<string, object>();
        for (int i = 0; i < row.OutputCells.Count; i++)
        {
            var column = table.OutputColumns[i];
            var cell = row.OutputCells[i];
            outputs[column.Name] = EvaluateOutputExpression(cell.Expression);
        }
        return outputs;
    }

    private object EvaluateOutputExpression(string expression)
    {
        // Simple evaluation - can be enhanced with FEEL evaluator
        if (bool.TryParse(expression, out var boolVal))
            return boolVal;
        if (int.TryParse(expression, out var intVal))
            return intVal;
        if (double.TryParse(expression, out var doubleVal))
            return doubleVal;
        return expression.Trim('"', '\'');
    }
}
```

---

### D. Validation Implementation

```csharp
// src/Muonroi.RuleEngine.DecisionTable/Validators/DecisionTableValidator.cs
namespace Muonroi.RuleEngine.DecisionTable.Validators;

public sealed class DecisionTableValidator
{
    public ValidationResult Validate(DecisionTable table)
    {
        var errors = new List<string>();

        // Check basic structure
        if (string.IsNullOrWhiteSpace(table.Name))
            errors.Add("Decision table name is required");

        if (table.InputColumns.Count == 0)
            errors.Add("At least one input column is required");

        if (table.OutputColumns.Count == 0)
            errors.Add("At least one output column is required");

        // Check for duplicate column names
        var allColumns = table.InputColumns.Concat(table.OutputColumns);
        var duplicates = allColumns
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dup in duplicates)
            errors.Add($"Duplicate column name: {dup}");

        // Validate each row
        foreach (var (row, index) in table.Rows.Select((r, i) => (r, i)))
        {
            if (row.InputCells.Count != table.InputColumns.Count)
                errors.Add($"Row {index}: Input cell count mismatch");

            if (row.OutputCells.Count != table.OutputColumns.Count)
                errors.Add($"Row {index}: Output cell count mismatch");

            // Validate FEEL expressions
            foreach (var cell in row.InputCells.Concat(row.OutputCells))
            {
                if (string.IsNullOrWhiteSpace(cell.Expression))
                    errors.Add($"Row {index}: Empty expression in column {cell.ColumnId}");
            }
        }

        // Detect overlaps (UNIQUE hit policy)
        if (table.HitPolicy == HitPolicy.Unique)
        {
            var overlaps = DetectOverlaps(table);
            errors.AddRange(overlaps.Select(o => $"Overlap detected: {o}"));
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private List<string> DetectOverlaps(DecisionTable table)
    {
        // TODO: Implement overlap detection algorithm
        // This is complex - needs to check if two rules can match same input
        return [];
    }
}

public record ValidationResult(bool IsValid, List<string> Errors);
```

---

### E. Web Controller API

```csharp
// src/Muonroi.RuleEngine.DecisionTable.Web/Controllers/DecisionTableController.cs
namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

[ApiController]
[Route("api/v1/decision-tables")]
public class DecisionTableController : ControllerBase
{
    private readonly IDecisionTableStore _store;
    private readonly DecisionTableValidator _validator;
    private readonly IDecisionTableConverter _converter;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var tables = await _store.GetAllAsync(page, pageSize);
        return Ok(tables);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var table = await _store.GetByIdAsync(id);
        if (table == null)
            return NotFound();
        return Ok(table);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DecisionTable table)
    {
        var validation = _validator.Validate(table);
        if (!validation.IsValid)
            return BadRequest(validation.Errors);

        table.Id = Guid.NewGuid().ToString();
        table.CreatedAt = DateTimeOffset.UtcNow;
        table.ModifiedAt = DateTimeOffset.UtcNow;

        await _store.SaveAsync(table);
        return CreatedAtAction(nameof(Get), new { id = table.Id }, table);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] DecisionTable table)
    {
        var existing = await _store.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var validation = _validator.Validate(table);
        if (!validation.IsValid)
            return BadRequest(validation.Errors);

        table.Id = id;
        table.ModifiedAt = DateTimeOffset.UtcNow;

        await _store.SaveAsync(table);
        return Ok(table);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _store.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/validate")]
    public async Task<IActionResult> Validate(string id)
    {
        var table = await _store.GetByIdAsync(id);
        if (table == null)
            return NotFound();

        var result = _validator.Validate(table);
        return Ok(result);
    }

    [HttpPost("{id}/export/json")]
    public async Task<IActionResult> ExportToJson(string id)
    {
        var table = await _store.GetByIdAsync(id);
        if (table == null)
            return NotFound();

        var json = _converter.Convert(table);
        return Content(json, "application/json");
    }

    [HttpPost("{id}/export/dmn")]
    public async Task<IActionResult> ExportToDmn(string id)
    {
        var table = await _store.GetByIdAsync(id);
        if (table == null)
            return NotFound();

        // TODO: Implement DMN XML export
        return Ok("DMN export not yet implemented");
    }
}
```

---

### F. Frontend JavaScript

```javascript
// src/Muonroi.RuleEngine.DecisionTable.Web/wwwroot/js/decision-table-editor.js

class DecisionTableEditor {
    constructor(containerId, options = {}) {
        this.container = document.getElementById(containerId);
        this.table = options.table || this.createEmptyTable();
        this.render();
    }

    createEmptyTable() {
        return {
            id: this.generateId(),
            name: 'New Decision Table',
            description: '',
            hitPolicy: 'FIRST',
            inputColumns: [
                { id: this.generateId(), name: 'input1', label: 'Input 1', dataType: 'string' }
            ],
            outputColumns: [
                { id: this.generateId(), name: 'output1', label: 'Output 1', dataType: 'string' }
            ],
            rows: [
                {
                    id: this.generateId(),
                    order: 1,
                    inputCells: [{ columnId: 'input1', expression: '' }],
                    outputCells: [{ columnId: 'output1', expression: '' }],
                    isEnabled: true
                }
            ]
        };
    }

    render() {
        const html = `
            <div class="decision-table-editor">
                <div class="dt-toolbar">
                    <button onclick="editor.addColumn('input')">+ Input Column</button>
                    <button onclick="editor.addColumn('output')">+ Output Column</button>
                    <button onclick="editor.addRow()">+ Row</button>
                    <button onclick="editor.validate()">Validate</button>
                    <button onclick="editor.save()">Save</button>
                    <button onclick="editor.exportJson()">Export JSON</button>
                </div>

                <table class="dt-table">
                    <thead>
                        <tr>
                            <th rowspan="2">#</th>
                            <th colspan="${this.table.inputColumns.length}">Input</th>
                            <th colspan="${this.table.outputColumns.length}">Output</th>
                            <th rowspan="2">Actions</th>
                        </tr>
                        <tr>
                            ${this.table.inputColumns.map(col => `
                                <th>
                                    <input type="text" value="${col.label}"
                                           onchange="editor.updateColumnLabel('${col.id}', this.value)">
                                    <select onchange="editor.updateColumnType('${col.id}', this.value)">
                                        <option ${col.dataType === 'string' ? 'selected' : ''}>string</option>
                                        <option ${col.dataType === 'number' ? 'selected' : ''}>number</option>
                                        <option ${col.dataType === 'boolean' ? 'selected' : ''}>boolean</option>
                                        <option ${col.dataType === 'date' ? 'selected' : ''}>date</option>
                                    </select>
                                </th>
                            `).join('')}
                            ${this.table.outputColumns.map(col => `
                                <th>
                                    <input type="text" value="${col.label}"
                                           onchange="editor.updateColumnLabel('${col.id}', this.value)">
                                </th>
                            `).join('')}
                        </tr>
                    </thead>
                    <tbody>
                        ${this.table.rows.map((row, index) => `
                            <tr>
                                <td>${index + 1}</td>
                                ${row.inputCells.map(cell => `
                                    <td>
                                        <input type="text"
                                               class="feel-expression"
                                               value="${cell.expression}"
                                               placeholder="FEEL expression"
                                               onchange="editor.updateCell('${row.id}', '${cell.columnId}', this.value)">
                                    </td>
                                `).join('')}
                                ${row.outputCells.map(cell => `
                                    <td>
                                        <input type="text"
                                               value="${cell.expression}"
                                               placeholder="Output value"
                                               onchange="editor.updateCell('${row.id}', '${cell.columnId}', this.value)">
                                    </td>
                                `).join('')}
                                <td>
                                    <button onclick="editor.deleteRow('${row.id}')">🗑️</button>
                                </td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
        `;

        this.container.innerHTML = html;
    }

    addColumn(type) {
        const column = {
            id: this.generateId(),
            name: `${type}${this.table[type + 'Columns'].length + 1}`,
            label: `${type.charAt(0).toUpperCase() + type.slice(1)} ${this.table[type + 'Columns'].length + 1}`,
            dataType: 'string'
        };

        this.table[type + 'Columns'].push(column);

        // Add cells to existing rows
        this.table.rows.forEach(row => {
            row[type + 'Cells'].push({
                columnId: column.id,
                expression: ''
            });
        });

        this.render();
    }

    addRow() {
        const row = {
            id: this.generateId(),
            order: this.table.rows.length + 1,
            inputCells: this.table.inputColumns.map(col => ({
                columnId: col.id,
                expression: ''
            })),
            outputCells: this.table.outputColumns.map(col => ({
                columnId: col.id,
                expression: ''
            })),
            isEnabled: true
        };

        this.table.rows.push(row);
        this.render();
    }

    deleteRow(rowId) {
        this.table.rows = this.table.rows.filter(r => r.id !== rowId);
        this.render();
    }

    updateCell(rowId, columnId, value) {
        const row = this.table.rows.find(r => r.id === rowId);
        if (!row) return;

        const allCells = [...row.inputCells, ...row.outputCells];
        const cell = allCells.find(c => c.columnId === columnId);
        if (cell) {
            cell.expression = value;
        }
    }

    async save() {
        try {
            const response = await fetch('/api/v1/decision-tables', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(this.table)
            });

            if (response.ok) {
                alert('Decision table saved successfully!');
            } else {
                const errors = await response.json();
                alert('Validation errors: ' + errors.join('\n'));
            }
        } catch (error) {
            alert('Error saving: ' + error.message);
        }
    }

    async validate() {
        try {
            const response = await fetch(`/api/v1/decision-tables/${this.table.id}/validate`, {
                method: 'POST'
            });

            const result = await response.json();
            if (result.isValid) {
                alert('✅ Decision table is valid!');
            } else {
                alert('❌ Validation errors:\n' + result.errors.join('\n'));
            }
        } catch (error) {
            alert('Error validating: ' + error.message);
        }
    }

    async exportJson() {
        try {
            const response = await fetch(`/api/v1/decision-tables/${this.table.id}/export/json`, {
                method: 'POST'
            });

            const json = await response.text();
            const blob = new Blob([json], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${this.table.name}.json`;
            a.click();
        } catch (error) {
            alert('Error exporting: ' + error.message);
        }
    }

    generateId() {
        return 'dt_' + Math.random().toString(36).substr(2, 9);
    }
}

// Initialize editor
let editor;
window.addEventListener('DOMContentLoaded', () => {
    editor = new DecisionTableEditor('decision-table-container');
});
```

---

### G. Testing Strategy

```csharp
// tests/Muonroi.RuleEngine.DecisionTable.Tests/DecisionTableConverterTests.cs
public class DecisionTableConverterTests
{
    [Fact]
    public void Convert_SimpleTable_GeneratesCorrectJson()
    {
        // Arrange
        var table = new DecisionTable
        {
            Name = "Age Check",
            HitPolicy = HitPolicy.First,
            InputColumns =
            [
                new() { Name = "age", Label = "Age", DataType = "number" }
            ],
            OutputColumns =
            [
                new() { Name = "canDrive", Label = "Can Drive", DataType = "boolean" }
            ],
            Rows =
            [
                new()
                {
                    Order = 1,
                    InputCells = [new() { Expression = ">= 18" }],
                    OutputCells = [new() { Expression = "true" }]
                },
                new()
                {
                    Order = 2,
                    InputCells = [new() { Expression = "< 18" }],
                    OutputCells = [new() { Expression = "false" }]
                }
            ]
        };

        var converter = new DecisionTableToJsonConverter();

        // Act
        var json = converter.Convert(table);

        // Assert
        var workflow = JsonSerializer.Deserialize<JsonDocument>(json);
        Assert.NotNull(workflow);
        Assert.Equal("Age Check", workflow.RootElement.GetProperty("WorkflowName").GetString());
        Assert.Equal(2, workflow.RootElement.GetProperty("Rules").GetArrayLength());
    }

    [Fact]
    public void Validate_TableWithOverlaps_ReturnsErrors()
    {
        // Arrange
        var table = new DecisionTable
        {
            HitPolicy = HitPolicy.Unique,
            InputColumns = [new() { Name = "x", DataType = "number" }],
            OutputColumns = [new() { Name = "y", DataType = "string" }],
            Rows =
            [
                new() { InputCells = [new() { Expression = "> 10" }], OutputCells = [new() { Expression = "A" }] },
                new() { InputCells = [new() { Expression = "> 5" }], OutputCells = [new() { Expression = "B" }] }
                // These overlap for x=15
            ]
        };

        var validator = new DecisionTableValidator();

        // Act
        var result = validator.Validate(table);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("overlap", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }
}
```

---

## 📊 Success Criteria

- [ ] Can create decision table with 10+ columns and 100+ rows
- [ ] Can export to JSON workflow format
- [ ] Can validate decision tables (detect overlaps/gaps)
- [ ] Can import from Excel
- [ ] Can visualize in web UI
- [ ] Performance: Render 100-row table in <200ms
- [ ] Test coverage: >90%
- [ ] Documentation: Complete API docs + user guide

---

## 📚 Documentation Deliverables

1. `docs/decision-table-guide.md` - User guide
2. `docs/decision-table-api-reference.md` - API documentation
3. `Samples/DecisionTableDemo/` - Sample project
4. Video tutorial (optional)

---

# Strategy 2: Full DMN FEEL Compliance

## 🎯 Objective
Expand FeelEvaluator to support 100% of DMN 1.3 FEEL specification.

## 📅 Timeline: Week 5-8 (April 1-28)

---

## 📦 Current FEEL Coverage

### ✅ Already Implemented (Phase 1)
- Logical: AND, OR, NOT
- Arithmetic: +, -, *, /
- Comparison: =, !=, >, >=, <, <=
- String: contains, startsWith, upper, lower
- Date: today(), now(), days(), years()
- Math: abs(), round(), sum()
- Nested paths: order.customer.name
- Array access: items[0], items[*]

### ❌ Missing (DMN 1.3 Required)

#### **A. Data Types**
- [ ] Duration: duration("P1Y"), duration("PT2H")
- [ ] Date/Time: date("2026-03-01"), time("14:30:00"), date and time("...")
- [ ] Year-Month Duration: years and months duration(...)
- [ ] Day-Time Duration: days and time duration(...)

#### **B. Built-in Functions**
- [ ] **List functions:**
  - list contains(list, element)
  - count(list)
  - min(list), max(list)
  - sum(list), mean(list)
  - all(list), any(list)
  - sublist(list, start, length)
  - append(list, item), concatenate(list1, list2)
  - insert before(list, position, item)
  - remove(list, position)
  - reverse(list), index of(list, match)
  - union(list1, list2), distinct values(list)
  - flatten(list), sort(list, precedes)

- [ ] **String functions:**
  - string length(string)
  - substring(string, start, length)
  - string(from)
  - upper case(string), lower case(string)
  - substring before(string, match)
  - substring after(string, match)
  - replace(input, pattern, replacement, flags)
  - contains(string, match)
  - starts with(string, match)
  - ends with(string, match)
  - matches(input, pattern, flags)
  - split(string, delimiter)

- [ ] **Number functions:**
  - decimal(n, scale)
  - floor(n), ceiling(n)
  - abs(n), modulo(dividend, divisor)
  - sqrt(n), log(n), exp(n)
  - odd(n), even(n)

- [ ] **Date functions:**
  - date(from), date(year, month, day)
  - date and time(from)
  - time(from), time(hour, minute, second)
  - day of week(date), day of year(date)
  - week of year(date)
  - month of year(date), year(date)

- [ ] **Temporal functions:**
  - duration(from)
  - years and months duration(from, to)
  - is(value1, value2)

- [ ] **Boolean functions:**
  - not(boolean)

- [ ] **Context functions:**
  - get entries(context)
  - get value(context, key)

#### **C. Advanced Expressions**
- [ ] **For loops:** for i in 1..10 return i * 2
- [ ] **Quantified expressions:**
  - some x in list satisfies condition
  - every x in list satisfies condition
- [ ] **If-then-else:** if condition then value1 else value2
- [ ] **Filter expressions:** list[item.price > 100]
- [ ] **Context expressions:** {x: 10, y: 20, sum: x + y}
- [ ] **Function definitions:** function(x, y) x + y
- [ ] **Ranges:** [1..10], (1..10), [1..10)
- [ ] **Between:** x between 10 and 20
- [ ] **Instance of:** x instance of number

---

## 📦 Implementation Plan

### Week 5: Core Language Features

#### **Day 1-2: Type System Enhancement**

```csharp
// src/Muonroi.Rules/Feel/FeelType.cs
namespace Muonroi.Rules.Feel;

public enum FeelType
{
    Number,
    String,
    Boolean,
    Date,
    Time,
    DateTime,
    Duration,
    YearMonthDuration,
    DayTimeDuration,
    List,
    Context,
    Range,
    Function,
    Null
}

public abstract record FeelValue
{
    public abstract FeelType Type { get; }
    public abstract object? Value { get; }
}

public record FeelNumber(double Value) : FeelValue
{
    public override FeelType Type => FeelType.Number;
    object? FeelValue.Value => Value;
}

public record FeelString(string Value) : FeelValue
{
    public override FeelType Type => FeelType.String;
    object? FeelValue.Value => Value;
}

public record FeelDate(DateOnly Value) : FeelValue
{
    public override FeelType Type => FeelType.Date;
    object? FeelValue.Value => Value;
}

public record FeelDuration(TimeSpan Value) : FeelValue
{
    public override FeelType Type => FeelType.Duration;
    object? FeelValue.Value => Value;
}

public record FeelList(List<FeelValue> Items) : FeelValue
{
    public override FeelType Type => FeelType.List;
    object? FeelValue.Value => Items;
}

public record FeelContext(Dictionary<string, FeelValue> Entries) : FeelValue
{
    public override FeelType Type => FeelType.Context;
    object? FeelValue.Value => Entries;
}

public record FeelRange(FeelValue Start, FeelValue End, bool IncludeStart, bool IncludeEnd) : FeelValue
{
    public override FeelType Type => FeelType.Range;
    object? FeelValue.Value => (Start, End);
}
```

#### **Day 3-5: Parser Enhancement**

```csharp
// src/Muonroi.Rules/Feel/FeelParser.cs
namespace Muonroi.Rules.Feel;

public class FeelParser
{
    public FeelValue Parse(string expression, Dictionary<string, object> variables)
    {
        var tokens = Tokenize(expression);
        var ast = BuildAst(tokens);
        return Evaluate(ast, variables);
    }

    private List<Token> Tokenize(string expression)
    {
        // Lexer: convert string to tokens
        // Support: numbers, strings, keywords, operators, parentheses, etc.
        return [];
    }

    private AstNode BuildAst(List<Token> tokens)
    {
        // Parser: build Abstract Syntax Tree
        // Support precedence, associativity, function calls, etc.
        return new LiteralNode(null);
    }

    private FeelValue Evaluate(AstNode node, Dictionary<string, object> variables)
    {
        // Evaluator: walk AST and compute result
        return node switch
        {
            LiteralNode lit => EvaluateLiteral(lit),
            BinaryOpNode bin => EvaluateBinaryOp(bin, variables),
            FunctionCallNode fn => EvaluateFunctionCall(fn, variables),
            ForNode loop => EvaluateFor(loop, variables),
            IfNode cond => EvaluateIf(cond, variables),
            _ => throw new NotSupportedException()
        };
    }
}

// AST Node types
public abstract record AstNode;
public record LiteralNode(object? Value) : AstNode;
public record VariableNode(string Name) : AstNode;
public record BinaryOpNode(AstNode Left, string Operator, AstNode Right) : AstNode;
public record FunctionCallNode(string Name, List<AstNode> Arguments) : AstNode;
public record ForNode(string Variable, AstNode Collection, AstNode Body) : AstNode;
public record IfNode(AstNode Condition, AstNode ThenBranch, AstNode ElseBranch) : AstNode;
public record QuantifiedNode(string Quantifier, string Variable, AstNode Collection, AstNode Predicate) : AstNode;
public record PathNode(AstNode Object, string Property) : AstNode;
public record IndexNode(AstNode Array, AstNode Index) : AstNode;
```

---

### Week 6-7: Built-in Functions Library

```csharp
// src/Muonroi.Rules/Feel/FeelStandardLibrary.cs
namespace Muonroi.Rules.Feel;

public static class FeelStandardLibrary
{
    private static readonly Dictionary<string, Delegate> Functions = new()
    {
        // List functions
        ["list contains"] = (List<FeelValue> list, FeelValue element) =>
            list.Contains(element),

        ["count"] = (List<FeelValue> list) =>
            new FeelNumber(list.Count),

        ["min"] = (List<FeelValue> list) =>
            list.OfType<FeelNumber>().Min(n => n.Value),

        ["max"] = (List<FeelValue> list) =>
            list.OfType<FeelNumber>().Max(n => n.Value),

        ["sum"] = (List<FeelValue> list) =>
            new FeelNumber(list.OfType<FeelNumber>().Sum(n => n.Value)),

        ["mean"] = (List<FeelValue> list) =>
            new FeelNumber(list.OfType<FeelNumber>().Average(n => n.Value)),

        ["all"] = (List<FeelValue> list) =>
            list.OfType<FeelValue>().All(v => v is FeelValue { Type: FeelType.Boolean } b && (bool)b.Value!),

        ["any"] = (List<FeelValue> list) =>
            list.OfType<FeelValue>().Any(v => v is FeelValue { Type: FeelType.Boolean } b && (bool)b.Value!),

        ["sublist"] = (List<FeelValue> list, int start, int? length) =>
            new FeelList(length.HasValue
                ? list.Skip(start - 1).Take(length.Value).ToList()
                : list.Skip(start - 1).ToList()),

        ["append"] = (List<FeelValue> list, FeelValue item) =>
            new FeelList([..list, item]),

        ["concatenate"] = (List<FeelValue> list1, List<FeelValue> list2) =>
            new FeelList([..list1, ..list2]),

        ["reverse"] = (List<FeelValue> list) =>
            new FeelList([..list.AsEnumerable().Reverse()]),

        ["distinct values"] = (List<FeelValue> list) =>
            new FeelList(list.Distinct().ToList()),

        // String functions
        ["string length"] = (string str) =>
            new FeelNumber(str.Length),

        ["substring"] = (string str, int start, int? length) =>
            new FeelString(length.HasValue
                ? str.Substring(start - 1, length.Value)
                : str.Substring(start - 1)),

        ["substring before"] = (string str, string match) =>
        {
            var index = str.IndexOf(match, StringComparison.Ordinal);
            return new FeelString(index >= 0 ? str.Substring(0, index) : "");
        },

        ["substring after"] = (string str, string match) =>
        {
            var index = str.IndexOf(match, StringComparison.Ordinal);
            return new FeelString(index >= 0 ? str.Substring(index + match.Length) : "");
        },

        ["replace"] = (string input, string pattern, string replacement, string? flags) =>
            new FeelString(System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement)),

        ["split"] = (string str, string delimiter) =>
            new FeelList(str.Split(delimiter).Select(s => new FeelString(s) as FeelValue).ToList()),

        ["starts with"] = (string str, string prefix) =>
            str.StartsWith(prefix),

        ["ends with"] = (string str, string suffix) =>
            str.EndsWith(suffix),

        // Number functions
        ["floor"] = (double n) => new FeelNumber(Math.Floor(n)),
        ["ceiling"] = (double n) => new FeelNumber(Math.Ceiling(n)),
        ["sqrt"] = (double n) => new FeelNumber(Math.Sqrt(n)),
        ["log"] = (double n) => new FeelNumber(Math.Log(n)),
        ["exp"] = (double n) => new FeelNumber(Math.Exp(n)),
        ["odd"] = (double n) => (int)n % 2 != 0,
        ["even"] = (double n) => (int)n % 2 == 0,
        ["modulo"] = (double dividend, double divisor) => new FeelNumber(dividend % divisor),

        // Date functions
        ["day of week"] = (DateOnly date) =>
            new FeelString(date.DayOfWeek.ToString()),

        ["day of year"] = (DateOnly date) =>
            new FeelNumber(date.DayOfYear),

        ["week of year"] = (DateOnly date) =>
            new FeelNumber(System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue))),

        ["month of year"] = (DateOnly date) =>
            new FeelNumber(date.Month),

        ["year"] = (DateOnly date) =>
            new FeelNumber(date.Year),
    };

    public static FeelValue CallFunction(string name, List<FeelValue> arguments)
    {
        if (!Functions.TryGetValue(name, out var func))
            throw new InvalidOperationException($"Unknown function: {name}");

        // Convert FeelValue arguments to .NET types
        var args = ConvertArguments(arguments);

        // Invoke function
        var result = func.DynamicInvoke(args);

        // Convert result back to FeelValue
        return ConvertToFeelValue(result);
    }

    private static object?[] ConvertArguments(List<FeelValue> arguments)
    {
        return arguments.Select(arg => arg.Value).ToArray();
    }

    private static FeelValue ConvertToFeelValue(object? result)
    {
        return result switch
        {
            double d => new FeelNumber(d),
            int i => new FeelNumber(i),
            string s => new FeelString(s),
            bool b => new FeelValue { Type = FeelType.Boolean, Value = b } as FeelValue,
            DateOnly date => new FeelDate(date),
            TimeSpan ts => new FeelDuration(ts),
            List<FeelValue> list => new FeelList(list),
            _ => throw new NotSupportedException($"Cannot convert {result?.GetType()} to FeelValue")
        };
    }
}
```

---

### Week 8: Testing & Documentation

#### **Comprehensive Test Suite**

```csharp
// tests/Rules/Feel/FeelDmnComplianceTests.cs
public class FeelDmnComplianceTests
{
    [Theory]
    [InlineData("list contains([1,2,3], 2)", true)]
    [InlineData("count([1,2,3,4,5])", 5)]
    [InlineData("min([5,2,8,1,9])", 1)]
    [InlineData("max([5,2,8,1,9])", 9)]
    [InlineData("sum([1,2,3,4,5])", 15)]
    [InlineData("mean([1,2,3,4,5])", 3)]
    public void BuiltInFunction_ListFunctions_ReturnsExpectedResult(string expression, object expected)
    {
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("string length('hello')", 5)]
    [InlineData("substring('hello', 2, 3)", "ell")]
    [InlineData("substring before('hello world', ' ')", "hello")]
    [InlineData("substring after('hello world', ' ')", "world")]
    [InlineData("split('a,b,c', ',')", new[] { "a", "b", "c" })]
    public void BuiltInFunction_StringFunctions_ReturnsExpectedResult(string expression, object expected)
    {
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("for i in 1..5 return i * 2", new[] { 2, 4, 6, 8, 10 })]
    [InlineData("for x in [1,2,3] return x + 10", new[] { 11, 12, 13 })]
    public void Expression_ForLoop_ReturnsExpectedSequence(string expression, int[] expected)
    {
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("some x in [1,2,3,4,5] satisfies x > 3", true)]
    [InlineData("every x in [1,2,3,4,5] satisfies x > 0", true)]
    [InlineData("every x in [1,2,3,4,5] satisfies x > 3", false)]
    public void Expression_Quantified_ReturnsExpectedBoolean(string expression, bool expected)
    {
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("if 10 > 5 then 'yes' else 'no'", "yes")]
    [InlineData("if 10 < 5 then 'yes' else 'no'", "no")]
    public void Expression_IfThenElse_ReturnsCorrectBranch(string expression, string expected)
    {
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Expression_Context_CanAccessNestedProperties()
    {
        var expression = "{x: 10, y: 20, sum: x + y}.sum";
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(30, result);
    }

    [Theory]
    [InlineData("[1,2,3,4,5][item > 2]", new[] { 3, 4, 5 })]
    [InlineData("[{x:1}, {x:2}, {x:3}][item.x > 1]", new[] { 2, 3 })]
    public void Expression_FilterExpression_FiltersCorrectly(string expression, int[] expected)
    {
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("10 between 5 and 15", true)]
    [InlineData("10 between 15 and 20", false)]
    [InlineData("[1..10]", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    public void Expression_Ranges_WorksCorrectly(string expression, object expected)
    {
        var result = FeelEvaluator.Evaluate(expression, new Dictionary<string, object>());
        Assert.Equal(expected, result);
    }
}
```

---

## 📊 Success Criteria

- [ ] 100% DMN 1.3 FEEL compliance
- [ ] All 60+ built-in functions implemented
- [ ] Performance: Parse & evaluate <10ms for typical expressions
- [ ] Test coverage: >95%
- [ ] DMN Technology Compatibility Kit (TCK) tests passing
- [ ] Documentation: Complete FEEL reference guide

---

## 📚 Documentation Deliverables

1. `docs/feel-reference.md` - Complete FEEL language reference
2. `docs/feel-migration-guide.md` - Migrating from v1 to v2
3. `Samples/FeelPlayground/` - Interactive FEEL playground
4. DMN compliance certificate

---

# Strategy 3: Multi-Tenant Quota Management

## 🎯 Objective
Add comprehensive quota and rate limiting for multi-tenant scenarios.

## 📅 Timeline: Week 3-8 (March 15 - April 28)

---

## 📦 Features

### A. Quota Types

1. **Rule Execution Quotas**
   - Max rules per tenant
   - Max rule executions per day/month
   - Max concurrent executions

2. **Storage Quotas**
   - Max decision tables per tenant
   - Max JSON workflows per tenant
   - Max total storage (MB)

3. **Rate Limiting**
   - API requests per second/minute
   - Rule evaluations per second
   - Workflow executions per hour

4. **Resource Limits**
   - Max rule complexity (expression depth)
   - Max workflow size (KB)
   - Max execution time (ms)

---

## 📦 Implementation

### A. Quota Models

```csharp
// src/Muonroi.BuildingBlock/Shared/Tenancy/TenantQuota.cs
namespace Muonroi.BuildingBlock.Shared.Tenancy;

public sealed class TenantQuota
{
    public string TenantId { get; set; } = string.Empty;

    // Rule quotas
    public int MaxRulesPerTenant { get; set; } = 100;
    public int MaxRuleExecutionsPerDay { get; set; } = 10000;
    public int MaxConcurrentExecutions { get; set; } = 10;

    // Storage quotas
    public int MaxDecisionTables { get; set; } = 50;
    public int MaxJsonWorkflows { get; set; } = 100;
    public int MaxStorageMB { get; set; } = 100;

    // Rate limits
    public int MaxApiRequestsPerMinute { get; set; } = 100;
    public int MaxRuleEvaluationsPerSecond { get; set; } = 50;
    public int MaxWorkflowExecutionsPerHour { get; set; } = 500;

    // Resource limits
    public int MaxRuleComplexity { get; set; } = 10; // Max nested depth
    public int MaxWorkflowSizeKB { get; set; } = 500;
    public int MaxExecutionTimeMs { get; set; } = 5000;

    // Tier
    public TenantTier Tier { get; set; } = TenantTier.Free;
}

public enum TenantTier
{
    Free,
    Starter,
    Professional,
    Enterprise
}

public static class TenantQuotaPresets
{
    public static TenantQuota Free => new()
    {
        Tier = TenantTier.Free,
        MaxRulesPerTenant = 10,
        MaxRuleExecutionsPerDay = 1000,
        MaxConcurrentExecutions = 2,
        MaxDecisionTables = 5,
        MaxJsonWorkflows = 10,
        MaxStorageMB = 10,
        MaxApiRequestsPerMinute = 20,
        MaxRuleEvaluationsPerSecond = 10,
        MaxWorkflowExecutionsPerHour = 100,
        MaxRuleComplexity = 5,
        MaxWorkflowSizeKB = 50,
        MaxExecutionTimeMs = 1000
    };

    public static TenantQuota Starter => new()
    {
        Tier = TenantTier.Starter,
        MaxRulesPerTenant = 50,
        MaxRuleExecutionsPerDay = 10000,
        MaxConcurrentExecutions = 5,
        MaxDecisionTables = 20,
        MaxJsonWorkflows = 50,
        MaxStorageMB = 50,
        MaxApiRequestsPerMinute = 100,
        MaxRuleEvaluationsPerSecond = 50,
        MaxWorkflowExecutionsPerHour = 1000,
        MaxRuleComplexity = 10,
        MaxWorkflowSizeKB = 200,
        MaxExecutionTimeMs = 3000
    };

    public static TenantQuota Professional => new()
    {
        Tier = TenantTier.Professional,
        MaxRulesPerTenant = 200,
        MaxRuleExecutionsPerDay = 100000,
        MaxConcurrentExecutions = 20,
        MaxDecisionTables = 100,
        MaxJsonWorkflows = 200,
        MaxStorageMB = 500,
        MaxApiRequestsPerMinute = 500,
        MaxRuleEvaluationsPerSecond = 200,
        MaxWorkflowExecutionsPerHour = 10000,
        MaxRuleComplexity = 20,
        MaxWorkflowSizeKB = 1000,
        MaxExecutionTimeMs = 10000
    };

    public static TenantQuota Enterprise => new()
    {
        Tier = TenantTier.Enterprise,
        MaxRulesPerTenant = int.MaxValue,
        MaxRuleExecutionsPerDay = int.MaxValue,
        MaxConcurrentExecutions = 100,
        MaxDecisionTables = int.MaxValue,
        MaxJsonWorkflows = int.MaxValue,
        MaxStorageMB = int.MaxValue,
        MaxApiRequestsPerMinute = int.MaxValue,
        MaxRuleEvaluationsPerSecond = int.MaxValue,
        MaxWorkflowExecutionsPerHour = int.MaxValue,
        MaxRuleComplexity = int.MaxValue,
        MaxWorkflowSizeKB = int.MaxValue,
        MaxExecutionTimeMs = 60000
    };
}
```

---

### B. Quota Tracking

```csharp
// src/Muonroi.BuildingBlock/Shared/Tenancy/TenantQuotaTracker.cs
namespace Muonroi.BuildingBlock.Shared.Tenancy;

public interface ITenantQuotaTracker
{
    Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default);
    Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default);
    Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default);
    Task ResetDailyQuotasAsync(CancellationToken ct = default);
}

public sealed class TenantQuotaTracker(
    IDistributedCache cache,
    ITenantQuotaStore quotaStore,
    ILogger<TenantQuotaTracker> logger) : ITenantQuotaTracker
{
    public async Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount, CancellationToken ct)
    {
        var quota = await quotaStore.GetQuotaAsync(tenantId, ct);
        if (quota == null)
        {
            logger.LogWarning("No quota found for tenant {TenantId}, using Free tier", tenantId);
            quota = TenantQuotaPresets.Free;
        }

        var usage = await GetCurrentUsageAsync(tenantId, type, ct);
        var limit = GetLimit(quota, type);

        if (usage + amount > limit)
        {
            logger.LogWarning("Quota exceeded for tenant {TenantId}, type {Type}: {Usage}/{Limit}",
                tenantId, type, usage, limit);
            return false;
        }

        return true;
    }

    public async Task IncrementUsageAsync(string tenantId, QuotaType type, int amount, CancellationToken ct)
    {
        var key = GetCacheKey(tenantId, type);
        var currentValue = await cache.GetStringAsync(key, ct);
        var current = int.TryParse(currentValue, out var val) ? val : 0;

        await cache.SetStringAsync(key, (current + amount).ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetExpirationFor(type)
        }, ct);

        // Also track in database for historical analytics
        await quotaStore.RecordUsageAsync(tenantId, type, amount, ct);
    }

    private async Task<int> GetCurrentUsageAsync(string tenantId, QuotaType type, CancellationToken ct)
    {
        var key = GetCacheKey(tenantId, type);
        var value = await cache.GetStringAsync(key, ct);
        return int.TryParse(value, out var result) ? result : 0;
    }

    private int GetLimit(TenantQuota quota, QuotaType type)
    {
        return type switch
        {
            QuotaType.RuleExecutionsPerDay => quota.MaxRuleExecutionsPerDay,
            QuotaType.ConcurrentExecutions => quota.MaxConcurrentExecutions,
            QuotaType.ApiRequestsPerMinute => quota.MaxApiRequestsPerMinute,
            QuotaType.RuleEvaluationsPerSecond => quota.MaxRuleEvaluationsPerSecond,
            QuotaType.WorkflowExecutionsPerHour => quota.MaxWorkflowExecutionsPerHour,
            _ => int.MaxValue
        };
    }

    private TimeSpan GetExpirationFor(QuotaType type)
    {
        return type switch
        {
            QuotaType.RuleExecutionsPerDay => TimeSpan.FromDays(1),
            QuotaType.ApiRequestsPerMinute => TimeSpan.FromMinutes(1),
            QuotaType.RuleEvaluationsPerSecond => TimeSpan.FromSeconds(1),
            QuotaType.WorkflowExecutionsPerHour => TimeSpan.FromHours(1),
            _ => TimeSpan.FromDays(1)
        };
    }

    private string GetCacheKey(string tenantId, QuotaType type)
    {
        return $"quota:{tenantId}:{type}:{DateTime.UtcNow:yyyyMMdd}";
    }

    public async Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct)
    {
        return await quotaStore.GetUsageAsync(tenantId, ct);
    }

    public async Task ResetDailyQuotasAsync(CancellationToken ct)
    {
        logger.LogInformation("Resetting daily quotas for all tenants");
        // This would be called by a background job daily at midnight
        await quotaStore.ResetDailyCountersAsync(ct);
    }
}

public enum QuotaType
{
    RuleExecutionsPerDay,
    ConcurrentExecutions,
    ApiRequestsPerMinute,
    RuleEvaluationsPerSecond,
    WorkflowExecutionsPerHour,
    StorageUsageMB,
    TotalRules,
    TotalDecisionTables,
    TotalWorkflows
}

public sealed class QuotaUsage
{
    public string TenantId { get; set; } = string.Empty;
    public Dictionary<QuotaType, int> CurrentUsage { get; set; } = [];
    public Dictionary<QuotaType, int> Limits { get; set; } = [];
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
```

---

### C. Quota Enforcement Middleware

```csharp
// src/Muonroi.BuildingBlock/External/Middleware/QuotaEnforcementMiddleware.cs
namespace Muonroi.BuildingBlock.External.Middleware;

public class QuotaEnforcementMiddleware(
    RequestDelegate next,
    ITenantQuotaTracker quotaTracker,
    ILogger<QuotaEnforcementMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await next(context);
            return;
        }

        // Check API rate limit
        var allowed = await quotaTracker.CheckQuotaAsync(
            tenantId,
            QuotaType.ApiRequestsPerMinute,
            1,
            context.RequestAborted);

        if (!allowed)
        {
            logger.LogWarning("API rate limit exceeded for tenant {TenantId}", tenantId);
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = "You have exceeded your API request quota. Please try again later.",
                retryAfter = 60
            });
            return;
        }

        // Increment usage
        await quotaTracker.IncrementUsageAsync(
            tenantId,
            QuotaType.ApiRequestsPerMinute,
            1,
            context.RequestAborted);

        await next(context);
    }
}

public static class QuotaEnforcementMiddlewareExtensions
{
    public static IApplicationBuilder UseQuotaEnforcement(this IApplicationBuilder app)
    {
        return app.UseMiddleware<QuotaEnforcementMiddleware>();
    }
}
```

---

### D. Rule Orchestrator Integration

```csharp
// Modify src/Muonroi.RuleEngine.Core/RuleOrchestrator.cs
public sealed class RuleOrchestrator<TContext>
{
    private readonly ITenantQuotaTracker? _quotaTracker;

    public RuleOrchestrator(
        IEnumerable<IRule<TContext>> rules,
        IEnumerable<IHookHandler<TContext>> hooks,
        ILogger<RuleOrchestrator<TContext>>? logger,
        IEnumerable<IRuleEventListener<TContext>>? listeners = null,
        ITenantQuotaTracker? quotaTracker = null) // NEW
    {
        // ... existing code ...
        _quotaTracker = quotaTracker;
    }

    public async Task<FactBag> ExecuteAsync(
        TContext context,
        HookPoint? filterPoint = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId(context);

        // Check concurrent execution quota
        if (_quotaTracker != null && !string.IsNullOrWhiteSpace(tenantId))
        {
            var allowed = await _quotaTracker.CheckQuotaAsync(
                tenantId,
                QuotaType.ConcurrentExecutions,
                1,
                cancellationToken);

            if (!allowed)
            {
                throw new QuotaExceededException(
                    $"Concurrent execution quota exceeded for tenant {tenantId}");
            }

            await _quotaTracker.IncrementUsageAsync(
                tenantId,
                QuotaType.ConcurrentExecutions,
                1,
                cancellationToken);
        }

        try
        {
            // ... existing rule execution logic ...

            // Track rule execution quota
            if (_quotaTracker != null && !string.IsNullOrWhiteSpace(tenantId))
            {
                await _quotaTracker.IncrementUsageAsync(
                    tenantId,
                    QuotaType.RuleExecutionsPerDay,
                    _rules.Count,
                    cancellationToken);
            }

            return facts;
        }
        finally
        {
            // Decrement concurrent execution counter
            if (_quotaTracker != null && !string.IsNullOrWhiteSpace(tenantId))
            {
                await _quotaTracker.IncrementUsageAsync(
                    tenantId,
                    QuotaType.ConcurrentExecutions,
                    -1,
                    cancellationToken);
            }
        }
    }

    private string? ResolveTenantId(TContext context)
    {
        if (context is ITenantScoped scoped)
            return scoped.TenantId;

        return TenantContext.CurrentTenantId;
    }
}

public class QuotaExceededException(string message) : Exception(message);
```

---

### E. Quota Dashboard API

```csharp
// src/Muonroi.BuildingBlock/External/Controller/TenantQuotaController.cs
namespace Muonroi.BuildingBlock.External.Controller;

[ApiController]
[Route("api/v1/tenants/{tenantId}/quotas")]
[Authorize]
public class TenantQuotaController(
    ITenantQuotaTracker quotaTracker,
    ITenantQuotaStore quotaStore) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(string tenantId, CancellationToken ct)
    {
        // Security: Ensure user can only view their own tenant
        if (TenantContext.CurrentTenantId != tenantId)
            return Forbid();

        var usage = await quotaTracker.GetUsageAsync(tenantId, ct);
        return Ok(usage);
    }

    [HttpGet("limits")]
    public async Task<IActionResult> GetLimits(string tenantId, CancellationToken ct)
    {
        if (TenantContext.CurrentTenantId != tenantId)
            return Forbid();

        var quota = await quotaStore.GetQuotaAsync(tenantId, ct);
        return Ok(quota);
    }

    [HttpPut("limits")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateLimits(
        string tenantId,
        [FromBody] TenantQuota quota,
        CancellationToken ct)
    {
        await quotaStore.SaveQuotaAsync(tenantId, quota, ct);
        return Ok(quota);
    }

    [HttpPost("upgrade")]
    public async Task<IActionResult> UpgradeTier(
        string tenantId,
        [FromBody] UpgradeRequest request,
        CancellationToken ct)
    {
        if (TenantContext.CurrentTenantId != tenantId)
            return Forbid();

        var newQuota = request.Tier switch
        {
            TenantTier.Starter => TenantQuotaPresets.Starter,
            TenantTier.Professional => TenantQuotaPresets.Professional,
            TenantTier.Enterprise => TenantQuotaPresets.Enterprise,
            _ => throw new InvalidOperationException("Invalid tier")
        };

        newQuota.TenantId = tenantId;
        await quotaStore.SaveQuotaAsync(tenantId, newQuota, ct);

        return Ok(new { message = $"Upgraded to {request.Tier} tier", quota = newQuota });
    }
}

public record UpgradeRequest(TenantTier Tier);
```

---

### F. Testing

```csharp
// tests/Muonroi.BuildingBlock.Tests/Tenancy/TenantQuotaTests.cs
public class TenantQuotaTests
{
    [Fact]
    public async Task CheckQuota_WhenUnderLimit_ReturnsTrue()
    {
        // Arrange
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var store = new InMemoryQuotaStore();
        await store.SaveQuotaAsync("tenant1", TenantQuotaPresets.Free, default);

        var tracker = new TenantQuotaTracker(cache, store, NullLogger<TenantQuotaTracker>.Instance);

        // Act
        var allowed = await tracker.CheckQuotaAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckQuota_WhenOverLimit_ReturnsFalse()
    {
        // Arrange
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var store = new InMemoryQuotaStore();
        var freeQuota = TenantQuotaPresets.Free;
        freeQuota.MaxApiRequestsPerMinute = 5;
        await store.SaveQuotaAsync("tenant1", freeQuota, default);

        var tracker = new TenantQuotaTracker(cache, store, NullLogger<TenantQuotaTracker>.Instance);

        // Use up quota
        for (int i = 0; i < 5; i++)
        {
            await tracker.IncrementUsageAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1);
        }

        // Act
        var allowed = await tracker.CheckQuotaAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task RuleOrchestrator_WhenQuotaExceeded_ThrowsException()
    {
        // Arrange
        var quota = TenantQuotaPresets.Free;
        quota.MaxConcurrentExecutions = 1;

        var store = new InMemoryQuotaStore();
        await store.SaveQuotaAsync("tenant1", quota, default);

        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var tracker = new TenantQuotaTracker(cache, store, NullLogger<TenantQuotaTracker>.Instance);

        TenantContext.CurrentTenantId = "tenant1";

        var orchestrator = new RuleOrchestrator<TestContext>(
            [],
            [],
            NullLogger<RuleOrchestrator<TestContext>>.Instance,
            null,
            tracker);

        // Occupy the quota
        await tracker.IncrementUsageAsync("tenant1", QuotaType.ConcurrentExecutions, 1);

        // Act & Assert
        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            orchestrator.ExecuteAsync(new TestContext(), cancellationToken: default));
    }
}
```

---

## 📊 Success Criteria

- [ ] All quota types enforced correctly
- [ ] Middleware blocks over-quota requests
- [ ] Dashboard API returns accurate usage
- [ ] No quota leaks (counters always decremented)
- [ ] Performance: <5ms overhead per request
- [ ] Test coverage: >90%

---

## 📚 Documentation Deliverables

1. `docs/multi-tenant-quota-guide.md` - Quota management guide
2. `docs/quota-api-reference.md` - API documentation
3. Dashboard UI mockups
4. Migration guide for existing tenants

---

# Strategy 4: Kubernetes Helm Charts

## 🎯 Objective
Create production-ready Helm charts for deploying rule engine to Kubernetes.

## 📅 Timeline: Week 9-12 (May 1-28)

---

## 📦 Helm Chart Structure

```
k8s/helm/muonroi-rule-engine/
├── Chart.yaml
├── values.yaml
├── values-dev.yaml
├── values-staging.yaml
├── values-production.yaml
├── templates/
│   ├── NOTES.txt
│   ├── _helpers.tpl
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── ingress.yaml
│   ├── configmap.yaml
│   ├── secret.yaml
│   ├── hpa.yaml                    # Horizontal Pod Autoscaler
│   ├── pdb.yaml                    # Pod Disruption Budget
│   ├── networkpolicy.yaml
│   ├── servicemonitor.yaml         # Prometheus ServiceMonitor
│   └── tests/
│       └── test-connection.yaml
├── dashboards/
│   └── rule-engine-dashboard.json  # Grafana dashboard
└── README.md
```

---

## 📦 Implementation

### A. Chart.yaml

```yaml
# k8s/helm/muonroi-rule-engine/Chart.yaml
apiVersion: v2
name: muonroi-rule-engine
description: A Helm chart for Muonroi Rule Engine with decision tables and FEEL support
type: application
version: 1.0.0
appVersion: "2.0.0"
keywords:
  - rule-engine
  - decision-table
  - dmn
  - feel
  - business-rules
home: https://github.com/muonroi/MuonroiBuildingBlock
sources:
  - https://github.com/muonroi/MuonroiBuildingBlock
maintainers:
  - name: Muonroi Team
    email: support@muonroi.com
dependencies:
  - name: postgresql
    version: "12.x.x"
    repository: https://charts.bitnami.com/bitnami
    condition: postgresql.enabled
  - name: redis
    version: "17.x.x"
    repository: https://charts.bitnami.com/bitnami
    condition: redis.enabled
```

---

### B. values.yaml

```yaml
# k8s/helm/muonroi-rule-engine/values.yaml

# Global settings
global:
  imageRegistry: ""
  imagePullSecrets: []

# Image settings
image:
  registry: docker.io
  repository: muonroi/rule-engine
  tag: "2.0.0"
  pullPolicy: IfNotPresent

# Replica count
replicaCount: 3

# Service settings
service:
  type: ClusterIP
  port: 80
  targetPort: 8080
  annotations: {}

# Ingress settings
ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/rate-limit: "100"
  hosts:
    - host: rule-engine.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: rule-engine-tls
      hosts:
        - rule-engine.example.com

# Resource limits
resources:
  limits:
    cpu: 1000m
    memory: 1Gi
  requests:
    cpu: 500m
    memory: 512Mi

# Autoscaling
autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 80

# Pod Disruption Budget
podDisruptionBudget:
  enabled: true
  minAvailable: 2

# Health checks
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 3

# Application settings
config:
  # License settings
  license:
    mode: "Online"
    tier: "Enterprise"
    endpoint: "https://license.muonroi.com/api"

  # Database settings (if not using dependency)
  database:
    type: "PostgreSQL"
    host: ""
    port: 5432
    name: "rule_engine"
    username: ""
    password: ""

  # Redis settings (if not using dependency)
  redis:
    enabled: true
    host: ""
    port: 6379
    password: ""

  # Multi-tenant settings
  multiTenant:
    enabled: true
    requireTenantClaim: true

  # Quota settings
  quota:
    enabled: true
    defaultTier: "Free"

  # OpenTelemetry settings
  telemetry:
    enabled: true
    otlpEndpoint: "http://otel-collector:4317"
    serviceName: "rule-engine"

# PostgreSQL dependency
postgresql:
  enabled: true
  auth:
    username: ruleengine
    password: changeme
    database: rule_engine
  primary:
    persistence:
      enabled: true
      size: 10Gi

# Redis dependency
redis:
  enabled: true
  auth:
    enabled: true
    password: changeme
  master:
    persistence:
      enabled: true
      size: 5Gi

# Monitoring
monitoring:
  enabled: true
  serviceMonitor:
    enabled: true
    interval: 30s
    scrapeTimeout: 10s

# Security
securityContext:
  runAsNonRoot: true
  runAsUser: 1000
  fsGroup: 1000
  capabilities:
    drop:
      - ALL

# Network policy
networkPolicy:
  enabled: true
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: ingress-nginx
      ports:
        - protocol: TCP
          port: 8080
  egress:
    - to:
        - namespaceSelector: {}
      ports:
        - protocol: TCP
          port: 5432  # PostgreSQL
        - protocol: TCP
          port: 6379  # Redis
        - protocol: TCP
          port: 443   # HTTPS

# Affinity rules
affinity:
  podAntiAffinity:
    preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 100
        podAffinityTerm:
          labelSelector:
            matchExpressions:
              - key: app.kubernetes.io/name
                operator: In
                values:
                  - muonroi-rule-engine
          topologyKey: kubernetes.io/hostname
```

---

### C. Deployment Template

```yaml
# k8s/helm/muonroi-rule-engine/templates/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "muonroi-rule-engine.fullname" . }}
  labels:
    {{- include "muonroi-rule-engine.labels" . | nindent 4 }}
spec:
  {{- if not .Values.autoscaling.enabled }}
  replicas: {{ .Values.replicaCount }}
  {{- end }}
  selector:
    matchLabels:
      {{- include "muonroi-rule-engine.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      annotations:
        checksum/config: {{ include (print $.Template.BasePath "/configmap.yaml") . | sha256sum }}
        checksum/secret: {{ include (print $.Template.BasePath "/secret.yaml") . | sha256sum }}
      labels:
        {{- include "muonroi-rule-engine.selectorLabels" . | nindent 8 }}
    spec:
      {{- with .Values.global.imagePullSecrets }}
      imagePullSecrets:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      securityContext:
        {{- toYaml .Values.securityContext | nindent 8 }}
      containers:
        - name: {{ .Chart.Name }}
          image: "{{ .Values.image.registry }}/{{ .Values.image.repository }}:{{ .Values.image.tag | default .Chart.AppVersion }}"
          imagePullPolicy: {{ .Values.image.pullPolicy }}
          ports:
            - name: http
              containerPort: 8080
              protocol: TCP
          livenessProbe:
            {{- toYaml .Values.livenessProbe | nindent 12 }}
          readinessProbe:
            {{- toYaml .Values.readinessProbe | nindent 12 }}
          resources:
            {{- toYaml .Values.resources | nindent 12 }}
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ASPNETCORE_URLS
              value: "http://+:8080"
            - name: LicenseConfigs__Mode
              value: {{ .Values.config.license.mode | quote }}
            - name: LicenseConfigs__Online__Endpoint
              value: {{ .Values.config.license.endpoint | quote }}
            - name: DatabaseConfigs__DbType
              value: {{ .Values.config.database.type | quote }}
            - name: DatabaseConfigs__ConnectionStrings__PostgreSqlConnectionString
              valueFrom:
                secretKeyRef:
                  name: {{ include "muonroi-rule-engine.fullname" . }}
                  key: database-connection-string
            - name: RedisConfigs__Host
              value: {{ .Values.redis.enabled | ternary (printf "%s-redis-master" (include "muonroi-rule-engine.fullname" .)) .Values.config.redis.host | quote }}
            - name: RedisConfigs__Port
              value: {{ .Values.redis.enabled | ternary "6379" .Values.config.redis.port | quote }}
            - name: RedisConfigs__Password
              valueFrom:
                secretKeyRef:
                  name: {{ include "muonroi-rule-engine.fullname" . }}
                  key: redis-password
            - name: MultiTenantConfigs__Enabled
              value: {{ .Values.config.multiTenant.enabled | quote }}
            - name: OpenTelemetry__OtlpEndpoint
              value: {{ .Values.config.telemetry.otlpEndpoint | quote }}
            - name: OpenTelemetry__ServiceName
              value: {{ .Values.config.telemetry.serviceName | quote }}
          volumeMounts:
            - name: config
              mountPath: /app/appsettings.Production.json
              subPath: appsettings.Production.json
      volumes:
        - name: config
          configMap:
            name: {{ include "muonroi-rule-engine.fullname" . }}
      {{- with .Values.affinity }}
      affinity:
        {{- toYaml . | nindent 8 }}
      {{- end }}
```

---

### D. Horizontal Pod Autoscaler

```yaml
# k8s/helm/muonroi-rule-engine/templates/hpa.yaml
{{- if .Values.autoscaling.enabled }}
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: {{ include "muonroi-rule-engine.fullname" . }}
  labels:
    {{- include "muonroi-rule-engine.labels" . | nindent 4 }}
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: {{ include "muonroi-rule-engine.fullname" . }}
  minReplicas: {{ .Values.autoscaling.minReplicas }}
  maxReplicas: {{ .Values.autoscaling.maxReplicas }}
  metrics:
    {{- if .Values.autoscaling.targetCPUUtilizationPercentage }}
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: {{ .Values.autoscaling.targetCPUUtilizationPercentage }}
    {{- end }}
    {{- if .Values.autoscaling.targetMemoryUtilizationPercentage }}
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: {{ .Values.autoscaling.targetMemoryUtilizationPercentage }}
    {{- end }}
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
        - type: Percent
          value: 50
          periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
        - type: Percent
          value: 100
          periodSeconds: 30
        - type: Pods
          value: 4
          periodSeconds: 30
      selectPolicy: Max
{{- end }}
```

---

### E. ServiceMonitor for Prometheus

```yaml
# k8s/helm/muonroi-rule-engine/templates/servicemonitor.yaml
{{- if .Values.monitoring.serviceMonitor.enabled }}
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: {{ include "muonroi-rule-engine.fullname" . }}
  labels:
    {{- include "muonroi-rule-engine.labels" . | nindent 4 }}
spec:
  selector:
    matchLabels:
      {{- include "muonroi-rule-engine.selectorLabels" . | nindent 6 }}
  endpoints:
    - port: http
      path: /metrics
      interval: {{ .Values.monitoring.serviceMonitor.interval }}
      scrapeTimeout: {{ .Values.monitoring.serviceMonitor.scrapeTimeout }}
{{- end }}
```

---

### F. Installation Commands

```bash
# Install with default values
helm install rule-engine ./k8s/helm/muonroi-rule-engine

# Install in specific namespace
helm install rule-engine ./k8s/helm/muonroi-rule-engine -n rule-engine --create-namespace

# Install with custom values
helm install rule-engine ./k8s/helm/muonroi-rule-engine \
  --values k8s/helm/muonroi-rule-engine/values-production.yaml

# Upgrade existing release
helm upgrade rule-engine ./k8s/helm/muonroi-rule-engine

# Rollback to previous version
helm rollback rule-engine

# Uninstall
helm uninstall rule-engine
```

---

## 📊 Success Criteria

- [ ] Chart installs successfully on K8s 1.28+
- [ ] All health checks pass
- [ ] Autoscaling works (scales up under load)
- [ ] Pod disruption budget prevents downtime
- [ ] Monitoring integration works (Prometheus/Grafana)
- [ ] Helm tests pass (`helm test rule-engine`)
- [ ] Documentation complete

---

## 📚 Documentation Deliverables

1. `k8s/helm/muonroi-rule-engine/README.md` - Installation guide
2. `docs/kubernetes-deployment-guide.md` - Production deployment guide
3. Grafana dashboard JSON
4. Troubleshooting guide

---

# 📅 OVERALL TIMELINE SUMMARY

```
┌──────────────────────────────────────────────────────────────────┐
│                    Q1 2026 ROADMAP                               │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  MARCH                 APRIL                  MAY                │
│  Week 1-4              Week 5-8               Week 9-12          │
│  ┌───────────────┐    ┌───────────────┐      ┌───────────────┐ │
│  │ S1: Decision  │    │ S2: Full DMN  │      │ S4: Helm      │ │
│  │     Table     │    │     FEEL      │      │    Charts     │ │
│  │   Designer    │    │  Compliance   │      │               │ │
│  │               │    ├───────────────┤      │               │ │
│  │               │    │ S3: Quota     │      │               │ │
│  │               │    │   Management  │      │               │ │
│  └───────────────┘    └───────────────┘      └───────────────┘ │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘

Legend:
S1 = Strategy 1: Visual Decision Table Designer
S2 = Strategy 2: Full DMN FEEL Compliance
S3 = Strategy 3: Multi-Tenant Quota Management
S4 = Strategy 4: Kubernetes Helm Charts
```

---

# 🎯 KEY MILESTONES

| Week | Date | Milestone |
|------|------|-----------|
| 2 | Mar 15 | Decision Table Designer Beta |
| 4 | Mar 29 | Decision Table Designer GA |
| 6 | Apr 12 | Full FEEL Parser Complete |
| 8 | Apr 26 | DMN 1.3 Compliance Certified |
| 10 | May 10 | Quota Management Beta |
| 12 | May 28 | All Features GA + Helm Charts Ready |

---

# 📊 RESOURCE ALLOCATION

## Team Structure (Recommended)

- **1 Full-stack Developer** - Decision Table Designer (Weeks 1-4)
- **1 Backend Developer** - FEEL Parser & Functions (Weeks 5-8)
- **1 Backend Developer** - Quota Management (Weeks 3-8)
- **1 DevOps Engineer** - Helm Charts (Weeks 9-12)
- **1 QA Engineer** - Testing & Validation (Ongoing)
- **1 Technical Writer** - Documentation (Ongoing)

## Estimated Effort

- **Strategy 1:** 160 hours (4 weeks × 40 hours)
- **Strategy 2:** 160 hours (4 weeks × 40 hours)
- **Strategy 3:** 120 hours (3 weeks × 40 hours)
- **Strategy 4:** 80 hours (2 weeks × 40 hours)
- **Testing & Docs:** 80 hours (ongoing)
- **Total:** ~600 hours (3.75 person-months)

---

# ✅ SUCCESS METRICS

| Metric | Target | Measure |
|--------|--------|---------|
| Feature Completion | 100% | All 4 strategies delivered |
| Test Coverage | >90% | Code coverage report |
| Documentation | 100% | All docs published |
| Performance | <10ms | Rule evaluation latency |
| Reliability | 99.9% | Uptime SLA |
| Adoption | 50+ | Enterprise customers using new features |

---

This roadmap is saved to: `D:\Personal\Project\MuonroiBuildingBlock\ROADMAP_Q1_2026.md`
