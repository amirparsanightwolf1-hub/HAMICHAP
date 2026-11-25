# VAT and electronic tax reporting setup

This is an internal sales VAT and reporting preparation workflow. It is not a certified fiscal invoicing integration. Formal/informal are business document labels, not statutory electronic invoice types or exemptions. All new invoices require explicit tax treatment regardless of label.

1. Confirm registered tax jurisdiction and actual transaction currency with accounting. Existing amounts must never be silently converted from toman to rial.
2. Configure the deployment via environment variables or server-side configuration (no private signing keys in Git):
   - TaxReporting__Jurisdiction (e.g. IR if registered in Iran)
   - TaxReporting__CurrencyCode (e.g. IRR if prices really are in rial)
   - TaxReporting__LegalName
   - TaxReporting__NationalId
   - TaxReporting__EconomicCode
   - TaxReporting__RegistrationNumber
   - TaxReporting__Address
   - TaxReporting__PostalCode
   - TaxReporting__TaxMemoryId (required before issuing Iran invoices)
3. Apply migrations and grant Sales.Invoices.TaxReporting only to responsible accounting employees.
4. Set actual currency, official goods/service identifier, verified VAT percentage and any zero-rate justification before sending a quotation. Unit prices exclude VAT. Rounding is whole rials for IRR, otherwise two decimals. Confirm this is suitable for your currency and authority. Partial invoice amounts allocate cumulative rounded net and VAT.
5. Invoices created before this change remain unclassified for VAT/reporting. Previously accepted quotations without tax data are blocked from new invoicing; accounting must reconcile and obtain customer agreement for any revised pricing. Never infer missing VAT or add it after acceptance.
6. Open /sales/tax-reporting; prepare and save the internal JSON for a verified accounting/provider mapping. Use the actual official portal or approved provider to transmit and verify results. Only then record the external reference and status. The internal JSON is not an official schema, signed payload, tax identification number, filing or confirmation.
7. For Iran, verify current rates and official goods/service identifiers through the tax authority's goods/services system (https://stuffid.tax.gov.ir/) and confirm current Moadian requirements through the official portal (https://my.tax.gov.ir/). Direct integration requires the current technical specification, enrollment, tax memory identifier, authorized signing/transport setup or licensed provider API, and testing. No external HTTP request is made by this implementation.
8. Electronic corrections, cancellations, returns, tax-period VAT returns and purchase/input VAT are not implemented. External acceptance is manually recorded, not independently verified. Both invoice types remain subject to existing full-payment shipment gate.

## Quotation document types

Apply migration 20261125090000_AddQuotationDocumentTypes before deploying. In the quotation editor select Formal or Informal, enter prices, currency and verified VAT information, then Generate quotation (saved draft preview with a draft banner). Print / Save PDF uses the browser's A4 print dialog. Mark as sent still requires all linked proofs to be accepted and captures the buyer/seller details. Historical quotations remain unclassified. Formal requires seller legal identity/address/postal code and matching currency in TaxReporting and the customer's national identification; optional company registration/economic details are printed where available. QuotationBranding__DisplayName, QuotationBranding__Phone and QuotationBranding__Email customize commercial branding. Informal never waives VAT. The accepted quotation type controls newly issued invoices; legacy quotations retain the existing invoice-type selection. Neither quotation format is a fiscal invoice or official tax submission.

### Reusable formal seller companies

Apply 20261125100000_AddSellerCompanies. Select Formal in the editor, enter the seller company name to search the saved directory, and select an existing company or Add company. Enter the verified legal name, national ID, economic code, registered address, postal code, jurisdiction and currency; registration number, telephone, email and tax memory ID may also be saved. Save company for reuse selects it for the current draft; save/generate the quotation to persist that association. Users with quotation creation permission can add or edit saved company details; the jurisdiction and national ID of an existing company cannot be changed. No registry lookup is performed. Unique jurisdiction/national ID and row versions protect duplicates and concurrent edits. Sent quotations retain a party and tax-seller snapshot. Invoices from new formal quotations use that issuer instead of the global seller configuration; a missing Iran tax memory ID can be supplied later in the same saved profile without replacing the original legal party details. Older formal quotations still use their original snapshots and must match the configured invoice seller. Informal behavior is unchanged. Never enter signing keys or passwords into company profiles.
