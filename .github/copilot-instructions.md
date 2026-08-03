# Copilot Custom Instructions

## Commit Messages
You must strictly follow the Conventional Commits specification for all generated git commit messages.

Format layout:
<type>(<scope>): <short description in imperative mood>

[optional body describing why the change was made]

[optional footer for breaking changes or tracking IDs]

Strict rules to follow:
1. Allowed <type> tokens: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert.
2. The <scope> is optional and must name the module, project, or component affected in lowercase.
3. The <short description> must be in the imperative present tense (e.g., "add feature", NOT "added feature" or "adds feature").
4. Keep the first line under 50 characters. Do not end it with a period.
5. Use all lowercase for the type, scope, and description line.
6. If a breaking change is present, append an exclamation mark after the type or scope (e.g., feat(api)!:) and prefix the footer line with "BREAKING CHANGE:".
7. Focus on explaining *what* was changed and *why*, rather than *how* it was done.