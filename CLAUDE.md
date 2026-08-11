# CLAUDE.md

Guidance for Claude Code when working in this repository.

Everything that applies to any agent — repo structure, published vs authoring
skills, skill-authoring rules, validation gates, the reference instance, and the
branch/PR workflow — lives in [AGENTS.md](AGENTS.md) so it is written once and
stays in sync across tools. It is imported here:

@AGENTS.md

The rest of this file is the Claude Code-specific part only.

## Plugin marketplace

This repo doubles as a Claude Code plugin marketplace. Add it and install the
plugins with:

```
/plugin marketplace add umbraco/Umbraco-CMS-Implementation-Skills

/plugin install umbraco-cms-content-modelling-skills@umbraco-cms-implementation-marketplace
/plugin install umbraco-cms-implementation-skills@umbraco-cms-implementation-marketplace
```

- **Marketplace name:** `umbraco-cms-implementation-marketplace`.
- **Versions** are kept in sync between `.claude-plugin/marketplace.json` and each
  plugin's `.claude-plugin/plugin.json`. When bumping a plugin version, update both.

These manifests are Claude-specific. Other agents consume the same skills through
the open SKILL.md format instead — see [AGENTS.md](AGENTS.md).

## Source references

Skills are most accurate when the Umbraco source is available as a working directory:

```bash
/add-dir /path/to/Umbraco-CMS
```
