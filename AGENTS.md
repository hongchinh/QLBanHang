
<!-- BEGIN agent-skills rules -->
## Context Loading Protocol

Before planning or implementing any features, load project documentation:

### Step 1 — Load Entry Point
Read `docs/SUMMARY.md` to get the full documentation landscape. This single file contains the project overview and tables listing all detail files with descriptions.

The `Code Standard` section is very important to keep the code consistent, maintainable. Skip if the file doesn't exist. Let documentation guide implementation — if docs conflict with implementation needs, clarify with the user instead of guessing.

### Step 2 — Load on Demand
Based on the current task, use the file tables in `docs/SUMMARY.md` to identify and load only the detail files that are directly relevant. Do NOT load all detail files upfront.

## Question Tool Mandate

`Question Tool` is the common method for asking users questions with interactive options. Always use it when asking a question during task execution. Do not ask questions in plain text unless the interface does not support interactive tools. The table below shows which tool to use for each agent:

| Agent       | Tool                     |
| ----------- | ------------------------ |
| Claude Code | `AskUserQuestion`        |
| OpenCode    | `question`               |
| Gemini CLI  | `ask_user`               |
| Cursor      | `ask questions`          |
| Others      | Based on available tools |

Guidelines:

- Prefer selectable options (2–5 choices) over open-ended text when practical.
- Ask exactly one question per message; do not bundle multiple questions.
- Use open-ended plain-text questions only when the answer genuinely requires free-form input or external context.
- Pause for a full user response when the question requires detailed explanation.
- Never interupt the session/flow, the users should not input a new prompt, only respond to the question asked.

## General Principles

- Follow every step in each workflow skill; do not skip required steps.
- Apply YAGNI, KISS, DRY, SOLID, and the principle of least surprise.
- Ask clarifying questions when documentation is unclear or when critical context is missing.
- Generate timestamps with inline bash commands:
  - Folder name: `` `date +%y%m%d-%H%M` ``
  - Document timestamp: `` `date "+%Y-%m-%d %H:%M:%S"` ``

<!-- END agent-skills rules -->

## User Guide Rule

Every feature change ships its user guide (in-app help, `frontend/src/features/help/content/<key>.md`) in the same task and the same PR. This is part of "done", not a follow-up.

- **New screen or route:** create its help file. Do not add a new route to `HELP_PENDING`.
- **Changed screen** (labels, buttons, fields, steps, rules, messages, shortcuts, permissions): update its help file so every sentence matches the new code, and set `updated` to today.
- **Changed screen whose route is still in `HELP_PENDING`:** write its help file now and remove the route from `HELP_PENDING`.
- **Shared code** (components, services, validators): run `npm run help:stale` (from `frontend/`) and update every help file it lists.
- Use the `help-content` skill. Every bold label must be real on-screen text. Details are in `docs/code-standard/conventions.md` "User Help".
- Before finishing, `npm run test -- src/features/help` passes and `npm run help:stale` prints `Không có cảnh báo.`
- Backend-only or refactor changes that the user cannot see need no help change. Say so in the PR.
