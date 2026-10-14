using HamiChap_MES_ERP.Models.Administration;
using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Inventory;
using HamiChap_MES_ERP.Models.Production;
using HamiChap_MES_ERP.Models.Production.Quality;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Shared;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HamiChap_MES_ERP.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {

            builder.Entity<InventoryBalance>()
                .Property(x => x.OnHandQuantity)
                .HasPrecision(18, 3);

            builder.Entity<InventoryBalance>()
                .Property(x => x.ReservedQuantity)
                .HasPrecision(18, 3);

            builder.Entity<InventoryLot>()
                .Property(x => x.OriginalQuantity)
                .HasPrecision(18, 3);

            builder.Entity<InventoryTransaction>()
                .Property(x => x.Quantity)
                .HasPrecision(18, 3);

            builder.Entity<InventoryDocumentLine>()
                .Property(x => x.Quantity)
                .HasPrecision(18, 3);

            builder.Entity<Item>()
                .Property(x => x.LowStockThreshold)
                .HasPrecision(18, 3);

            builder.Entity<SalesRequestLine>()
                .Property(x => x.Quantity)
                .HasPrecision(18, 3);

            builder.Entity<BillOfMaterialLine>()
                .Property(x => x.Quantity)
                .HasPrecision(18, 4);

            builder.Entity<QualityInspection>()
                .Property(x => x.InspectedQuantity)
                .HasPrecision(18, 4);

            builder.Entity<QualityInspection>()
                .Property(x => x.AcceptedQuantity)
                .HasPrecision(18, 4);

            builder.Entity<QualityInspection>()
                .Property(x => x.RejectedQuantity)
                .HasPrecision(18, 4);


            builder.Entity<QualityInspectionResult>()
                .Property(x => x.MeasuredValue)
                .HasPrecision(18, 4);

            builder.Entity<QualityInspectionResult>()
                .Property(x => x.MinimumValue)
                .HasPrecision(18, 4);

            builder.Entity<QualityInspectionResult>()
                .Property(x => x.MaximumValue)
                .HasPrecision(18, 4);

            builder.Entity<SalesDeliveryAllocation>()
                .Property(x => x.Quantity)
                .HasPrecision(18, 4);


            base.OnModelCreating(builder);

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    builder.Entity(entityType.ClrType)
                        .Property(nameof(BaseEntity.RowVersion))
                        .IsRowVersion();
                }
            }

            builder.Entity<SalesDeliveryAllocation>()
                .HasOne(x => x.SalesDeliveryLine)
                .WithMany(x => x.Allocations)
                .HasForeignKey(x => x.SalesDeliveryLineId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<SalesDeliveryAllocation>()
                .HasOne(x => x.InventoryLot)
                .WithMany()
                .HasForeignKey(x => x.InventoryLotId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryDocumentLine>()
                .HasOne(x => x.FromCustomer)
                .WithMany()
                .HasForeignKey(x => x.FromCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryDocumentLine>()
                .HasOne(x => x.ToCustomer)
                .WithMany()
                .HasForeignKey(x => x.ToCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalesDelivery>()
                .HasOne(x => x.SalesOrder)
                .WithMany()
                .HasForeignKey(x => x.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalesDelivery>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalesDelivery>()
                .HasOne(x => x.FromLocation)
                .WithMany()
                .HasForeignKey(x => x.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalesDelivery>()
                .HasOne(x => x.ToLocation)
                .WithMany()
                .HasForeignKey(x => x.ToLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalesDeliveryLine>()
                .HasOne(x => x.SalesDelivery)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.SalesDeliveryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SalesDeliveryLine>()
                .HasOne(x => x.SalesOrderLine)
                .WithMany()
                .HasForeignKey(x => x.SalesOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalesDeliveryLine>()
                .HasOne(x => x.InventoryDocument)
                .WithMany()
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalesDeliveryLine>()
                .Property(x => x.Quantity)
                .HasPrecision(18, 4);

            builder.Entity<InventoryDocument>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryDocument>()
                .HasOne(x => x.QualityInspection)
                .WithMany()
                .HasForeignKey(x => x.QualityInspectionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QualityInspection>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<QualityInspectionResult>()
                .HasOne(x => x.QualityInspection)
                .WithMany(x => x.Results)
                .HasForeignKey(x => x.QualityInspectionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MachineCalendar>()
                .HasOne(x => x.Machine)
                .WithMany()
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<ProductionSchedule>()
                .HasOne(x => x.ProductionOperation)
                .WithMany()
                .HasForeignKey(x => x.ProductionOperationId)
                .OnDelete(DeleteBehavior.Cascade);



            builder.Entity<ProductionSchedule>()
                .HasOne(x => x.Machine)
                .WithMany()
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductionMaterialConsumption>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<ProductionReceipt>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductionOperation>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);



            builder.Entity<ProductionOperation>()
                .HasOne(x => x.WorkCenter)
                .WithMany(x => x.Operations)
                .HasForeignKey(x => x.WorkCenterId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<ProductionOutput>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProductionMaterialReservation>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);



            builder.Entity<ProductionMaterialReservation>()
                .HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MaterialRequirementPlan>()
                .HasOne(x => x.ProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);



            builder.Entity<MaterialRequirementPlanLine>()
                .HasOne(x => x.Plan)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.MaterialRequirementPlanId)
                .OnDelete(DeleteBehavior.Cascade);



            builder.Entity<MaterialRequirementPlanLine>()
                .HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BillOfMaterial>()
                .HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<BillOfMaterialLine>()
                .HasOne(x => x.BillOfMaterial)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.BillOfMaterialId)
                .OnDelete(DeleteBehavior.Cascade);



            builder.Entity<BillOfMaterialLine>()
                .HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductionOrder>()
                .HasOne(x => x.SalesOrder)
                .WithMany()
                .HasForeignKey(x => x.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<ProductionOrderLine>()
                .HasOne(x => x.ProductionOrder)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SalesOrder>()
                .HasOne(x => x.Quotation)
                .WithMany()
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<SalesOrder>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<SalesOrderLine>()
                .HasOne(x => x.SalesOrder)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Quotation>()
                .HasOne(x => x.SalesRequest)
                .WithMany()
                .HasForeignKey(x => x.SalesRequestId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<Quotation>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);



            builder.Entity<QuotationLine>()
                .HasOne(x => x.Quotation)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);



            builder.Entity<ApprovalHistory>()
                .HasOne(x => x.Quotation)
                .WithMany()
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SalesEvaluation>()
                .HasOne(x => x.SalesRequest)
                .WithMany()
                .HasForeignKey(x => x.SalesRequestId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<SalesEvaluationLine>()
                .HasOne(x => x.SalesEvaluation)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.SalesEvaluationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SalesRequest>()
                .HasIndex(x => x.RequestNumber)
                .IsUnique();

            builder.Entity<SalesRequest>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<SalesRequest>()
                .HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<SalesRequest>()
                .HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<SalesRequestLine>()
                .HasOne(x => x.SalesRequest)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.SalesRequestId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<SalesRequestLine>()
                .HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<SalesRequestLine>()
                .HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasOne(t => t.ToLocation)
                .WithMany()
                .HasForeignKey(t => t.ToLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasOne(t => t.FromLocation)
                .WithMany()
                .HasForeignKey(t => t.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasOne(x => x.InventoryDocumentLine)
                .WithMany()
                .HasForeignKey(x => x.InventoryDocumentLineId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryDocumentLine>()
                .HasOne(x => x.InventoryDocument)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InventoryDocumentLine>()
                .HasOne(x => x.InventoryLot)
                .WithMany()
                .HasForeignKey(x => x.InventoryLotId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryDocumentLine>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryDocumentLine>()
                .HasIndex(x => x.CustomerId);

            builder.Entity<Item>()
                .HasOne(i => i.Unit)
                .WithMany()
                .HasForeignKey(i => i.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Item>()
                .HasOne(i => i.Category)
                .WithMany()
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryLot>()
                .HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryLot>()
                .HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransaction>()
                .HasIndex(x => x.ItemId);

            builder.Entity<InventoryTransaction>()
                .HasIndex(x => x.TransactionDate);

            builder.Entity<InventoryTransaction>()
                .HasIndex(x => x.FromLocationId);

            builder.Entity<InventoryTransaction>()
                .HasIndex(x => x.ToLocationId);

            builder.Entity<InventoryLot>()
                .HasIndex(x => x.LotNumber);

            builder.Entity<InventoryDocument>()
                .HasIndex(x => x.DocumentNumber)
                .IsUnique();

            builder.Entity<Item>()
                .HasIndex(x => x.Code)
                .IsUnique();

            builder.Entity<Customer>()
                .HasIndex(x => x.Code)
                .IsUnique();

            builder.Entity<InventoryTransaction>()
                .HasIndex(x => x.CustomerId);

            builder.Entity<InventoryLocation>()
                .HasIndex(x => x.Code)
                .IsUnique();

            builder.Entity<NumberSeries>()
                .HasIndex(x => new
                {
                    x.Type,
                    x.Year
                })
                .IsUnique();

            builder.Entity<InventoryBalance>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryBalance>()
                .HasIndex(x => new
                {
                    x.ItemId,
                    x.InventoryLocationId,
                    x.InventoryLotId,
                    x.CustomerId
                })
                .IsUnique();

            builder.Entity<InventoryDocument>()
                .HasOne(x => x.FromLocation)
                .WithMany()
                .HasForeignKey(x => x.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryDocument>()
                .HasOne(x => x.ToLocation)
                .WithMany()
                .HasForeignKey(x => x.ToLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RolePermissionManagement>()
                .HasOne(x => x.Permission)
                .WithMany()
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RolePermission>()
                .HasOne(x => x.Permission)
                .WithMany()
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RolePermissionManagement>()
                .HasIndex(x => new
                {
                    x.ManagerRoleId,
                    x.TargetRoleId,
                    x.PermissionId
                })
                .IsUnique();

            builder.Entity<RolePermission>()
                .HasIndex(x => new
                {
                    x.RoleId,
                    x.PermissionId
                })
                .IsUnique();

            builder.Entity<Permission>()
                .HasIndex(x => x.Name)
                .IsUnique();
        }

        public DbSet<Permission> Permissions { get; set; }

        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<Customer> Customers { get; set; }

        public DbSet<Unit> Units => Set<Unit>();

        public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

        public DbSet<Item> Items => Set<Item>();

        public DbSet<Machine> Machines => Set<Machine>();

        public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    
        public DbSet<InventoryTransaction> InventoryTransactions
    => Set<InventoryTransaction>();

        public DbSet<InventoryBalance> InventoryBalances
    => Set<InventoryBalance>();

        public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();

        public DbSet<InventoryDocument> InventoryDocuments
    => Set<InventoryDocument>();

        public DbSet<InventoryDocumentLine> InventoryDocumentLines
            => Set<InventoryDocumentLine>();

        public DbSet<InventoryLot> InventoryLots
            => Set<InventoryLot>();

        public DbSet<RolePermissionManagement> RolePermissionManagements
    => Set<RolePermissionManagement>();

        public DbSet<SalesRequest> SalesRequests => Set<SalesRequest>();

        public DbSet<SalesRequestLine> SalesRequestLines => Set<SalesRequestLine>();

        public DbSet<SalesEvaluation> SalesEvaluations
    => Set<SalesEvaluation>();

        public DbSet<SalesEvaluationLine> SalesEvaluationLines
            => Set<SalesEvaluationLine>();

        public DbSet<Quotation> Quotations
    => Set<Quotation>();

        public DbSet<QuotationLine> QuotationLines
            => Set<QuotationLine>();

        public DbSet<ApprovalHistory> ApprovalHistories
            => Set<ApprovalHistory>();
        public DbSet<SalesOrder> SalesOrders
    => Set<SalesOrder>();

        public DbSet<SalesOrderLine> SalesOrderLines
            => Set<SalesOrderLine>();
        public DbSet<ProductionOrder> ProductionOrders
    => Set<ProductionOrder>();

        public DbSet<ProductionOrderLine> ProductionOrderLines
            => Set<ProductionOrderLine>();
        public DbSet<BillOfMaterial> BillOfMaterials
    => Set<BillOfMaterial>();
        public DbSet<BillOfMaterialLine> BillOfMaterialLines
            => Set<BillOfMaterialLine>();
        public DbSet<MaterialRequirementPlan> MaterialRequirementPlans
    => Set<MaterialRequirementPlan>();
        public DbSet<MaterialRequirementPlanLine> MaterialRequirementPlanLines
            => Set<MaterialRequirementPlanLine>();

        public DbSet<ProductionMaterialReservation> ProductionMaterialReservations
    => Set<ProductionMaterialReservation>();
        public DbSet<ProductionWorkCenter> ProductionWorkCenters
    => Set<ProductionWorkCenter>();
        public DbSet<ProductionOperation> ProductionOperations
            => Set<ProductionOperation>();
        public DbSet<ProductionOutput> ProductionOutputs
            => Set<ProductionOutput>();
        public DbSet<ProductionMaterialConsumption> ProductionMaterialConsumptions
    => Set<ProductionMaterialConsumption>();


        public DbSet<ProductionReceipt>
            ProductionReceipts
            => Set<ProductionReceipt>();
        public DbSet<MachineCalendar> MachineCalendars
    => Set<MachineCalendar>();


        public DbSet<ProductionSchedule> ProductionSchedules
            => Set<ProductionSchedule>();
        public DbSet<QualityInspection>
    QualityInspections
    => Set<QualityInspection>();

        public DbSet<QualityInspectionResult>
            QualityInspectionResults
            => Set<QualityInspectionResult>();

        public DbSet<SalesDelivery>
    SalesDeliveries
    => Set<SalesDelivery>();

        public DbSet<SalesDeliveryLine>
            SalesDeliveryLines
            => Set<SalesDeliveryLine>();

        public DbSet<SalesDeliveryAllocation>
    SalesDeliveryAllocations
    => Set<SalesDeliveryAllocation>();
    }

}
