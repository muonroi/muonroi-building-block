import re
import sys

file_path = 'src/Muonroi.RuleEngine.DecisionTable/Import/DecisionTableImporter.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

content = re.sub(
    r'if\s*\(lines\.Length\s*<\s*3\)\s*\{\s*throw new MConfigurationException\(\"Table must contain hit policy, headers and at least one rule\"\);\s*\}',
    r'MGuard.Configured(lines.Length >= 3, "Table must contain hit policy, headers and at least one rule");',
    content
)

content = re.sub(
    r'if\s*\(hitParts\.Length\s*<\s*2\s*\|\|\s*!hitParts\[0\]\.Equals\(\"HitPolicy\",\s*StringComparison\.OrdinalIgnoreCase\)\)\s*\{\s*throw new MConfigurationException\(\"Missing HitPolicy declaration\"\);\s*\}',
    r'MGuard.Configured(hitParts.Length >= 2 && hitParts[0].Equals("HitPolicy", StringComparison.OrdinalIgnoreCase), "Missing HitPolicy declaration");',
    content
)

content = re.sub(
    r'if\s*\(!Enum\.TryParse\(hitParts\[1\],\s*true,\s*out RawHitPolicy policy\)\)\s*\{\s*throw new MConfigurationException\(\$\"Invalid hit policy \{hitParts\[1\]\}\"\);\s*\}',
    r'MGuard.Configured(Enum.TryParse(hitParts[1], true, out RawHitPolicy policy), $"Invalid hit policy {hitParts[1]}");',
    content
)

content = re.sub(
    r'if\s*\(headers\.Length\s*<\s*2\)\s*\{\s*throw new MConfigurationException\(\"Decision table requires at least one input and one output column\"\);\s*\}',
    r'MGuard.Configured(headers.Length >= 2, "Decision table requires at least one input and one output column");',
    content
)

content = re.sub(
    r'if\s*\(cols\.Length\s*!=\s*headers\.Length\)\s*\{\s*throw new MConfigurationException\(\$\"Row \{rowIndex - 1\} has incorrect number of columns\"\);\s*\}',
    r'MGuard.Configured(cols.Length == headers.Length, $"Row {rowIndex - 1} has incorrect number of columns");',
    content
)

with open(file_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)
