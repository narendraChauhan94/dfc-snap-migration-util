# DFC → SNAP V2 migration rules

These rules come from the hand-built reference client `Template\MidUlsterDistrictCouncil` (the proven manual
process). `dfcmigrate` reproduces it with **0 data differences**; the regression test
`MidUlster_IsRebuiltExactlyLikeTheReferenceClient` enforces this.

## 1. Customer data folder: what the utility looks for

Folder and file names can be anything. Each file's role is decided by the columns it contains.

| Role | Recognised by | Required |
|---|---|---|
| Club (portals) | JSON rows with `ClubURL`, `ClubKey`, `Name` | exactly 1 file |
| Branches (venues) | JSON rows with `LinkedClubRefs`, `ClubRef`, `HideVenue` | exactly 1 |
| Plans | JSON rows with `Fee`, `LetterType`, `api_duration` | 1 or more (merged) |
| Categories | JSON rows with `Category`, `ParentCategoryID` | exactly 1 |
| Design | JSON rows with `ButtonColour`, `Logo` | exactly 1 |
| Merchant sheet | `.xlsx` with `URL Type`, `Club_URL`, `Branch_ID`, `Site Ref (Venue-wise)`, `Merchant HNK`, `Beneficiary ID`, `New URL` | exactly 1 |
| Logo | one `.jpg`/`.png`/`.gif` | optional (default: Xplor Pay logo) |

The utility stops with a clear message if any of these is true:
- a required file is missing;
- there are two files for the same role, or two images;
- a JSON file is invalid or the Excel file is unreadable;
- the files belong to more than one DFC customer.

Unrecognised files are listed and ignored.

## 2. Site, pages and branches

| SNAP V2 | Source | Rule |
|---|---|---|
| Site folder and page names | Merchant sheet `New URL` | `https://…/<Folder>/<Page>.aspx`, e.g. `MidUlsterDistrictCouncil` and `MidUlsterDistrictCouncilInternal`. One folder per customer. |
| Page type | Merchant sheet `URL Type` | `Websales` or `InClub`. **Both pages are built from the template web page** (`GenericClub.aspx/.config`); the Internal page has no staff logon, as in the reference. |
| Portals migrated | Merchant sheet `Club_URL` | Only portals listed in the sheet |
| Branches | Merchant sheet rows → venue (`Branch_ID` = venue `ID`, `Site Ref` = venue `ClubRef`) | A branch is created only if the venue has at least one eligible plan in that portal |
| Branch `ID` | Venue name | Lower-case, "Leisure Centre" removed, e.g. `greenvale_annual` |
| `MerchantID` | Merchant sheet `Merchant HNK` | **Must be stored as Text**, exactly 16 digits. A numeric cell is refused because Excel drops the 16th digit. If every ID ends in 0 (3 or more IDs), generation stops: the column was changed to Text after Excel had already rounded the values, so the IDs must be re-entered from the merchant source. |
| `DDMS_Beneficiary` | Merchant sheet `Beneficiary ID` | Whole number |
| `Description` | Venue `Name` | |
| `ConfirmationEmail_Branch_EmailAddresses` | Venue `Email` | Comma-separated. These are staff addresses: personal data, approved for this use, and never written to logs or reports. |
| Branch `ProductPageBranchInfo` | Venue `Welcome` | Only when not empty |
| Branch information panel (page) | Venue address lines, `Telephone`, `DisplayEmail` | Address line 1 is dropped when the venue name already starts with it |

## 3. Products

| SNAP V2 | Source | Rule |
|---|---|---|
| Which plans | Plans for the portal and site ref | `Active = 1`, **no promo code**, not expired |
| Product `ID` | Plan `ClubRef` + `Name` | `4130_adult_all_inclusive_monthly_dd_membership` |
| Template product | Plan type | One-off (`CardPayOneOff = 1` or `One-Off`) → `oneoff_product`; otherwise `monthly_product` |
| `MonthlyMembershipFeePence` / `JoiningFeePence` | `Fee` / `JoinFee` | × 100 |
| `ContractLengthMonths` | `Term` | One-off = 1 |
| `OneOffMembershipDuration` | `api_duration` (`03M`, `06M`, `12M`) | One-off only; recurring must be monthly (`M`) |
| `PaymentMethods` | Plan type | Recurring: empty (SNAP default DD, whether DFC said `CS` or `DD`). One-off: `OneOff`. |
| `YoungAgeLimitYears` | `MinAge` | |
| `CollectionScheduleDaysOfMonth` | Club `PayDate1/8/15/25` = `Y` | e.g. 1 and 15 |
| `ProductDescription(Long)` | `Name` | |
| `ProductDescriptionLongHTML` | Category + name + price | `<span class="Adult_Memberships"><strong>Name</strong><br />&pound;34.00 per month</span>`, with "…for a minimum of 12 months" when Term > 1, or "…one-off payment for N months membership". The span class makes SNAP's product tabs. |
| Product order | Category | Category sort order, then category name, then site ref, then plan name |

## 4. Global settings (each page uses its own portal's club record)

| SNAP V2 | Source | When missing |
|---|---|---|
| `ClubName`, `CompanyName`, `PDFCompanyName` | Club `Name` | |
| `ConfirmationEmail_Branch_Addresses` | Club `NotifyEmail` (or `Email`) | Error |
| `ProductPageBranchInfo` | Club `Welcome` | |
| `ThankyouMessage` | Club `FinalMessage` | Standard sentence + `CONFIRM wording with client` note |
| `ClubID` | Not in DFC data | Empty + `NEEDS VALUE: Harlands 5-digit club number…` note |
| `TermsAndConditionsDocument` | Club `TermsUpload` | Empty + `NEEDS VALUE: DFC pointed at …` note |
| `PDFCompanyAddress` | Club address | Empty + `NEEDS VALUE: club address is blank…` note |
| Originator (DFC 406492), Voyager, SagePay, promo codes off, marketing sources | Common template | — |

A site with any NEEDS VALUE or CONFIRM note is reported as **"GENERATED – ACTION ITEMS OPEN"**, never "COMPLETE".

## 5. Look and feel

| Item | Source |
|---|---|
| `ClubStyle.css` colours | Design: `ButtonColour`, `GreyButtonColour`, `ErrorColour`, `InfoIconColour`, `ErrorIconColour`, `BackgroundColour` (page and banner), `TextColour` (body text), `HeaderTextColour` (text on button/header colours), `InternalBGColour` (form panels). Each must be a 6-digit hex value, otherwise it's an error. Derived: button hover = 85% towards white; error background = 96% towards white; grey hover = grey − 20 per channel. |
| Logo position | Design `LogoAlign`: Left, Center or Right; anything else is an error |
| Box corners | Design `BoxStyle`: Rounded = 4px, Square = 0; anything else is an error |
| `Glow_thickness`, `Border_thickness` | Not applied. The template, like the reference, has no glow or border box. A warning is shown if the value isn't 0. |
| Other Design fields (`SelectorStyle`, `FontID`, `HideCompanyName`, …) | Not used. The manual reference shows no trace of them. |
| Header logo | The logo image in the folder, copied under its own name |
| Branding, footer, CSS/JS fixes, email files | Common template (taken from the reference) |

## 6. Validation

1. **Before writing:** all of the checks above. Any error means nothing is written.
2. **After writing:** every generated file is read back and checked against the plan, covering pages, global values, branches, products, panels, the logo, the stylesheet and leftover template markers.
3. **Optional reference comparison:** a semantic comparison with a known-good client. Differences are classed as **data** (fails the run) or **cosmetic**.
4. **Optional live check:** each page returns 200 and shows the customer name.

## 7. Known cosmetic differences from the Mid Ulster reference

- **Button hover shade:** the reference uses the hand-picked `#f4dedd`; the utility derives `#fadedc`.
- **Club panel:** the reference adds "Leisure Services", which isn't in any data file.
- **Branch IDs:** the reference uses the hand-picked `meadowbank` and `maghera_mlc`; the utility's rule gives `meadowbank_sports_arena` and `maghera_monthly_mlc_adult_mlc_concession`. Branch IDs are internal and have no functional effect.
