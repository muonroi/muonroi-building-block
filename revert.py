import re
import glob

files = glob.glob('src/Muonroi.RuleEngine.*/**/*.cs', recursive=True)

for file_path in files:
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # We need to replace:
    # var obj = expression ?? MGuard.Found<object>(null, "entity", key);
    # With:
    # var obj = MGuard.Found(expression, "entity", key);
    
    # Let's just fix it universally by finding `(.*?)\s*\?\?\s*MGuard\.Found<object>\(null, (.*?)\);`
    # and converting to `MGuard.Found(\1, \2);`
    # Wait, some expressions might have `await` which makes it complex.
    # Let's just use regex substitution.
    
    # We will just restore `MGuard.Found<object>(null, ...)` back to `throw new MNotFoundException(...)` first.
    content = re.sub(
        r'MGuard\.Found<object>\(null,\s*(.*?)\)',
        r'throw new MNotFoundException(\1)',
        content
    )

    with open(file_path, 'w', encoding='utf-8', newline='') as f:
        f.write(content)
