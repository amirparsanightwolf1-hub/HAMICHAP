using HamiChap_MES_ERP.Models.Customers;
using HamiChap_MES_ERP.Models.Sales.Enums;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesRequest : BaseEntity
{
    public long Id { get; set; }


    // SR-2026-000001
    public string RequestNumber { get; set; } = "";


    // Customer asking for products
    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;


    // When request was created
    public DateTime RequestDate { get; set; }
        = DateTime.Now;


    // Customer requested delivery date
    public DateTime? RequestedDeliveryDate { get; set; }


    // Workflow status
    public SalesRequestStatus Status { get; set; }
        = SalesRequestStatus.Draft;


    // Employee assigned to evaluate it
    public string? AssignedToUserId { get; set; }

    public ApplicationUser? AssignedToUser { get; set; }


    // Employee who created it
    public string CreatedByUserId { get; set; } = "";

    public ApplicationUser CreatedByUser { get; set; } = null!;


    public string Notes { get; set; } = "";


    public ICollection<SalesRequestLine> Lines { get; set; }
        = new List<SalesRequestLine>();
}