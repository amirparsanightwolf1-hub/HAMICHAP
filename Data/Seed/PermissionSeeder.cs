using HamiChap_MES_ERP.Data;
using HamiChap_MES_ERP.Models.Administration;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Data.Seed;

public static class PermissionSeeder
{
    public static async Task SeedPermissionsAsync(
        ApplicationDbContext context)
    {
        var permissions = new List<Permission>
        {
            // =========================
            // Users
            // =========================

            new()
            {
                Name = "Users.View",
                Description = "View employees and users"
            },

            new()
            {
                Name = "Users.Create",
                Description = "Create new employees and users"
            },

            new()
            {
                Name = "Users.Edit",
                Description = "Edit employee and user information"
            },

            new()
            {
                Name = "Users.Disable",
                Description = "Disable or deactivate users"
            },


            // =========================
            // Customers
            // =========================

            new()
            {
                Name = "Customers.View",
                Description = "View customers"
            },

            new()
            {
                Name = "Customers.Create",
                Description = "Create customers"
            },

            new()
            {
                Name = "Customers.Edit",
                Description = "Edit customers"
            },

            new()
            {
                Name = "Customers.Delete",
                Description = "Delete customers"
            },


            // =========================
            // Inventory Documents
            // =========================

            new()
            {
                Name = "Inventory.Documents.View",
                Description = "View inventory documents"
            },

            new()
            {
                Name = "Inventory.Documents.Create",
                Description = "Create inventory documents"
            },

            new()
            {
                Name = "Inventory.Documents.Edit",
                Description = "Edit inventory documents"
            },

            new()
            {
                Name = "Inventory.Documents.Post",
                Description = "Post inventory documents"
            },


            // =========================
            // Inventory Items
            // =========================

            new()
            {
                Name = "Inventory.Items.View",
                Description = "View inventory items"
            },

            new()
            {
                Name = "Inventory.Items.Create",
                Description = "Create inventory items"
            },

            new()
            {
                Name = "Inventory.Items.Edit",
                Description = "Edit inventory items"
            },


            // =========================
            // Inventory Units
            // =========================

            new()
            {
                Name = "Inventory.Units.View",
                Description = "View inventory units"
            },

            new()
            {
                Name = "Inventory.Units.Create",
                Description = "Create inventory units"
            },

            new()
            {
                Name = "Inventory.Units.Edit",
                Description = "Edit inventory units"
            },


            // =========================
            // Inventory Categories
            // =========================

            new()
            {
                Name = "Inventory.Categories.View",
                Description = "View inventory categories"
            },

            new()
            {
                Name = "Inventory.Categories.Create",
                Description = "Create inventory categories"
            },

            new()
            {
                Name = "Inventory.Categories.Edit",
                Description = "Edit inventory categories"
            },


            // =========================
            // Inventory Lots
            // =========================

            new()
            {
                Name = "Inventory.Lots.View",
                Description = "View inventory lots"
            },

            new()
            {
                Name = "Inventory.Lots.Create",
                Description = "Create inventory lots"
            },

            new()
            {
                Name = "Inventory.Lots.Edit",
                Description = "Edit inventory lots"
            },


            // =========================
            // Inventory Stock
            // =========================

            new()
            {
                Name = "Inventory.Stock.View",
                Description = "View inventory stock"
            },

            new()
            {
                Name = "Inventory.Stock.Receive",
                Description = "Receive materials into inventory"
            },

            new()
            {
                Name = "Inventory.Stock.Move",
                Description = "Move materials between locations"
            },

            new()
            {
                Name = "Inventory.Stock.Adjust",
                Description = "Adjust inventory quantities"
            },


            // =========================
            // Inventory Locations
            // =========================

            new()
            {
                Name = "Inventory.Locations.View",
                Description = "View inventory locations"
            },

            new()
            {
                Name = "Inventory.Locations.Create",
                Description = "Create inventory locations"
            },

            new()
            {
                Name = "Inventory.Locations.Edit",
                Description = "Edit inventory locations"
            },


            // =========================
            // Inventory Transactions
            // =========================

            new()
            {
                Name = "Inventory.Transactions.View",
                Description = "View inventory transactions"
            },


            // =========================
            // Machines
            // =========================

            new()
            {
                Name = "Machines.View",
                Description = "View machines"
            },

            new()
            {
                Name = "Machines.Create",
                Description = "Create machines"
            },

            new()
            {
                Name = "Machines.Edit",
                Description = "Edit machines"
            },

            new()
            {
                Name = "Machines.Delete",
                Description = "Delete machines"
            },


            // =========================
            // Production
            // =========================

            new()
            {
                Name = "Production.View",
                Description = "View production jobs"
            },

            new()
            {
                Name = "Production.Create",
                Description = "Create production jobs"
            },

            new()
            {
                Name = "Production.Edit",
                Description = "Edit production jobs"
            },

            new()
            {
                Name = "Production.Start",
                Description = "Start production operations"
            },

            new()
            {
                Name = "Production.Complete",
                Description = "Complete production operations"
            },


            // =========================
            // Reports
            // =========================

            new()
            {
                Name = "Reports.View",
                Description = "View reports"
            },


            // =========================
            // Roles
            // =========================

            new()
            {
                Name = "Roles.View",
                Description = "View roles"
            },

            new()
            {
                Name = "Roles.Create",
                Description = "Create roles"
            },

            new()
            {
                Name = "Roles.Delete",
                Description = "Delete roles"
            },

            new()
            {
                Name = "Roles.ManagePermissions",
                Description = "Manage permissions assigned to roles"
            },


            // =========================
            // Sales Requests
            // =========================

            new()
            {
                Name = "Sales.Requests.View",
                Description = "View sales requests"
            },

            new()
            {
                Name = "Sales.Requests.Create",
                Description = "Create sales requests"
            },

            new()
            {
                Name = "Sales.Requests.Edit",
                Description = "Edit sales requests"
            },

            new()
            {
                Name = "Sales.Requests.Evaluate",
                Description = "Evaluate sales requests"
            },

            new()
            {
                Name = "Sales.Requests.CreateQuotation",
                Description = "Create quotations from sales requests"
            },


            // =========================
            // =========================
            // Sales Quotations
            // =========================

            new()
            {
                Name = "Sales.Quotations.Approve",
                Description = "Approve or reject sales quotations"
            },


            // Sales Deliveries
            // =========================

            new()
            {
                Name = "Sales.Deliveries.View",
                Description = "View sales deliveries"
            },

            new()
            {
                Name = "Sales.Deliveries.Create",
                Description = "Create sales deliveries"
            },

            new()
            {
                Name = "Sales.Deliveries.Edit",
                Description = "Edit sales deliveries"
                },

            new()
            {
                Name = "Sales.Deliveries.Post",
                Description = "Post sales deliveries"
            },

            new()
            {
                Name = "Sales.Deliveries.Cancel",
                Description = "Cancel sales deliveries"
            },

            // =========================
            // Sales Orders
            // =========================

            new()
            {
                Name = "Sales.Orders.View",
                Description = "View sales orders"
            },

        };


        // Only insert permissions that don't already exist.
        // This allows new permissions to be added later without
        // deleting or duplicating existing permissions.

        var existingNames =
            await context.Permissions
                .Select(x => x.Name)
                .ToHashSetAsync();


        var missingPermissions =
            permissions
                .Where(x =>
                    !existingNames.Contains(x.Name))
                .ToList();


        if (missingPermissions.Count == 0)
        {
            return;
        }


        await context.Permissions.AddRangeAsync(
            missingPermissions);

        await context.SaveChangesAsync();
    }
}