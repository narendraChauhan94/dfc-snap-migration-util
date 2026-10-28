# dfc-migration: Claude Desktop plugin

Runs DFC to SNAP V2 migrations from Claude Desktop with your own Claude login. No API key is needed.
`dfcmigrate.exe` does all the building and checking. Claude picks the customer, explains the data, asks for your
approval and reports the result.

## What's inside

| Part | What it does |
|---|---|
| Skill `/migrate-tenant` | The workflow: data folder, customer pick list, check, approval, create, report |
| Tools (`dfcmigrate.exe mcp`) | `list_customers`, `check_customer` (read-only), `create_portal` (writes; asks you first) |
| Hook (`dfcmigrate.exe guard`) | Forces an approval prompt for `create_portal`, even in auto mode. Blocks Claude's file and shell tools from reading DFC data folders (live credentials and staff emails). |

The tools mask merchant IDs to their last 4 digits and remove email addresses before Claude sees anything.
The portal is only ever written to the `clientsFolder` set in `dfcmigrate.settings.json`, never a folder Claude chooses.

## Install (once per PC)

Requirements: the DFC migration tool folder on the PC (`dist\dfcmigrate.exe`, `Template\`), with `clientsFolder` set in
`dfcmigrate.settings.json` (running `dfcmigrate.exe` once interactively sets it).

In Claude Desktop (Code tab), type:

```
/plugin marketplace add C:\SourceCode\Snap-Git\DFCMigration\plugin-marketplace
/plugin install dfc-migration@xplor-dfc-migration
```

When asked for the **DFC migration tool folder**, accept the default or enter the folder that holds `dist\dfcmigrate.exe`.
Then start a **new session** and type `/migrate-tenant`.

You can also use **+ > Add plugins** in Claude Desktop.

## For other people

Put this `plugin-marketplace` folder in a shared git repo. Colleagues run `/plugin marketplace add <repo URL>` and the
same install command. Each person needs the tool folder on their PC and their own Claude login.
