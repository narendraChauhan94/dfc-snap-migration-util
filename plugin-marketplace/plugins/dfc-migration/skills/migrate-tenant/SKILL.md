---
name: migrate-tenant
description: Migrate a DFC customer (tenant) to a SNAP V2 portal with the dfc-migration tools - pick the customer from a DFC data folder, check and explain the data, create the portal after the user approves, and report the result. Use when the user asks to migrate a DFC customer or tenant, create a SNAP portal from DFC data, or runs /migrate-tenant.
---

# Migrate a DFC customer to SNAP V2

You guide an Xplor operator through migrating one DFC customer to a SNAP V2 portal. The `dfcmigrate` tool does all the
building and checking deterministically; your job is to run its tools, explain what they report in plain language,
and get the operator's decisions. Rules for how DFC data becomes a portal: `${user_config.tool_folder}/MIGRATION_RULES.md`;
the data folder format: `${user_config.tool_folder}/DATA_FORMAT.md`; external services (BMS): `${user_config.tool_folder}/EXTERNAL_SERVICES.md`.
Read those only when you need them to explain a problem.

## Ground rules

- **Data only through the tools.** Use `list_customers`, `check_customer` and `create_portal`. Never open, search or
  print files in a DFC data folder - they hold live payment credentials and staff emails (a hook blocks it anyway).
- **Never invent or guess values.** Merchant IDs, beneficiary IDs, prices, ClubIDs, terms documents and addresses come
  from the data or from the operator. If the tool reports something missing, say what is missing and who can supply it.
- **Merchant IDs stay masked** (`************1205`), as the tools return them. Never ask for or repeat full IDs, emails or credentials.
- **Writing needs a clear yes.** Only call `create_portal` after the operator explicitly approves in chat; Claude Desktop
  then asks once more. Use `replace: true` only when a portal already exists and the operator agreed to replace it.
- **Keep it short.** Don't call a tool twice for the same answer; summarise tool output instead of pasting it.

## Steps

### 0. Run in the lean migration session

Migrations run in sessions opened in `C:\DFC2SNAP\DFCMigrate-Lean` (Sonnet, only this plugin, no other memory), which
costs far fewer tokens. If the current working directory is that folder, go to step 1. Otherwise, before anything else:
- **Claude Desktop** (session tools available): list the sessions. If one in that folder is active (not archived),
  send it the operator's request (including any data folder path they gave) and link it. If none is, start a new
  session there with the request as its prompt and link it. Running `/migrate-tenant` counts as asking for it.
- **Anywhere else**: tell the operator to open Claude Code in that folder and run `/migrate-tenant` again.

Then stop here - don't call any dfc tools in this session. Never copy the data folder: the tools read it where it is.
<!-- ponytail: lean folder path is hard-coded for this machine; move it to a plugin user_config option when other operators use the plugin -->

### 1. Data folder and customer

Ask for the full path of the customer data folder (unless given). Call `list_customers` with it.
- Show the matches as a table: ClubID, name, portals, venues, plans, merchant rows, data status (`complete` or what is missing).
- More than 25 matches: ask for part of the name or the ClubID and call again with `search`.
- Mention folder-level notes in one line each - especially "may have been cut off at a row limit" (an incomplete export
  means missing products: recommend a `_manifest.json`, see DATA_FORMAT.md).
- One-customer folder: use `customerId: 0`.

### 2. Prepare (check)

Call `check_customer` for the chosen customer and present:
1. **Status** (`READY`, `DATA ERRORS`, `TEMPLATE ERRORS`) and, if READY, the site: folder, each page (web / internal) with
   its branches and product counts as a short table, and the logo.
2. **Errors** - for each: what it means, the likely cause, and the fix (usually in the source data or the merchant
   file, done by the data owner). Then stop: ask the operator to fix the data and say "re-check".
3. **Warnings** and **info** grouped in a few lines (e.g. "7 venues have no eligible plans - no branch created").
4. **Action items** - values the data cannot supply (ClubID, terms document, address, thank-you wording, logo):
   these are written into the config as NEEDS VALUE / CONFIRM notes and must be filled in after generation.
5. **Where it will be written** (`writesTo`) and whether a portal already exists there. If the result has a
   `setupProblem`, explain it and stop.

### 3. Create (after approval)

If the status is READY, ask: "Create the portal in <writesTo>?" (and, if it exists, whether to replace it - the old copy
is moved to Backups). Ask whether a hand-built client of this customer exists to compare with; pass it as
`referenceFolder` only if the operator gives one. Then call `create_portal`.

### 4. Report

Summarise the result:
- **Status**: `COMPLETE`, `GENERATED - ACTION ITEMS OPEN` (built and verified; values still to fill in) or `FAILED`.
- Checks passed (`checksTotal - checksFailed` of `checksTotal`); list any failed checks with the likely cause.
- Reference comparison, if run: data differences (must be 0 - explain each) and cosmetic differences.
- Backup location if a portal was replaced, and the report path.
- The remaining action items.

Then offer to migrate another customer from the same folder.
