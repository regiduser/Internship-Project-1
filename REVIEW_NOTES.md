# Review notes

## What changed in this version (2.0)

**Bugs fixed**

| # | Problem in the previous version | Fix |
|---|---|---|
| 1 | Closing or piping input (Ctrl+Z / Ctrl+D / `echo 4 \| app`) made every prompt **loop forever** | All input goes through `ConsoleIO`; end-of-input raises `InputClosedException` and the app exits cleanly |
| 2 | Synonym table silently overwrote itself: `network` ended up in the *AI* group (from "neural network"), `communication` lost its networking meaning, `skills`/`info`/`science` were mapped to unrelated concepts | Concept groups are named, a word may belong to several, generic words removed, regression tests added |
| 3 | Semantic text included `age {n}` for every student, so a query like "age" matched everyone | Only name + profile are embedded |
| 4 | Search by ID used substring matching: searching `1` returned IDs 1, 10, 11, 21... | Exact ID match (plus name substring) |
| 5 | If saving failed (disk full, file locked) the in-memory list had already changed, so screen and disk disagreed | Add / update / delete roll back when the repository throws |
| 6 | A hand-edited file with `null` profile, duplicate IDs or an invalid age caused crashes or odd behaviour later | Loaded data is validated; problems stop start-up with a clear message |
| 7 | CSV export wrote names such as `=HYPERLINK(...)` verbatim -> formula injection in Excel / Sheets | Dangerous leading characters are neutralised |
| 8 | Update forced retyping name and age, and the profile could never be cleared; the export silently overwrote files | Enter keeps the current value, `-` clears the profile, overwrite asks first |
| 9 | Banner box was two characters narrower than its borders; box characters showed as `?` on some Windows code pages | Width computed in code; console set to UTF-8 |
| 10 | Every semantic search re-embedded every student | Vectors cached per student, invalidated on change |

**Embedding quality:** stop-words, light stemming, signed feature hashing (collisions cancel instead of
accumulate), sub-linear term counts, evidence-based filtering (every hit explains itself), more concept
groups.

**Structure:** `StudentRules` (one set of limits), `IStudentRepository`, `ConsoleIO` + `StudentMenu`
split out of the 220-line `Program.cs`, `SemanticMatch` / `StudentStatistics` result types, project and
namespace renamed from `project_1` to `StudentApp`.

**New:** automated test suite, CI workflow, `.editorconfig`, strict analyzers with warnings-as-errors,
backup file on save, sortable list, median + age histogram, demo data, `--data` / `--help` options.

## Verification status

- `dotnet build` of the app: **0 warnings, 0 errors** (.NET 10 SDK, warnings treated as errors).
- 91 tests, all passing, plus scripted end-to-end runs of the real executable (demo data, semantic
  search, dashboard, closed-input exit, corrupt-file refusal, backup creation).
- **One caveat:** the package feed (NuGet) was not reachable from the environment where this was
  prepared, so the suite was executed against a small stand-in for xUnit's `[Fact]`/`Assert` API rather
  than the real xUnit packages. Run `dotnet test` once on your machine before the review; the tests use
  only mainstream xUnit assertions, so no changes are expected.

## Suggested 5-minute demo

1. `dotnet run --project StudentApp -- --data demo.json`
2. **9** -> load demo students, **2** -> list (try sorting by name).
3. **8** -> `programming`: Aarav ranks first, *why: related topic: programming*.
4. **8** -> `I like singing and playing the guitar`: shows stemming (`singing`~`Singing`) and concept (*music*).
5. **8** -> `network security`: both concepts light up for Meera.
6. **6** dashboard, **7** export, **4** delete (type `DELETE`).
7. Show `dotnet test`.

## Questions a reviewer is likely to ask

- **Is this real AI?** No - it is a classic feature-hashing text vectorizer with a hand-written concept
  map. It is deterministic, offline and explainable. The interface (`Embed` -> vector, cosine ->
  score) is the same one a transformer model would plug into.
- **Why hash into 512 dimensions?** Fixed-size vectors with no vocabulary to store. Collisions are
  possible, so the hash is signed (collisions average out) and results need shared evidence.
- **Why cosine similarity?** Vectors are L2-normalised, so it measures direction (topic), not length
  (how much text was written).
- **How do you keep the data safe?** Write to a temp file, flush, atomically swap, keep `.bak`;
  refuse to start on corrupt data; roll back memory if a save fails.
- **What would you do next?** Real embedding model behind the same method; persist vectors; a
  database instead of one JSON file; per-student interest tags.
