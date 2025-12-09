#!/usr/bin/env python3
"""
Script to update React pages to use Menu as a wrapper instead of sibling component.
"""

import re

def update_page(filename):
    with open(filename, 'r') as f:
        content = f.read()
    
    # Pattern 1: Replace the opening structure
    # From: <div className="XXX-layout">\n      <Menu />\n      <div className="XXX-content">
    # To: <Menu>
    content = re.sub(
        r'<div className="[^"]*-layout">\s*<Menu />\s*<div className="[^"]*-content">\s*',
        '<Menu>\n      ',
        content
    )
    
    # Pattern 2: Remove extra closing divs before modals/forms
    # Find patterns like:        </div>\n      </div>\n\n      {showForm
    content = re.sub(
        r'(\s*)</div>\s*</div>\s*\n\s*({(?:showForm|showImportModal|deleteConfirm))',
        r'\1\n\n      \2',
        content
    )
    
    # Pattern 3: Replace final </div> with </Menu>
    content = re.sub(
        r'}\s*\n\s*</div>\s*\n\s*\);\s*\n};',
        r'}\n      </div>\n    </Menu>\n  );\n};',
        content
    )
    
    with open(filename, 'w') as f:
        f.write(content)
    
    print(f"✓ Updated {filename}")

# Update all pages
pages = [
    'src/components/MyContractsPage.tsx',
    'src/components/UsersPage.tsx',
    'src/components/UsersMappingPage.tsx',
    'src/components/PVPage.tsx'
]

for page in pages:
    try:
        update_page(page)
    except Exception as e:
        print(f"✗ Error updating {page}: {e}")
