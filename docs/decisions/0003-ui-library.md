# ADR 0003: UI library cost

- **Status:** Pending measurement; plain renderer remains in use.
- **Context:** R15 implements the terminal UI with the plain ANSI renderer. The original T1.6/S5 spike requires a throwaway project outside the repository and measurements on each CI OS: Native-AOT binary size, median cold-start time over 20 `--version` runs, and trim/AOT warnings, comparing a plain console app with Spectre.Console rendering a table, progress bar, and prompt.
- **Decision:** Do not add a UI package in this plan. If the measured rule would select Spectre.Console, record **"adopt later, owner decides"**; the current product continues to use `PlainRenderer`.
- **Measurement rule:** Adopt only when the size increase is under 6 MB and there are zero AOT or trim warnings. The full-app size budget is the measured shipped binary size plus 10%.
- **Measurements:**

| Runner / RID | Plain AOT size | Spectre.Console AOT size | Extra | Plain cold start (median, 20) | Spectre cold start (median, 20) | AOT/trim warnings |
|---|---:|---:|---:|---:|---:|---|
| Ubuntu / linux-x64 | [U] not measured | [U] not measured | [U] | [U] | [U] | [U] |
| macOS / osx-arm64 | [U] not measured | [U] not measured | [U] | [U] | [U] | [U] |
| Windows / win-x64 | [U] not measured | [U] not measured | [U] | [U] | [U] | [U] |

- **Limitations:** The implementation environment has no .NET SDK and cannot resolve external network hosts, so the throwaway-project spike has not been run. These values are deliberately left unmeasured rather than inferred. R17 CI records the real application's Native-AOT and framework-dependent binary sizes separately; those are not substitutes for the UI-library spike.
- **Consequences:** No package is added. The library decision remains open until the specified measurements are performed.
