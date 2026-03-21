# Workflow Patterns

## Sequential Workflows

Breaking complex tasks into clear, sequential steps helps organize work effectively. Give Claude an overview of the process towards the beginning of SKILL.md.

Example structure for a PDF form task:
1. Analyze the form (run analyze_form.py)
2. Create field mapping (edit fields.json)
3. Validate mapping (run validate_fields.py)
4. Fill the form (run fill_form.py)
5. Verify output (run verify_output.py)

## Conditional Workflows

When tasks involve branching logic, guide Claude through decision points:

1. Determine the modification type:
   - Creating new content? → Follow "Creation workflow"
   - Editing existing content? → Follow "Editing workflow"

2. Creation workflow: [steps]
3. Editing workflow: [steps]
