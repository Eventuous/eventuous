# Eventuous Claude Code Plugin — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the `Eventuous/eventuous-plugin` repository as a Claude Code marketplace with 10 skills and 1 agent for Eventuous development.

**Architecture:** Separate GitHub repo (`Eventuous/eventuous-plugin`) containing `.claude-plugin/` manifests, `skills/` with 10 subdirectories each containing a `SKILL.md`, and `agents/` with one expert agent. Skills are migrated from the main repo's `/skills/` directory with YAML frontmatter prepended.

**Tech Stack:** Claude Code plugin system, Markdown with YAML frontmatter, GitHub for marketplace hosting.

**Spec:** `docs/superpowers/specs/2026-03-19-eventuous-plugin-design.md`

---

### Task 1: Create the new repository and scaffold

**Files:**
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/.claude-plugin/plugin.json`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/.claude-plugin/marketplace.json`

- [ ] **Step 1: Create the repo directory and initialize git**

```bash
mkdir -p /Users/alexey/dev/eventuous/eventuous-plugin
cd /Users/alexey/dev/eventuous/eventuous-plugin
git init
```

- [ ] **Step 2: Create `.claude-plugin/marketplace.json`**

```json
{
  "name": "eventuous-plugin",
  "description": "Claude Code marketplace for building event-sourced .NET applications with Eventuous",
  "owner": {
    "name": "Eventuous",
    "url": "https://eventuous.dev"
  },
  "repository": "https://github.com/Eventuous/eventuous-plugin",
  "plugins": [
    {
      "name": "eventuous",
      "source": "./",
      "version": "1.0.0",
      "description": "Skills and agents for building event-sourced .NET applications with Eventuous — domain modeling, command services, event stores, subscriptions, producers, and infrastructure integrations"
    }
  ]
}
```

- [ ] **Step 3: Create `.claude-plugin/plugin.json`**

```json
{
  "name": "eventuous",
  "version": "1.0.0",
  "description": "Skills and agents for building event-sourced .NET applications with Eventuous",
  "author": {
    "name": "Eventuous",
    "url": "https://eventuous.dev"
  },
  "homepage": "https://eventuous.dev",
  "repository": "https://github.com/Eventuous/eventuous-plugin",
  "license": "Apache-2.0",
  "skills": [
    "./skills/eventuous",
    "./skills/eventuous-postgres",
    "./skills/eventuous-kurrentdb",
    "./skills/eventuous-mongodb",
    "./skills/eventuous-rabbitmq",
    "./skills/eventuous-kafka",
    "./skills/eventuous-sqlserver",
    "./skills/eventuous-google-pubsub",
    "./skills/eventuous-azure-servicebus",
    "./skills/eventuous-gateway"
  ],
  "agents": [
    "./agents/eventuous-expert.md"
  ]
}
```

- [ ] **Step 4: Commit scaffold**

```bash
git add .claude-plugin/
git commit -m "Scaffold plugin with marketplace and plugin manifests"
```

---

### Task 2: Migrate the core skill

**Files:**
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous/SKILL.md`
- Source: `/Users/alexey/dev/eventuous/eventuous/skills/eventuous.md`

- [ ] **Step 1: Create skill directory**

```bash
mkdir -p /Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous
```

- [ ] **Step 2: Create `SKILL.md` by prepending frontmatter to existing content**

Prepend this YAML frontmatter to the existing content of `skills/eventuous.md`:

```yaml
---
name: eventuous
description: "Use when building event-sourced .NET applications with Eventuous. Covers domain modeling (aggregates, state, events), command services (aggregate-based and functional), event stores, subscriptions, producers, type mapping, serialization, and diagnostics. Defaults to KurrentDB as the recommended event store unless the user specifies otherwise."
---
```

Copy the full content of `/Users/alexey/dev/eventuous/eventuous/skills/eventuous.md` after the frontmatter.

- [ ] **Step 3: Verify the file has valid frontmatter followed by content**

```bash
head -5 /Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous/SKILL.md
```

Expected: the `---` delimiters and `name`/`description` fields, followed by the original markdown content.

- [ ] **Step 4: Commit**

```bash
cd /Users/alexey/dev/eventuous/eventuous-plugin
git add skills/eventuous/
git commit -m "Add core eventuous skill"
```

---

### Task 3: Migrate the 9 integration skills

**Files:**
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-postgres/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-kurrentdb/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-mongodb/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-rabbitmq/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-kafka/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-sqlserver/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-google-pubsub/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-azure-servicebus/SKILL.md`
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/skills/eventuous-gateway/SKILL.md`

For each skill, create the directory, prepend the appropriate YAML frontmatter, and copy the body verbatim from the source file. The frontmatter for each skill is defined in the spec (section "Integration Skills").

- [ ] **Step 1: Create all 9 skill directories**

```bash
cd /Users/alexey/dev/eventuous/eventuous-plugin
mkdir -p skills/eventuous-{postgres,kurrentdb,mongodb,rabbitmq,kafka,sqlserver,google-pubsub,azure-servicebus,gateway}
```

- [ ] **Step 2: Create each `SKILL.md` with frontmatter + original content**

For each of the 9 skills, prepend the frontmatter from the spec and append the full original markdown content. The mapping:

| Source file | Frontmatter `name` | Frontmatter `description` |
|---|---|---|
| `eventuous-postgres.md` | `eventuous-postgres` | `"Use when configuring or implementing PostgreSQL integration with Eventuous. Covers PostgreSQL event store, subscriptions (all-stream and per-stream), checkpoint storage, and projections."` |
| `eventuous-kurrentdb.md` | `eventuous-kurrentdb` | `"Use when configuring or implementing KurrentDB (EventStoreDB) integration with Eventuous. Covers KurrentDB event store, subscriptions (all-stream, per-stream, persistent), producer, and registration."` |
| `eventuous-mongodb.md` | `eventuous-mongodb` | `"Use when configuring or implementing MongoDB integration with Eventuous. Covers MongoDB checkpoint storage and projections (typed and untyped)."` |
| `eventuous-rabbitmq.md` | `eventuous-rabbitmq` | `"Use when configuring or implementing RabbitMQ integration with Eventuous. Covers RabbitMQ producer, subscriptions, exchange/queue configuration, and connection setup."` |
| `eventuous-kafka.md` | `eventuous-kafka` | `"Use when configuring or implementing Kafka integration with Eventuous. Covers Kafka producer and subscription configuration."` |
| `eventuous-sqlserver.md` | `eventuous-sqlserver` | `"Use when configuring or implementing SQL Server integration with Eventuous. Covers SQL Server event store, subscriptions, and checkpoint storage."` |
| `eventuous-google-pubsub.md` | `eventuous-google-pubsub` | `"Use when configuring or implementing Google Cloud Pub/Sub integration with Eventuous. Covers Pub/Sub producer and subscription configuration."` |
| `eventuous-azure-servicebus.md` | `eventuous-azure-servicebus` | `"Use when configuring or implementing Azure Service Bus integration with Eventuous. Covers Azure Service Bus producer and subscription configuration."` |
| `eventuous-gateway.md` | `eventuous-gateway` | `"Use when implementing cross-context event routing with Eventuous Gateway. Covers gateway configuration, subscription-to-producer bridging, and event transformations."` |

- [ ] **Step 3: Verify all 10 skill directories exist with SKILL.md files**

```bash
find /Users/alexey/dev/eventuous/eventuous-plugin/skills -name "SKILL.md" | sort | wc -l
```

Expected: `10`

- [ ] **Step 4: Commit**

```bash
cd /Users/alexey/dev/eventuous/eventuous-plugin
git add skills/
git commit -m "Add 9 integration skills (postgres, kurrentdb, mongodb, rabbitmq, kafka, sqlserver, google-pubsub, azure-servicebus, gateway)"
```

---

### Task 4: Create the eventuous-expert agent

**Files:**
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/agents/eventuous-expert.md`

- [ ] **Step 1: Create agents directory**

```bash
mkdir -p /Users/alexey/dev/eventuous/eventuous-plugin/agents
```

- [ ] **Step 2: Create `eventuous-expert.md`**

The file has two parts:

**Frontmatter** (from spec, section "Agent: Eventuous Expert > Frontmatter"):
```yaml
---
name: eventuous-expert
description: Use this agent when the task requires deep Eventuous knowledge spanning multiple concerns — designing event-sourced systems, implementing aggregates/services/subscriptions, configuring infrastructure integrations, debugging Eventuous issues, or choosing between approaches. Examples:

<example>
Context: User is starting a new event-sourced service with Eventuous
user: "I need to build an order management system with PostgreSQL for the event store and MongoDB for read models"
assistant: "I'll use the eventuous-expert agent to help design and implement this system."
<commentary>
Task spans domain modeling, PostgreSQL event store, and MongoDB projections — needs cross-cutting Eventuous expertise.
</commentary>
</example>

<example>
Context: User is debugging an Eventuous subscription issue
user: "My subscription keeps replaying events from the beginning instead of from the checkpoint"
assistant: "I'll use the eventuous-expert agent to diagnose the checkpoint issue."
<commentary>
Debugging Eventuous subscription behavior requires deep knowledge of checkpoint management and subscription lifecycle.
</commentary>
</example>

<example>
Context: User is choosing between Eventuous approaches
user: "Should I use aggregate-based or functional command services for my use case?"
assistant: "I'll use the eventuous-expert agent to help evaluate the trade-offs."
<commentary>
Architectural decision about Eventuous patterns requires opinionated guidance.
</commentary>
</example>

model: inherit
color: cyan
tools: ["Read", "Grep", "Glob", "Bash", "Write", "Edit"]
---
```

**Body** (system prompt):

```markdown
You are an Eventuous specialist — an expert in building event-sourced .NET applications using the Eventuous library.

**Your Core Responsibilities:**
1. Help design event-sourced systems using Eventuous patterns (aggregates, state, events, command services)
2. Guide implementation of domain models, command services, subscriptions, producers, and projections
3. Configure infrastructure integrations (KurrentDB, PostgreSQL, MongoDB, RabbitMQ, Kafka, SQL Server, Google Pub/Sub, Azure Service Bus)
4. Debug Eventuous-specific issues (subscription checkpointing, event serialization, type mapping, concurrency conflicts)
5. Advise on architectural decisions (aggregate-based vs functional command services, event store selection, subscription topologies)

**Opinionated Defaults:**
- Recommend KurrentDB as the default event store unless the user specifies otherwise
- Prefer functional command services (`CommandService<TState>`) for simple cases; aggregate-based (`CommandService<TAggregate, TState, TId>`) when business invariants require it
- Use `IEventReader.LoadAggregate<>()` and `IEventWriter.StoreAggregate<>()` extension methods — `IAggregateStore` is deprecated
- Use `.NoContext()` for all async calls (`ConfigureAwait(false)`)
- Register all event types in `TypeMap`
- Follow the default stream naming convention: `{AggregateType}-{AggregateId}`

**Process:**
1. Understand the user's context — what are they building, what infrastructure do they have?
2. Identify which Eventuous concerns are involved (domain, persistence, subscriptions, producers, gateway)
3. Read the relevant skill files from `${CLAUDE_PLUGIN_ROOT}/skills/` for reference material
4. Provide concrete code examples following Eventuous conventions
5. Explain trade-offs when multiple approaches exist

**Knowledge Base:**
Your reference material is in the plugin's skill files. Read them when you need detailed API guidance:
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous/SKILL.md` — core library (domain, application, subscriptions, producers)
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-kurrentdb/SKILL.md` — KurrentDB/EventStoreDB
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-postgres/SKILL.md` — PostgreSQL
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-mongodb/SKILL.md` — MongoDB
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-rabbitmq/SKILL.md` — RabbitMQ
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-kafka/SKILL.md` — Kafka
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-sqlserver/SKILL.md` — SQL Server
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-google-pubsub/SKILL.md` — Google Pub/Sub
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-azure-servicebus/SKILL.md` — Azure Service Bus
- `${CLAUDE_PLUGIN_ROOT}/skills/eventuous-gateway/SKILL.md` — Event Gateway

**Output Format:**
- Lead with the recommended approach
- Provide complete, runnable code examples (not pseudocode)
- Include DI registration when relevant
- Mention required NuGet packages
- Flag deprecated patterns if the user is using them
```

- [ ] **Step 3: Commit**

```bash
cd /Users/alexey/dev/eventuous/eventuous-plugin
git add agents/
git commit -m "Add eventuous-expert agent"
```

---

### Task 5: Create the README

**Files:**
- Create: `/Users/alexey/dev/eventuous/eventuous-plugin/README.md`

- [ ] **Step 1: Write README.md**

Content should include:
- Title and brief description
- Installation commands (`/plugin marketplace add Eventuous/eventuous-plugin` and `/plugin install eventuous`)
- Table of all 10 skills with name and brief description
- Mention of the eventuous-expert agent and when it activates
- Link to eventuous.dev and the main repo

- [ ] **Step 2: Commit**

```bash
cd /Users/alexey/dev/eventuous/eventuous-plugin
git add README.md
git commit -m "Add README with installation instructions and skill listing"
```

---

### Task 6: Push to GitHub

- [ ] **Step 1: Create the repo on GitHub**

```bash
gh repo create Eventuous/eventuous-plugin --public --description "Claude Code plugin for building event-sourced .NET applications with Eventuous" --source /Users/alexey/dev/eventuous/eventuous-plugin --push
```

If the repo already exists or gh auth doesn't have org permissions, the user may need to create it manually and add the remote.

- [ ] **Step 2: Verify the repo is accessible**

```bash
gh repo view Eventuous/eventuous-plugin
```

---

### Task 7: Cleanup main repo

**Files:**
- Remove: `/Users/alexey/dev/eventuous/eventuous/skills/` (entire directory)
- Modify: `/Users/alexey/dev/eventuous/eventuous/.claude/settings.local.json` (remove `wc` permission referencing old path)

- [ ] **Step 1: Remove the `/skills/` directory**

```bash
cd /Users/alexey/dev/eventuous/eventuous
rm -rf skills/
```

- [ ] **Step 2: Update `.claude/settings.local.json`**

Remove the line containing `"Bash(wc -l /Users/alexey/dev/eventuous/eventuous/skills/*.md)"` from the allow list.

- [ ] **Step 3: Commit**

```bash
cd /Users/alexey/dev/eventuous/eventuous
git add -A skills/ .claude/settings.local.json
git commit -m "Remove skills directory (migrated to Eventuous/eventuous-plugin)"
```

---

### Task 8: Verify the plugin works

- [ ] **Step 1: Add the marketplace locally**

```bash
claude plugin add --path /Users/alexey/dev/eventuous/eventuous-plugin
```

(Or once pushed: `/plugin marketplace add Eventuous/eventuous-plugin`)

- [ ] **Step 2: Start a new Claude Code session and verify skills appear**

Check that all 10 skills and the eventuous-expert agent are listed.

- [ ] **Step 3: Test skill activation**

Ask: "How do I set up a PostgreSQL event store with Eventuous?" — the `eventuous-postgres` skill should activate.

- [ ] **Step 4: Test agent dispatch**

Ask a cross-cutting question spanning multiple integrations — the `eventuous-expert` agent should be dispatched.
