import os, re, glob

# 1. Resolve SaleControllers using directives
for path in glob.glob("MEP_Management_System/Controllers/SaleController/*.cs"):
    with open(path, 'r') as f:
        content = f.read()
    
    def repl_controller(m):
        head = m.group(1).strip().split('\n')
        sonn = m.group(2).strip().split('\n')
        lines = set([l.strip() for l in head + sonn if l.strip()])
        lines = sorted([l for l in lines if l.startswith('using')])
        return '\n'.join(lines) + '\n'
        
    new_content = re.sub(r'<<<<<<< HEAD\n(.*?)\n=======\n(.*?)\n>>>>>>> origin/sonnvh\n', repl_controller, content, flags=re.DOTALL)
    with open(path, 'w') as f:
        f.write(new_content)

# 2. Resolve .gitignore
with open(".gitignore", "r") as f: content = f.read()
def repl_gitignore(m):
    head = m.group(1).strip()
    sonn = m.group(2).strip()
    return head + "\n" + sonn + "\n"
new_content = re.sub(r'<<<<<<< HEAD\n(.*?)\n=======\n(.*?)\n>>>>>>> origin/sonnvh\n', repl_gitignore, content, flags=re.DOTALL)
with open(".gitignore", "w") as f: f.write(new_content)

# 3. Resolve Invoice.cs
with open("MEP_Management_System/Models/Invoice.cs", "r") as f: content = f.read()
def repl_invoice(m):
    head = m.group(1).strip()
    return head + "\n"
new_content = re.sub(r'<<<<<<< HEAD\n(.*?)\n=======\n(.*?)\n>>>>>>> origin/sonnvh\n', repl_invoice, content, flags=re.DOTALL)
with open("MEP_Management_System/Models/Invoice.cs", "w") as f: f.write(new_content)

# 4. Resolve AuthController.cs
with open("MEP_Management_System/Controllers/AuthenticationController/AuthController.cs", "r") as f: content = f.read()
def repl_auth(m):
    head = m.group(1).strip()
    sonn = m.group(2).strip()
    return head + "\n\n" + sonn + "\n"
new_content = re.sub(r'<<<<<<< HEAD\n(.*?)\n=======\n(.*?)\n>>>>>>> origin/sonnvh\n', repl_auth, content, flags=re.DOTALL)
with open("MEP_Management_System/Controllers/AuthenticationController/AuthController.cs", "w") as f: f.write(new_content)

