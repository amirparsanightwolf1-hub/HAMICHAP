using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Data.Seed;

public static class ProductionMachineSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var defaults = new (string Code, string Name)[]
        {
            ("POLAR", "Polar Guillotine"),
            ("HEIDELBERG", "Heidelberg"),
            ("NILPETER-1", "Nilpeter 1"),
            ("NILPETER-2", "Nilpeter 2"),
            ("WENCHUN", "Wenchun"),
            ("TOPS-102", "Tops 102"),
            ("SAKURAI", "Sakurai Screen Printing"),
            ("SBL", "SBL Die Cutter"),
            ("FOLDER-GLUER", "Folder-gluer")
        };
        var existing = await db.Machines.AsNoTracking().ToListAsync();
        foreach (var (code, name) in defaults)
        {
            if (existing.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            db.Machines.Add(new Machine { Code = code, Name = name, IsActive = true });
        }
        await db.SaveChangesAsync();
    }
}
