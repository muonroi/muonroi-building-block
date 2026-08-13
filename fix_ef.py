import re
import glob

# For all C# files in Muonroi.RuleEngine.EntityFrameworkCore and Muonroi.RuleEngine.Proliferation
files = glob.glob('src/Muonroi.RuleEngine.EntityFrameworkCore/**/*.cs', recursive=True)
files += glob.glob('src/Muonroi.RuleEngine.Proliferation/**/*.cs', recursive=True)

for file_path in files:
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # MInternalException
    content = re.sub(
        r'throw new MInternalException\((.*?)\);',
        r'MGuard.State(false, \1);',
        content
    )
    
    # ?? throw new MNotFoundException(entity, key)
    # Becomes MGuard.Found(obj, entity, key) -> wait, ?? throw new ... is an expression.
    # It usually looks like: var obj = await GetAsync() ?? throw new MNotFoundException("Entity", key);
    # To use MGuard.Found, it would be: var obj = MGuard.Found(await GetAsync(), "Entity", key);
    # Actually, we can use MGuard.Found(...) in place, but doing it with regex is hard.
    # What if we add a Fail for MNotFoundException? Or just do manual replacements for MNotFoundException.
    
    with open(file_path, 'w', encoding='utf-8', newline='') as f:
        f.write(content)
