using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Services;

public class InventoryPostingService
{
    private readonly ApplicationDbContext _db;

    public InventoryPostingService(ApplicationDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // Public entry point
    // =========================================================

    public async Task PostAsync(
    long documentId,
    string userId,
    bool useTransaction = true)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException(
                "A valid user is required to post an inventory document.");

        if (!useTransaction)
        {
            await PostInternalAsync(
                documentId,
                userId);

            return;
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            await PostInternalAsync(
                documentId,
                userId);

            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();

            throw new InvalidOperationException(
                "The inventory document or stock was changed by another user. " +
                "Please reload the document and try again.");
        }
        catch
        {
            await transaction.RollbackAsync();

            throw;
        }
    }

    private async Task PostInternalAsync(
    long documentId,
    string userId)
    {
        var document =
            await LoadDocumentAsync(
                documentId);

        ValidateDocument(
            document);

        var locations =
            await LoadLocationsAsync(
                document);

        ValidateDocumentLocations(
            document,
            locations);

        ValidateLines(
            document);

        foreach (var line in document.Lines)
        {
            await PostLineAsync(
                document,
                line,
                locations,
                userId);
        }

        document.Status =
            InventoryDocumentStatus.Posted;

        await _db.SaveChangesAsync();
    }

    // =========================================================
    // Backward-compatible Goods Receipt method
    // =========================================================

    public async Task PostGoodsReceiptAsync(
        long documentId,
        string userId)
    {
        var documentType = await _db.InventoryDocuments
            .AsNoTracking()
            .Where(x => x.Id == documentId)
            .Select(x => x.DocumentType)
            .FirstOrDefaultAsync();

        if (documentType != InventoryDocumentType.GoodsReceipt)
        {
            throw new InvalidOperationException(
                "The selected document is not a Goods Receipt.");
        }

        await PostAsync(
            documentId,
            userId);
    }



    // =========================================================
    // Load document
    // =========================================================

    private async Task<InventoryDocument> LoadDocumentAsync(
        long documentId)
    {
        var document = await _db.InventoryDocuments
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
                    .ThenInclude(x => x.Unit)
            .Include(x => x.Lines)
                .ThenInclude(x => x.InventoryLot)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == documentId);

        if (document == null)
        {
            throw new InvalidOperationException(
                "Inventory document not found.");
        }

        return document;
    }

    // =========================================================
    // Document validation
    // =========================================================

    private static void ValidateDocument(
        InventoryDocument document)
    {
        if (document.Status == InventoryDocumentStatus.Posted)
        {
            throw new InvalidOperationException(
                "Document has already been posted.");
        }

        if (document.Status == InventoryDocumentStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled documents cannot be posted.");
        }

        if (document.Lines == null ||
            document.Lines.Count == 0)
        {
            throw new InvalidOperationException(
                "The inventory document contains no lines.");
        }

        if (document.FromLocationId == null)
        {
            throw new InvalidOperationException(
                "Source location is required.");
        }

        if (document.ToLocationId == null)
        {
            throw new InvalidOperationException(
                "Destination location is required.");
        }

        if (document.FromLocationId ==
            document.ToLocationId)
        {
            throw new InvalidOperationException(
                "Source and destination locations cannot be the same.");
        }

        if (document.DocumentDate == default)
        {
            throw new InvalidOperationException(
                "Document date is required.");
        }
    }

    // =========================================================
    // Location loading
    // =========================================================

    private async Task<Dictionary<int, InventoryLocation>>
        LoadLocationsAsync(
            InventoryDocument document)
    {
        var locationIds = new[]
        {
            document.FromLocationId!.Value,
            document.ToLocationId!.Value
        }
        .Distinct()
        .ToList();

        return await _db.InventoryLocations
            .Where(x => locationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
    }

    // =========================================================
    // Location validation
    // =========================================================

    private static void ValidateDocumentLocations(
        InventoryDocument document,
        Dictionary<int, InventoryLocation> locations)
    {
        var fromId = document.FromLocationId!.Value;
        var toId = document.ToLocationId!.Value;

        if (!locations.TryGetValue(fromId, out var fromLocation))
        {
            throw new InvalidOperationException(
                "Source inventory location was not found.");
        }

        if (!locations.TryGetValue(toId, out var toLocation))
        {
            throw new InvalidOperationException(
                "Destination inventory location was not found.");
        }

        if (!fromLocation.IsActive)
        {
            throw new InvalidOperationException(
                "Source inventory location is inactive.");
        }

        if (!toLocation.IsActive)
        {
            throw new InvalidOperationException(
                "Destination inventory location is inactive.");
        }
    }

    // =========================================================
    // Line validation
    // =========================================================

    private static void ValidateLines(
        InventoryDocument document)
    {
        foreach (var line in document.Lines)
        {
            if (line.Item == null)
            {
                throw new InvalidOperationException(
                    $"Item {line.ItemId} could not be loaded.");
            }

            if (line.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    $"Quantity must be greater than zero for item '{line.Item.Name}'.");
            }

            if (line.UnitId <= 0)
            {
                throw new InvalidOperationException(
                    $"Unit is required for item '{line.Item.Name}'.");
            }

            if (line.UnitId != line.Item.UnitId)
            {
                throw new InvalidOperationException(
                    $"Invalid unit for item '{line.Item.Name}'.");
            }

            if (line.Item.RequiresLotTracking)
            {
                if (string.IsNullOrWhiteSpace(line.LotNumber))
                {
                    throw new InvalidOperationException(
                        $"Lot number is required for item '{line.Item.Name}'.");
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(line.LotNumber))
                {
                    throw new InvalidOperationException(
                        $"Item '{line.Item.Name}' does not use lot tracking.");
                }
            }
        }
    }

    // =========================================================
    // Post one line
    // =========================================================

    private async Task PostLineAsync(
        InventoryDocument document,
        InventoryDocumentLine line,
        Dictionary<int, InventoryLocation> locations,
        string userId)
    {
        var fromLocation =
            locations[document.FromLocationId!.Value];

        var toLocation =
            locations[document.ToLocationId!.Value];

        var sourceCustomerId =
            line.FromCustomerId ?? line.CustomerId;

        var destinationCustomerId =
            line.ToCustomerId ?? line.CustomerId;

        ValidateOwnership(
            document,
            line,
            fromLocation,
            toLocation);

        var lot = await ResolveLotAsync(
            document,
            line);

        var transactionType =
            GetTransactionType(
                document.DocumentType);

        var shouldDecreaseSource =
            ShouldDecreaseSource(
                document.DocumentType,
                fromLocation);

        if (shouldDecreaseSource)
        {
            await DecreaseInventoryBalanceAsync(
                itemId: line.ItemId,
                locationId: fromLocation.Id,
                lotId: lot?.Id,
                customerId: sourceCustomerId,
                quantity: line.Quantity);
        }

        await IncreaseInventoryBalanceAsync(
                itemId: line.ItemId,
                locationId: toLocation.Id,
                lotId: lot?.Id,
                customerId: destinationCustomerId,
                quantity: line.Quantity);

        var inventoryTransaction =
            new InventoryTransaction
            {
                ItemId = line.ItemId,

                InventoryLotId = lot?.Id,

                FromLocationId =
                    fromLocation.Id,

                ToLocationId =
                    toLocation.Id,

                CustomerId =
                    destinationCustomerId,

                Quantity =
                    line.Quantity,

                UnitId =
                    line.UnitId,

                TransactionType =
                    transactionType,

                TransactionDate =
                    document.DocumentDate,

                UserId =
                    userId,

                ReferenceId =
                    document.Id,

                Notes =
                    line.Notes ?? document.Notes ?? "",

                InventoryDocumentLineId =
                    line.Id
            };

        _db.InventoryTransactions.Add(
            inventoryTransaction);

        line.InventoryLotId =
            lot?.Id;
    }

    // =========================================================
    // Ownership validation
    // =========================================================

    private static void ValidateOwnership(
        InventoryDocument document,
        InventoryDocumentLine line,
        InventoryLocation fromLocation,
        InventoryLocation toLocation)
    {
        switch (document.DocumentType)
        {
            case InventoryDocumentType.GoodsReceipt:

                // Goods received from supplier become
                // company-owned unless a customer owner
                // was explicitly assigned.
                break;

            case InventoryDocumentType.CustomerReceipt:

                if (line.CustomerId == null)
                {
                    throw new InvalidOperationException(
                        $"Customer ownership is required for item '{line.Item.Name}' " +
                        "on a Customer Receipt.");
                }

                break;

            case InventoryDocumentType.MaterialTransfer:

                // Ownership is preserved.
                break;

            case InventoryDocumentType.ProductionIssue:

                if (line.CustomerId != null)
                {
                    throw new InvalidOperationException(
                        "Production Issue currently supports company-owned inventory only.");
                }

                break;

            case InventoryDocumentType.ProductionReceipt:

                if (line.CustomerId != null)
                {
                    throw new InvalidOperationException(
                        "Production Receipt currently supports company-owned inventory only.");
                }

                break;

            case InventoryDocumentType.SalesDelivery:

                if (line.FromCustomerId != null)
                {
                    throw new InvalidOperationException(
                        $"Sales Delivery source inventory must be company-owned " +
                        $"for item '{line.Item.Name}'.");
                }

                if (line.ToCustomerId == null)
                {
                    throw new InvalidOperationException(
                        $"Destination customer ownership is required for item " +
                        $"'{line.Item.Name}' on a Sales Delivery.");
                }

                break;

            case InventoryDocumentType.ProductionScrap:

                if (line.CustomerId != null)
                {
                    throw new InvalidOperationException(
                        "Production Scrap currently supports company-owned inventory only.");
                }

                break;

            case InventoryDocumentType.InventoryAdjustment:

                // Ownership may be company or customer.
                break;

            case InventoryDocumentType.CustomerReturn:

                if (line.CustomerId == null)
                {
                    throw new InvalidOperationException(
                        $"Customer ownership is required for item '{line.Item.Name}' " +
                        "on a Customer Return.");
                }

                break;

            case InventoryDocumentType.SupplierReturn:

                if (line.CustomerId != null)
                {
                    throw new InvalidOperationException(
                        "Supplier Return must use company-owned inventory.");
                }

                break;

            default:

                throw new InvalidOperationException(
                    $"Unsupported inventory document type: {document.DocumentType}.");
        }
    }

    // =========================================================
    // Lot resolution
    // =========================================================

    private async Task<InventoryLot?> ResolveLotAsync(
        InventoryDocument document,
        InventoryDocumentLine line)
    {
        if (!line.Item.RequiresLotTracking)
        {
            return null;
        }

        var lotNumber =
            line.LotNumber!.Trim();

        var existingLot = await _db.InventoryLots
            .FirstOrDefaultAsync(x =>
                x.ItemId == line.ItemId &&
                x.LotNumber == lotNumber);

        if (existingLot != null)
        {
            if (existingLot.IsClosed)
            {
                throw new InvalidOperationException(
                    $"Lot '{lotNumber}' for item '{line.Item.Name}' is closed.");
            }

            if (line.LotExpirationDate.HasValue &&
                existingLot.ExpirationDate.HasValue &&
                existingLot.ExpirationDate.Value.Date !=
                line.LotExpirationDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Expiration date for lot '{lotNumber}' does not match the existing lot.");
            }

            return existingLot;
        }

        var lot = new InventoryLot
        {
            ItemId =
                line.ItemId,

            LotNumber =
                lotNumber,

            SerialNumber =
                null,

            ReceivedDate =
                document.DocumentDate,

            ExpirationDate =
                line.LotExpirationDate,

            OriginalQuantity =
                line.Quantity,

            UnitId =
                line.UnitId,

            IsClosed =
                false
        };

        _db.InventoryLots.Add(lot);

        await _db.SaveChangesAsync();

        return lot;
    }

    // =========================================================
    // Source stock
    // =========================================================

    private async Task DecreaseInventoryBalanceAsync(
        int itemId,
        int locationId,
        long? lotId,
        int? customerId,
        decimal quantity)
    {
        var balance = await _db.InventoryBalances
            .FirstOrDefaultAsync(x =>
                x.ItemId == itemId &&
                x.InventoryLocationId == locationId &&
                x.InventoryLotId == lotId &&
                x.CustomerId == customerId);

        if (balance == null)
        {
            throw new InvalidOperationException(
                "Insufficient inventory. No source stock balance exists.");
        }

        if (balance.OnHandQuantity < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient inventory. " +
                $"Available: {balance.OnHandQuantity}, " +
                $"Requested: {quantity}.");
        }

        if (balance.ReservedQuantity > balance.OnHandQuantity)
        {
            throw new InvalidOperationException(
                "Inventory balance is invalid because reserved quantity " +
                "is greater than on-hand quantity.");
        }

        var availableQuantity =
            balance.OnHandQuantity -
            balance.ReservedQuantity;

        if (availableQuantity < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient available inventory. " +
                $"Available: {availableQuantity}, " +
                $"Requested: {quantity}.");
        }

        balance.OnHandQuantity -= quantity;

        balance.LastUpdated =
            DateTime.Now;

        if (balance.OnHandQuantity == 0)
        {
            balance.OnHandQuantity = 0;

            if (balance.ReservedQuantity > 0)
            {
                throw new InvalidOperationException(
                    "Cannot reduce inventory to zero while stock remains reserved.");
            }

            await CloseLotIfEmptyAsync(
                lotId,
                itemId,
                customerId,
                locationId);
        }
    }

    // =========================================================
    // Destination stock
    // =========================================================

    private async Task IncreaseInventoryBalanceAsync(
        int itemId,
        int locationId,
        long? lotId,
        int? customerId,
        decimal quantity)
    {
        var balance = await _db.InventoryBalances
            .FirstOrDefaultAsync(x =>
                x.ItemId == itemId &&
                x.InventoryLocationId == locationId &&
                x.InventoryLotId == lotId &&
                x.CustomerId == customerId);

        if (balance == null)
        {
            balance = new InventoryBalance
            {
                ItemId =
                    itemId,

                InventoryLocationId =
                    locationId,

                InventoryLotId =
                    lotId,

                CustomerId =
                    customerId,

                OnHandQuantity =
                    quantity,

                ReservedQuantity =
                    0,

                LastUpdated =
                    DateTime.Now
            };

            _db.InventoryBalances.Add(
                balance);

            return;
        }

        balance.OnHandQuantity +=
            quantity;

        balance.LastUpdated =
            DateTime.Now;
    }

    // =========================================================
    // Lot closing
    // =========================================================

    private async Task CloseLotIfEmptyAsync(
        long? lotId,
        int itemId,
        int? customerId,
        int locationId)
    {
        if (lotId == null)
            return;

        var remainingQuantity =
            await _db.InventoryBalances
                .Where(x =>
                    x.ItemId == itemId &&
                    x.InventoryLotId == lotId &&
                    x.CustomerId == customerId)
                .SumAsync(x => (decimal?)x.OnHandQuantity)
                ?? 0;

        if (remainingQuantity > 0)
            return;

        var lot =
            await _db.InventoryLots
                .FirstOrDefaultAsync(x =>
                    x.Id == lotId.Value);

        if (lot != null)
        {
            lot.IsClosed = true;
        }
    }

    // =========================================================
    // Transaction type
    // =========================================================

    private static InventoryTransactionType
        GetTransactionType(
            InventoryDocumentType documentType)
    {
        return documentType switch
        {
            InventoryDocumentType.GoodsReceipt =>
                InventoryTransactionType.Receive,

            InventoryDocumentType.CustomerReceipt =>
                InventoryTransactionType.Receive,

            InventoryDocumentType.MaterialTransfer =>
                InventoryTransactionType.Transfer,

            InventoryDocumentType.ProductionIssue =>
                InventoryTransactionType.Consume,

            InventoryDocumentType.ProductionReceipt =>
                InventoryTransactionType.Produce,

            InventoryDocumentType.ProductionScrap =>
                InventoryTransactionType.Scrap,

            InventoryDocumentType.SalesDelivery =>
                InventoryTransactionType.Transfer,

            InventoryDocumentType.InventoryAdjustment =>
                InventoryTransactionType.Adjustment,

            InventoryDocumentType.CustomerReturn =>
                InventoryTransactionType.Return,

            InventoryDocumentType.SupplierReturn =>
                InventoryTransactionType.Return,

            _ => throw new InvalidOperationException(
                $"No transaction type is configured for {documentType}.")
        };
    }

    // =========================================================
    // Source movement rules
    // =========================================================

    private static bool ShouldDecreaseSource(
    InventoryDocumentType documentType,
    InventoryLocation fromLocation)
    {
        return documentType switch
        {
            InventoryDocumentType.GoodsReceipt =>
                false,

            InventoryDocumentType.CustomerReceipt =>
                false,

            InventoryDocumentType.MaterialTransfer =>
                true,

            InventoryDocumentType.ProductionIssue =>
                true,

            InventoryDocumentType.ProductionReceipt =>
                true,

            InventoryDocumentType.ProductionScrap =>
                true,

            InventoryDocumentType.SalesDelivery =>
                true,

            InventoryDocumentType.InventoryAdjustment =>
                true,

            InventoryDocumentType.CustomerReturn =>
                false,

            InventoryDocumentType.SupplierReturn =>
                true,

            _ => throw new InvalidOperationException(
                $"No source movement rule is configured for {documentType}.")
        };
    }
}