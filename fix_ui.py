with open('Views/Identicon/Index.cshtml', 'r') as f:
    content = f.read()

old_str = 'document.getElementById("safeModeToggle") ? document.getElementById("safeModeToggle").checked : false'
new_str = 'document.getElementById("safeModeSelect") ? document.getElementById("safeModeSelect").value : "None"'

content = content.replace(old_str, new_str)

with open('Views/Identicon/Index.cshtml', 'w') as f:
    f.write(content)
