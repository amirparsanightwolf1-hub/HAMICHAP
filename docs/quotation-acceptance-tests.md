# Quotation generation acceptance checklist

Run against a non-production database after applying the migration.

1. Select Formal in an editable quotation; complete the customer identity profile, configured seller information, actual currency, quantities, prices, VAT rates and tax identifiers. Generate quotation. Verify the draft banner, registered parties, identifiers, VAT rate/amount, net and gross per line, totals and signature areas. Save to PDF in Persian and English.
2. Select Informal on a separate draft with identical quantities and rates. Generate; verify the simplified header/table and no national IDs or tax identifiers. Confirm its gross amount is identical to the formal quote.
3. Generate with no type, incomplete formal party identity, mismatched formal currency, missing VAT, invalid validity date, or zero prices. Verify no printable document is generated and the editor explains the missing data.
4. Save a valid draft, edit pricing and save again, generate and mark sent after accepted proofs. Verify the latest saved values and party snapshot are used. A quote with an unaccepted job proof must remain unsent.
5. After sending, modify the customer's address or seller configuration. Reopen the sent quote; the captured party information should remain unchanged. A revision can be reissued with new party information.
6. Check a legacy accepted quote remains unclassified; no type is inferred. Check an accepted classified quotation's invoice defaults to the same type; an incompatible invoice type is rejected in the service.
7. Check role-based access, A4 print with 1, 10 and 30 lines, long Persian descriptions, and large rial amounts. Ensure the sidebar, application controls and proof links are not printed, table headings repeat, and totals and signature blocks remain readable.
8. For fiscal reporting use the existing tax workflow; the quote PDF is not an official fiscal invoice.
