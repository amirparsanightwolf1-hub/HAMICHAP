using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;


namespace HamiChap_MES_ERP.Data.Seed;


public static class ProductionLocationSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext db)
    {

        if (!await db.InventoryLocations
            .AnyAsync(x =>
                x.Code == "PROD01"))
        {

            db.InventoryLocations.Add(
                new InventoryLocation
                {
                    Code = "PROD01",

                    Name =
                        "Production Floor",

                    Type =
                        InventoryLocationType.Production,

                    IsActive = true
                });


            await db.SaveChangesAsync();
        }
    }
}