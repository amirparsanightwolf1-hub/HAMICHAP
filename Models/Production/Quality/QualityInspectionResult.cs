using HamiChap_MES_ERP.Models.Production.Quality;
using HamiChap_MES_ERP.Models.Shared;

namespace HamiChap_MES_ERP.Models.Production;

public class QualityInspectionResult : BaseEntity
{
    public long Id { get; set; }

    public long QualityInspectionId { get; set; }

    public QualityInspection QualityInspection { get; set; }
        = null!;

    public string ParameterName { get; set; } = "";

    public decimal? MeasuredValue { get; set; }

    public string? TextValue { get; set; }

    public decimal? MinimumValue { get; set; }

    public decimal? MaximumValue { get; set; }

    public bool Passed { get; set; }

    public string Notes { get; set; } = "";
}