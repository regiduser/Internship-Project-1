# Student Management System

A layered C# / .NET 10 console application for managing student records, with durable JSON storage,
strict validation, CSV export, a statistics dashboard and **explainable semantic search** built on a
small, from-scratch embedding engine (no API key, no internet, no ML dependencies).

## Quick start

```bash
dotnet run --project StudentApp          # start the app
dotnet test                              # run the 90+ automated tests
```

New here? Start the app, choose **9** (load demo students), then **8** (semantic search) and try
`programming`, `network security`, or `I like singing and playing the guitar`.

Use a separate data file for demos so your real records stay untouched:

```bash
dotnet run --project StudentApp -- --data demo.json
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows, Linux and macOS.

## Features

| Area | What it does |
|---|---|
| Records | Add, list (sortable by ID / name / age / recently updated), update, delete |
| Validation | Unique positive IDs, name 1-100 chars, age 1-120, profile up to 1,000 chars - one shared rule set used by the UI, service and file loader |
| Storage | JSON in the user's app-data folder; atomic, flushed saves with an automatic `.bak` of the previous version |
| Safety | Delete needs the word `DELETE`; a corrupt or hand-edited data file stops start-up instead of being overwritten; failed saves roll back in-memory changes |
| Search | Name substring or exact ID search |
| Semantic search | Ranks students by cosine similarity of embedding vectors and **shows why** each one matched |
| Dashboard | Count, average, median, youngest/oldest, profile coverage, age histogram |
| Export | RFC 4180 CSV (Excel-friendly UTF-8), protected against spreadsheet formula injection, asks before overwriting |
| Demo mode | One-key sample data and a `--data` switch for isolated demos |

## Semantic search: how the embedding works

The goal is "find the students about X" even when the words differ (`programming` should find
someone who wrote `software development and Python coding`). It is a transparent *hashing vectorizer*,
deliberately simple enough to explain and test:

1. **Tokenize** the text, drop stop-words (`the`, `interested`, `skills`, ...) and lightly **stem**
   (`projects` -> `project`, `building` -> `build`).
2. Emit **features**: the word, a bigram with the previous word, and one feature for every
   **concept group** the word belongs to (`coding`, `software`, `developer` -> *programming*).
   A word may belong to several concepts (`communication` -> *networking* and *leadership*).
3. Weight repeats **sub-linearly** (1 + ln n) so keyword stuffing can't dominate.
4. **Hash** each feature into one of 512 slots with a **signed** stable hash (FNV-1a + Murmur finalizer),
   so colliding features tend to cancel rather than pile up.
5. **L2-normalise**; similarity is then the cosine of the angle between two vectors.
6. Keep only hits that share at least one word or concept with the query, and report those as the
   explanation: `why: related topic: networking, security; shared words: network`.

Vectors are cached per student and invalidated on update/delete. The score is a similarity measure,
not a probability, and this is **not** a trained neural model - coverage is limited to the concept
groups in `EmbeddingService.cs`. Swapping in a real model later only means replacing
`EmbeddingService.Embed`.

## Project structure

```text
StudentApp.slnx
StudentApp/                  console application
  Models/                    Student, StudentRules (shared limits), SemanticMatch, StudentStatistics
  Services/
    EmbeddingService.cs      tokenizer, stemmer, concept groups, hashing vectorizer, cosine similarity
    IStudentRepository.cs    storage abstraction (lets the service be tested without a disk)
    StudentRepository.cs     atomic JSON persistence + validation of loaded data
    StudentService.cs        business rules, search, statistics, rollback on failed saves
  UI/
    ConsoleIO.cs             input/output helpers (end-of-input safe, testable)
    StudentMenu.cs           the menu and all screen layouts
    SampleData.cs            demo students
  Utils/CsvExporter.cs       CSV writer
  Program.cs                 entry point and command-line options
StudentApp.Tests/            xUnit tests (unit + scripted end-to-end menu sessions)
.github/workflows/ci.yml     build + test on every push and pull request
```

## Data storage

| OS | Default location |
|---|---|
| Windows | `%LOCALAPPDATA%\StudentApp\students.json` |
| Linux | `~/.local/share/StudentApp/students.json` |
| macOS | `~/.local/share/StudentApp/students.json` (as reported by .NET) |

Each save keeps the previous version next to it as `students.json.bak`. Everything stays on your
machine; nothing is transmitted. Avoid editing the file while the app is running. Records written by
earlier versions (including ones without profile text) load unchanged.

## Testing

`dotnet test` runs the xUnit suite:

- **EmbeddingService** - determinism, unit length, related-vs-unrelated ranking, stemming, concept groups, regression tests for earlier bugs
- **StudentService** - validation, CRUD, exact-ID search, sorting, semantic ranking and cache invalidation, statistics, **rollback when storage fails**
- **StudentRepository** - round trip, backups, corrupt / duplicate / invalid data, legacy files
- **CsvExporter** - quoting, newlines, formula-injection protection, BOM
- **StudentMenu** - whole scripted sessions through the real menu, including closed-input and storage-failure paths

## Limitations

Single-user local app: no authentication, no concurrent-writer support (the last save wins), no GUI.
Semantic search is a lightweight educational embedding, not a transformer model.
