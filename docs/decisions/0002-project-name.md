# ADR 0002: Project name Drawplate

- **Status:** Accepted (31 Oct 2026).
- **Context:** The tool needed a name free on GitHub, major package registries, and without an existing software brand. Candidates were screened against GitHub handles/repos, npm, PyPI, crates.io, Homebrew, Scoop, and Ubuntu package names.
- **Decision:** **Drawplate** — command `drawplate`, environment prefix `DRAWPLATE_`, namespaces `Drawplate.*`.
- **Rationale:** A drawplate is a jeweller’s hardened steel plate with graded holes; wire is pulled through ever smaller holes to a precise size without losing strength. Re-encoding does the same to a video. The name is distinctive, easy to spell, and was free on the checked registries.
- **Alternatives considered:** Gracilis, Contrahere, Pellicle, Filmpress, Wiredraw (rejected for handle clashes, medical associations, or weaker fit).
- **Consequences:** Repo, binary, installers, and docs all use Drawplate / `drawplate`. An optional installer alias for the old `vneat` name was left open and is not required for v1.
