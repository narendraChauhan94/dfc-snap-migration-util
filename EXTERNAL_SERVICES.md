# External services (BMS) in SNAP V2 portals

How a portal tells SNAP V2 which member-management system (BMS) to send a sign-up to, and with which IDs.
Learned from two hand-built clients and from the SNAP2 code that reads the settings. Use it as the
reference when generating a portal from a customer's BMS data.

Sources:
- `SnapV2\SNAP2\SnapDDA - Website\Clients\Trilogy`: service type `DeltaCS` (Harlands Scuba DD import web service)
- `SnapV2\SNAP2\SnapDDA - Website\Clients\MigrationDisabledrahulMarriottFitness`: service type `Marriott`
- `SnapV2\SNAP2\SnapDDA - Business Rules Layer\BridgeApiClient.vb` (`BuildRequest`, `ResolveBeneficiaryId`), `ExternalServices.vb`, `ExtSvcParameters.vb`

Only the live configs were used. Files with `.bak`, `WIP`, `Copy`, `_DEV`, `_NOTUSED` or `OLD` in the name were ignored.

## 1. Shape

Each level can hold an `<ExternalServices>` block with one `<ExtSvcParameters id="…">` per service:

```xml
<ExternalServices>
  <ExtSvcParameters id="DeltaCS">
    <servicetype>DeltaCS</servicetype>
    <!-- free-form parameters, lower-case names, whatever the service needs -->
  </ExtSvcParameters>
</ExternalServices>
```

| Level | Where | What it holds in the references |
|---|---|---|
| Site | `SnapConfig/GlobalSettings` | `servicetype`; the endpoint `url` (Marriott also `url2`); credentials `logon` (DeltaCS) or `password` (Marriott); default `siteid`; Marriott's existing-member field names |
| Branch | `Branches/Branch` | `servicetype` + the BMS site: `siteid` (DeltaCS) or `branchcode` (Marriott). Sometimes it repeats or overrides `url`/credentials. |
| Product | `Products/Product` | `packagecode` = the BMS price/package for this product. Optional: `jointmemberpackagecode`, `submemberN_packagecode` + `submemberN_packagedescription` (N = 1–3), `ReverseLeadAndJointMember` (DeltaCS) |

## 2. How SNAP V2 uses it (`BridgeApiClient.BuildRequest`)

1. **Merge order:** Global, then Branch, then Product. **Product wins** on conflicts.
2. The first service's `servicetype` becomes `ExternalServiceType` in the SnapBridge sign-up request. If there's no block, it's `None`.
3. `url`, `password` and `siteid` go into `BmsSubmissionInfo` as separate fields. **Every** merged parameter also goes into `AdditionalParamsJson` under the club's own key names, so the key names must match what that BMS integration expects (`packagecode`, not `priceid`, for DeltaCS and Marriott).
4. `DDMS_Beneficiary` is resolved from the form, then the Branch, then Global, then any `ExtSvcParameters` level.
5. `ExternalService_UseExternal` / `_Login` / `_Pass` / `_ProductName` is the **old V1 model**. It's only used when a club has no `ExtSvcParameters` at all. Trilogy has `ExternalService_UseExternal = None` and still sends to DeltaCS through `ExtSvcParameters`. **Generate `ExtSvcParameters` only. Never generate the V1 fields.**
6. Templated services (`template` parameter, e.g. EzRunner/Wellsway) have `%TOKEN%` placeholders that are filled from other parameters and the sign-up data. Out of scope until there's a reference client.

## 3. Generation rules (for the migration utility)

| BMS data | Goes to | Key |
|---|---|---|
| Service name | Global block `id` and `servicetype`. Repeat `servicetype` on each Branch block (as Trilogy does). | `servicetype` |
| Service URL(s) | Global | `url` (+ `url2` if the service has two) |
| Service credentials | Global | `logon` (DeltaCS) or `password` (Marriott). **Never copy from the data file into output, logs or reports.** Write an empty value plus a `NEEDS VALUE` note; ops fill it in on the server. |
| BMS site per venue | Branch | `siteid` (DeltaCS) or `branchcode` (Marriott) |
| Price/package ID per plan | Product | `packagecode` |
| Joint / sub-member package | Product | `jointmemberpackagecode`, `submemberN_packagecode`, `submemberN_packagedescription` |

- Put each value at **one** level only. Don't repeat url/credentials on every branch the way some Marriott branches do, because that's where configs drift.
- A plan with no price ID when the customer uses a BMS is an **error**, not a blank.
- **Key names depend on the service type.** Only `DeltaCS` and `Marriott` are confirmed. Any other service type stops generation until a reference client for it has been checked.
- Mid Ulster's DFC data reports a 3rd-party integration `XN`. Its service-type name and keys are unknown; it needs a reference before it can be generated.

## 4. Open questions

- The BMS JSON field names, and how they map to `servicetype` / `url` / `siteid` / `branchcode` / `packagecode`.
- Where credentials come from for a new portal (a secure store or ops), since they must not come from the data file.
- Whether any customer needs a second service (more than one `ExtSvcParameters` per level). SNAP2 only sends the first.
