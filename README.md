# DFC → SNAP V2 customer migration (`dfcmigrate.exe`)

You give the tool a **customer's DFC data folder**. It clones the common template (`Template\GenericClubFastDD`) into
`<SNAP Clients>\<Folder>\` using the same rules as the hand-built reference client
(`Template\MidUlsterDistrictCouncil`), then verifies the result.

- **Operator guide:** [HOW-TO-USE.txt](HOW-TO-USE.txt)
- **Every rule and its data source:** [MIGRATION_RULES.md](MIGRATION_RULES.md)

> **Data handling.** The exports contain staff email addresses (personal data, approved for the branch notification
> lists) and merchant passwords/JWT/API tokens. The tool reads only the fields it needs, never reads the credential
> columns, and never writes emails or secrets to logs or reports. Customer data folders and `Logs\` must stay
> access-restricted.

## Flow

```
customer data folder ─► recognise files by content ─► apply migration rules ─► preview
        (any names)        (missing / duplicate / mixed      (MIGRATION_RULES.md)   errors? stop, re-check
                            customer = stop)
                                                             ─► type 'yes' ─► write (staged, never partial,
                                                                              old copy → Backups\)
                                                             ─► verify files ─► compare with reference (optional)
                                                             ─► live HTTP check (optional) ─► report in Logs\
```

## Run

Double-click `Run-DFCMigration.bat`, or drag the data folder onto it. Scripted runs:

```bash
dist/dfcmigrate.exe --data "MidUlsterDistrictCouncil" --output "C:\SourceCode\SnapV2\SNAP2\SnapDDA - Website\Clients" --dry-run
```

```bash
dist/dfcmigrate.exe --data "MidUlsterDistrictCouncil" --output "C:\SourceCode\SnapV2\SNAP2\SnapDDA - Website\Clients" --reference "Template\MidUlsterDistrictCouncil\MidUlsterDistrictCouncil"
```

## Proof against the reference client

With the merchant column re-entered as text (exact IDs), Mid Ulster rebuilds with **0 data differences** from the
reference. All 98 checks pass. The 9 cosmetic differences are listed in MIGRATION_RULES.md §7. The test
`MidUlster_IsRebuiltExactlyLikeTheReferenceClient` repeats this on every build.

## Build & test

The suite has 23 xUnit tests:
- the reference regression test;
- folder detection with renamed files;
- missing, duplicate and mixed-customer files;
- refusal of numeric merchant IDs;
- the branch, product, global and stylesheet rules;
- safe replace, backup and rerun;
- prompts;
- the HTTP smoke check.

```bash
dotnet test DfcMigrator.sln
```

```bash
dotnet publish DfcMigrator/DfcMigrator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

The second command produces `dist/dfcmigrate.exe` (~84 MB, no .NET install needed). It depends on ClosedXML (MIT)
to read the merchant sheet.

To test locally, `LocalTest\snap2-iisexpress-classic.config` runs `SnapDDA - Website` under IIS Express in Classic
pipeline mode, which SNAP requires.

## Common template

The template was updated on 2026-10-19 with the generic parts of the reference client: Xplorpay branding, product
tabs, CSS/JS fixes, the DFC originator, email naming, a one-off sample product, and colour tokens. The previous template
is in `Backups\Template-GenericClubFastDD-20261019-133717`. The retired Excel workbook step is in
`Backups\obsolete-excel-step-*`.
