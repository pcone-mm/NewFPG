# Forest Soundminer Workflow

This project uses a fixed Soundminer version and a read-only source library. The Unity project consumes only approved WAV exports; it does not read or modify Soundminer's internal database.

## Required inputs

- `Soundminer` version recorded in the delivery note.
- Read-only source directory path.
- CSV or TSV metadata export containing source path, library name, duration, sample rate, channels, and any UCS fields.
- The requirements matrix at `ForestAudioRequirements.csv`.

Until those inputs are supplied, the Forest assets are mapping-ready only. The empty `ForestCombatAudioBank.asset` and `ForestAudioProfile.asset` are intentional and must not be filled with indicator or generated clips.

## Search and selection

1. Write the sonic intent in the matrix before searching.
2. Search with UCS plus English semantic terms from `soundminerQuery`.
3. Keep 3-8 candidates per row and record every original path and library name.
4. Rank candidates by semantic fit, transient clarity, frequency overlap, tail length, noise floor, loop quality, and sync to the visual event.
5. Mark one candidate as the edit source only after A/B against the Forest gameplay capture.

## Non-destructive editing

Edit outside the Unity project. Never modify source files or Soundminer's database. Record the source hash before edit and export a new master.

- Master: 48 kHz, 24-bit WAV.
- Positional SFX: mono.
- Music and wide ambience: stereo.
- Short SFX: Unity `Decompress On Load` with PCM or ADPCM.
- Long ambience and music: Unity `Streaming` with Vorbis.
- Every loop must be checked for a click, DC jump, or audible seam.

## Naming and handoff

- `SFX_<role>_<action>_<variant>`
- `AMB_<area>_<layer>`
- `MUS_<area>_<state>`

For each approved export, fill `candidateFiles`, `sourcePath`, `sourceLibrary`, `sourceHash`, `editTask`, and `finalResource` in the matrix. Then assign the final clip to the corresponding bank/profile field and run the audio EditMode and PlayMode tests.

## Runtime mapping

`FormalRoom/AudioRoot` owns the presentation-only runtime. `CombatAudioPresenter` consumes committed combat routing and uses the fixed SFX/UI pool. `MusicDirector` receives explicit lifecycle commands and owns two-source crossfades plus one ambience loop. Audio changes must not alter combat traces, hashes, or results.
