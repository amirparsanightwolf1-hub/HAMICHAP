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
