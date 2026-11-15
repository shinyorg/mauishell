# Shiny MAUI Shell — Working Notes

Guidance for maintaining this repo. Code lives in `src/`, the published Claude Code skill in
`skills/`, and the public documentation site in a **separate** repo at
`~/Desktop/dev/documentation` (rendered to https://shinylib.net/mauishell).

Shiny MAUI Shell layers ViewModel-first navigation, dialogs, viewmodel lifecycle, and
source-generated routes on top of .NET MAUI Shell.

## Documentation site

The public docs live in a **separate repo**: `~/Desktop/dev/documentation` (Astro / Starlight).

- Feature pages: `src/content/docs/mauishell/*.mdx` (e.g. `navigation.mdx`, `dialogs.mdx`,
  `lifecycle.mdx`, `sourcegen.mdx`, `ai.mdx`).
- Release notes: `src/content/docs/mauishell/release-notes.mdx`.
- Menu (sidebar): `src/sidebar-topics.mjs` — the MAUI Shell node lives under the **App Essentials**
  topic; add/update entries when you add a feature page.

### Required updates for EVERY fix & feature

A change is not "done" until these are in sync:

1. **readme.md** (repo root) — packed into the NuGet package; reflect new/changed behavior.
2. **Skill** (`skills/`) — the agent-facing "how to generate correct code" doc; update the trigger
   keyword list when a new public API / source-gen attribute is introduced.
3. **Docs site** — update the relevant feature page and add a **release note**.

### Release notes

Notes use the `<RN>` component (`import RN from '/src/components/ReleaseNote.astro'`), with
`type="feature|enhancement|fix|chore"` and an optional `breaking` flag. Group under a `## v6`-style
version heading; newest version section stays at the top. Use a `### <version> - TBD` heading for
unreleased work and promote it to a dated heading (`### v6.2.0 - June 4, 2026`) when cutting the
release. Add a `### Migration from <prev>` block for breaking changes.

## Blog posts (only when explicitly requested)

Do **not** write blog posts automatically as part of a fix/feature. Write them **only when the user asks**. When asked to blog a feature, produce **two** posts — first the docs-site version, then adapt it for the personal blog.

### 1. Docs site — `~/Desktop/dev/documentation`

- File: `src/content/docs/blog/YYYY/MM/<slug>.mdx` (current year/month folders; create the month folder if needed).
- Frontmatter:
  ```yaml
  ---
  title: '...'
  description: '...'
  date: YYYY-MM-DD
  authors:
    - allanritchie
  tags:
    - Release        # or Feature, AI, etc.
  ---
  ```
- Body is MDX. Reuse components where relevant, e.g. `import NugetBadge from '/src/components/NugetBadge.astro';` then `<NugetBadge name="Shiny.Maui.Shell" />`.
- Voice: product/release-note tone — what shipped, breaking changes, code samples, how to use it. **No hero image** on this site.

### 2. Personal blog — `~/Desktop/dev/blog` (adapt the docs post)

- File: `src/content/blog/YYYY/MM/<slug>.mdx` (note: `content/blog`, not `content/docs/blog`).
- Frontmatter (different schema — see `src/content.config.ts`):
  ```yaml
  ---
  title: '...'
  description: '...'
  pubDate: 'Mon DD YYYY'                          # e.g. 'Jun 15 2026'
  heroImage: '../../../../assets/<slug>-hero.svg'
  tags: ['Shiny', '.NET MAUI']
  ---
  ```
- Voice: rework the docs post into a personal, first-person narrative ("Here's something that shouldn't be hard but is…", "So I built…") — story/motivation up front, not a dry changelog.
- **Hero image is required.** Create `src/assets/<slug>-hero.svg`:
  - SVG, `viewBox="0 0 1200 630"`, `width="1200" height="630"`.
  - Match the house style: dark navy/indigo gradient background (`#0f172a` → `#1e1b4b`), cyan/green/violet accent gradients, subtle glow filters, the feature name as the headline. Crib an existing one (e.g. `datasync-hero.svg`, `documentdb-orleans-hero.svg`) as a starting template.

## Releasing

`main` is the only long-lived branch. Versions come from git tags, not the repo (no Nerdbank / `version.json`):

- Push a `v7.1.0` (or `v7.1.0-beta1`) tag, or run the **Release** workflow by hand with a version. It builds
  `Build.slnf` with `-p:Version=<tag>`, pushes the packages to NuGet (trusted publishing) and creates a GitHub release
  with the `.nupkg` files attached and generated notes. A `-suffix` makes both prerelease.
- **CI** (`ci.yml`) builds and runs the unit tests on every push / PR to `main`; it publishes nothing.
- Local builds are stamped `0.0.1-local` (`Directory.Build.props`).
- Promote the docs release-note heading from `TBD` to the release date when you tag.
