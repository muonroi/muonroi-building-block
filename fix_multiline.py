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
        content,
        flags=re.DOTALL
    )
    
    # 2. Statements: throw new MConfigurationException(...)
    content = re.sub(
        r'throw new MConfigurationException\((.*?)\);',
        r'MGuard.Configured(false, \1);',
        content,
        flags=re.DOTALL
    )
    
    # 3. throw new MUnauthorizedException(...) -> MGuard.Authorized(false, ...)
    content = re.sub(
        r'throw new (?:Muonroi\.Core\.Abstractions\.Exceptions\.)?MUnauthorizedException\((.*?)\);',
        r'MGuard.Authorized(false, \1);',
        content,
        flags=re.DOTALL
    )

    if content != original:
        with open(file_path, 'w', encoding='utf-8', newline='') as f:
            f.write(content)
