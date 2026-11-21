# AI Assistant Usage

## Tool used

We used Claude Code (in VS Code) as our main AI coding assistant for this assignment instead of GitHub Copilot. The Copilot instruction, prompt and skill files in .github/ were kept and updated for the new solution structure.

## How it was used

- Claude Code was used to help plan the migration and write code, working through the project in six steps (solution setup, data layer, API, admin app, public site, tests/documentation).
- Design choices such as using sessionStorage for the voting guide and separating the Public.Client project were discussed during planning, and the build, test and tenant isolation results were reviewed after each step before committing.

## Example prompts

- "Read the Assignment 2 specification and tell me what is missing from our plan."
- "Make sure the tenant query filters cannot mix tenants between users, and add tests for it."
- "Check that no voting guide data is sent to the server."

## Productivity

- Large parts of the migration from MVC to Blazor, Web API and Aspire were generated quickly, so we could spend more time on reviewing and testing tenant isolation.
- The AI also wrote automated checks (unit tests and E2E scripts) that we re-ran after every step.

## Problems encountered

- The SQL Server default collation is case-insensitive, so the lowercase slug check did not work at first and had to use a binary collation.
- A query that worked with the in-memory test database failed on SQL Server, so in-memory tests alone were not enough.
- In the admin app, the tenant was first resolved before the user was known, so every account saw no data until the resolution was moved to query time.
- Generated code still had to be reviewed and tested by us; we did not accept changes without checking the results.
