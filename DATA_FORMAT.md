# Customer data folder: required format

One folder holds the data for **all** customers. The agent asks for the folder, lists the customers it finds, and you pick one.
Files are recognised by their **content**, not their names, so the names below are suggestions.

| File | Format | Status |
|---|---|---|
| Club, Venue, Plan category, Design | DFC JSON exports, unchanged | ✅ Same as today |
| Plans | DFC JSON export, split into as many files as needed: `DFC All Plan 1.json` … `DFC All Plan N.json` | ✅ Any number of files. Each plan `ID` must appear in only one file. |
| Merchant and beneficiary IDs | `DFC All Merchant.json` (section 1) | New, replaces the Excel sheet |
| BMS (external service) | `DFC All BMS.json` (section 2) | New |
| Manifest | `_manifest.json` (section 3) | New. Lets the agent prove nothing was cut off. |
| Logos | `Logos\<ClubID>.<jpg/png>` (portal ID) | Optional |

General rules:
- **All IDs and codes are JSON strings**, not numbers: `"1111000000011205"`, `"008"`. Numbers lose digits (16-digit merchant IDs) and leading zeros (package code `008`).
- Use DFC's own IDs to link records: `ClubID` = **portal ID** (the Club file's `ID`, e.g. `"2060"`), `VenueID` = the Venue file's `ID`, and `PlanID` = the Plan file's `ID`.
- UTF-8, one JSON array per section, no comments.
- **No passwords, keys or tokens in the new files.** Service credentials are added on the server by ops, never shipped with the data.

## 1. Merchant and beneficiary IDs: `DFC All Merchant.json`

One row per portal and venue, the same as one row of today's Merchant HNK sheet.

```json
[
  {
    "ClubID": "2060",
    "VenueID": "6725166",
    "UrlType": "Websales",
    "ClubUrl": "MidUlster",
    "NewUrl": "https://snapv2.example/Clients/MidUlsterDistrictCouncil/MidUlsterDistrictCouncil.aspx",
    "MerchantHNK": "1111000000011205",
    "BeneficiaryID": "6725166",
    "ServiceUserNo": "406492",
    "ExtService": "XN"
  }
]
```

| Field | Required | Rule |
|---|---|---|
| `ClubID` | ✅ | Portal ID from the Club file |
| `VenueID` | ✅ | Venue ID from the Venue file |
| `UrlType` | ✅ | `Websales` or `InClub` |
| `ClubUrl` | ✅ | The portal's DFC `ClubURL` |
| `NewUrl` | ✅ | `https://…/<Folder>/<Page>.aspx`. One folder per customer; letters and digits only. |
| `MerchantHNK` | ✅ | Exactly 16 digits, as a string |
| `BeneficiaryID` | ✅ | Digits, as a string |
| `ServiceUserNo` | — | DD originator (SUN), if it differs from the template's default |
| `SiteRef` | — | The venue's `ClubRef`. If given, it's checked against the Venue file. |
| `ExtService` | — | 3rd-party integration name (today's sheet column "Ext Service – 3rd Party Integration"). Must match `ServiceType` in the BMS file. |

## 2. BMS: `DFC All BMS.json`

Three sections, matching the three levels SNAP V2 reads (see `EXTERNAL_SERVICES.md`). Leave a customer out if it has no BMS.

```json
{
  "Services": [
    { "ClubID": "2060", "ServiceType": "DeltaCS", "Url": "https://bms.example/soap/endpoint", "Url2": "", "SiteId": "" }
  ],
  "Branches": [
    { "ClubID": "2060", "VenueID": "6725166", "SiteId": "12", "BranchCode": "" }
  ],
  "Plans": [
    {
      "ClubID": "2060",
      "PlanID": "604310",
      "PackageCode": "008",
      "JointMemberPackageCode": "",
      "SubMembers": [ { "Number": 1, "PackageCode": "009", "Description": "Junior" } ]
    }
  ]
}
```

| Section | Field | Required | Goes to (SNAP config) |
|---|---|---|---|
| Services | `ClubID`, `ServiceType` | ✅ | Global `ExtSvcParameters id` + `servicetype` |
| | `Url` | ✅ | Global `url` |
| | `Url2` | — | Global `url2` (Marriott-style services) |
| | `SiteId` | — | Global default `siteid` |
| Branches | `ClubID`, `VenueID` | ✅ | — |
| | `SiteId` **or** `BranchCode` | ✅ one of them | Branch `siteid` (DeltaCS) or `branchcode` (Marriott) |
| Plans | `ClubID`, `PlanID`, `PackageCode` | ✅ | Product `packagecode` (the BMS price ID) |
| | `JointMemberPackageCode` | — | Product `jointmemberpackagecode` |
| | `SubMembers[]` (`Number` 1–3, `PackageCode`, `Description`) | — | Product `submemberN_packagecode` / `submemberN_packagedescription` |

Rules:
- Every plan of a BMS customer that becomes a product needs a `PackageCode`; a missing one stops generation.
- `ServiceType` must be a type with a checked reference client (today: `DeltaCS`, `Marriott`). Others are reported, not guessed.
- If the BMS price ID is already in the Plan export (`GladstonePriceBand` or `shortcode`), say which field holds it for which service type; then the `Plans` section can be skipped.

## 3. Manifest: `_manifest.json`

It proves every file is complete, so a cut-off export can't silently drop products.

```json
{
  "ExportedAt": "2026-10-27T15:33:00Z",
  "Files": [
    { "File": "DFC All Plan 1.json", "Rows": 10000 },
    { "File": "DFC All Plan 2.json", "Rows": 20000 },
    { "File": "DFC All Club data.json", "Rows": 206 }
  ],
  "Totals": { "Plans": 30000, "Clubs": 206, "Venues": 3135, "Categories": 5551, "Designs": 2074 }
}
```

The agent stops if a file's row count, or a total, doesn't match the data it reads (for example, plans exported from the source system ≠ plans found across all plan files).
