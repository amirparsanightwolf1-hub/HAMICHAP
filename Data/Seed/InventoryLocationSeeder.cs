using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Data.Seed;

public static class InventoryLocationSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (!await db.InventoryLocations.AnyAsync())
        {
            db.InventoryLocations.AddRange(

                new InventoryLocation
                {
                    Code = "WH01",
                    Name = "Main Warehouse",
                    Type = InventoryLocationType.Warehouse,
                    IsActive = true
                },


                new InventoryLocation
                {
                    Code = "SUP01",
                    Name = "Default Supplier",
                    Type = InventoryLocationType.Supplier,
                    IsActive = true
                },


                new InventoryLocation
                {
                    Code = "REWORK01",
                    Name = "Rework Area",
                    Type = InventoryLocationType.Rework,
                    IsActive = true
                }

            );


            await db.SaveChangesAsync();
        }
    }
}