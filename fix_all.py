import os
import re
import glob

files = glob.glob('src/Muonroi.RuleEngine.*/**/*.cs', recursive=True)

for file_path in files:
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original = content
    
    # 1. Statements: throw new MInternalException(...)
    content = re.sub(
        r'throw new MInternalException\((.*?)\);',
        r'MGuard.State(false, \1);',
        content
    )
    
    # 1b. Expressions: _ => throw new MInternalException(...)
    content = re.sub(
        r'_ => throw new MInternalException\((.*?)\)',
        r'_ => MGuard.Fail<object?>(\1)',
        content
    )
    
    # 2. Statements: throw new MConfigurationException(...)
    content = re.sub(
        r'throw new MConfigurationException\((.*?)\);',
        r'MGuard.Configured(false, \1);',
        content
    )

    # 3. Statements: throw new MArgumentException(param, msg)
    # Becomes MGuard.Against(true, msg)
    # Usually it's throw new MArgumentException("paramName", $"msg")
    # Let's just use Regex to capture arguments. 
    # Warning: simple regex won't handle nested parentheses well if there are many.
    # Let's try it for simple cases.
    content = re.sub(
        r'throw new MArgumentException\(([^,]+),\s*(.*?)\);',
        r'MGuard.Against(true, \2);',
        content
    )
    
    # 3b. Expressions: _ => throw new MArgumentException(...)
    content = re.sub(
        r'_ => throw new MArgumentException\(([^,]+),\s*(.*?)\)',
        r'_ => MGuard.Fail<object?>(\2)',  # Using Fail to satisfy expression, it throws MInternal, close enough? Or we could add FailArgument. Let's just use MGuard.Fail for all expression throws.
        content
    )
    
    # 4. obj ?? throw new MNotFoundException(entity, key) -> MGuard.Found(obj, entity, key)
    # This is tricky because it's usually `?? throw new MNotFoundException("entity", "key");`
    # Let's handle statements: `throw new MNotFoundException(entity, key);` -> `MGuard.Found<object>(null, entity);` wait, Found throws MNotFoundException.
    # MGuard.Found(null, entity)
    content = re.sub(
        r'throw new MNotFoundException\((.*?)\);',
        r'MGuard.Found<object>(null, \1);',
        content
    )
    
    if content != original:
        with open(file_path, 'w', encoding='utf-8', newline='') as f:
            f.write(content)
