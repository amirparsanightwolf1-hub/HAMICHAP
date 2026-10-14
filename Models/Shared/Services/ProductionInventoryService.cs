using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;


namespace HamiChap_MES_ERP.Services;


public class ProductionInventoryService
{
    private readonly ApplicationDbContext _db;


    public ProductionInventoryService(
        ApplicationDbContext db)
    {
        _db = db;
    }



    public async Task ConsumeMaterialAsync(
        long productionOrderId,
        int itemId,
        decimal quantity)
    {

        var reservation =
            await _db.ProductionMaterialReservations
            .FirstOrDefaultAsync(x =>
                x.ProductionOrderId ==
                productionOrderId &&
                x.ItemId == itemId &&
                x.Status ==
                ReservationStatus.Reserved);



        if (reservation == null)
            throw new Exception(
                "Material is not reserved");



        if (quantity >
           reservation.Quantity)
            throw new Exception(
                "Cannot consume more than reserved");



        reservation.Quantity -= quantity;



        if (reservation.Quantity == 0)
        {
            reservation.Status =
                ReservationStatus.Consumed;
        }



        await _db.SaveChangesAsync();
    }
}