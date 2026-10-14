using HamiChap_MES_ERP.Models.Shared;
using HamiChap_MES_ERP.Models.Sales.Enums;

namespace HamiChap_MES_ERP.Models.Sales;

public class SalesEvaluation : BaseEntity
{
    public long Id { get; set; }


    public long SalesRequestId { get; set; }

    public SalesRequest SalesRequest { get; set; } = null!;



    public SalesEvaluationStatus Status { get; set; }
        = SalesEvaluationStatus.Draft;



    // Employee doing evaluation

    public string? EvaluatorUserId { get; set; }

    public ApplicationUser? EvaluatorUser { get; set; }



    // Estimated values

    public decimal EstimatedMaterialCost { get; set; }


    public decimal EstimatedProductionCost { get; set; }


    public decimal EstimatedTotalCost { get; set; }



    public int EstimatedProductionDays { get; set; }



    public string Notes { get; set; } = "";



    public ICollection<SalesEvaluationLine> Lines { get; set; }
        = new List<SalesEvaluationLine>();
}