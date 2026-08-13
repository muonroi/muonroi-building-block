import re

file_path = 'src/Muonroi.RuleEngine.DecisionTable/Feel/FeelEvaluator.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace statements
content = re.sub(
    r'throw new MInternalException\((.*?)\);',
    r'MGuard.State(false, \1);',
    content
)

# Replace expressions
content = content.replace(
    '_ => throw new MInternalException($"Unsupported function \'{name}\'.", "FEEL_PARSE_ERROR")',
    '_ => MGuard.Fail<object?>($"Unsupported function \'{name}\'.", "FEEL_PARSE_ERROR")'
)

content = content.replace(
    '_ => throw new MInternalException($"Unsupported token \'{c}\'.", "FEEL_PARSE_ERROR")',
    '_ => MGuard.Fail<Token>($"Unsupported token \'{c}\'.", "FEEL_PARSE_ERROR")'
)

with open(file_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)
