using System.Text.Json;
using HamiChap_MES_ERP.Models.Sales;
using HamiChap_MES_ERP.Models.Sales.Enums;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Initial machine route inferred from fields saved by SheetJobEdit and RollJobEdit.
/// It is a suggestion, never a replacement for a Production Manager's edited stages.
/// The first token of every generated name retains its capability group for re-planning.
/// Unmapped finishing services deliberately remain unassigned for manager review.
/// </summary>
public static class ProductionJobRouting
{
    public sealed record SuggestedStage(string Name, string[] MachineCodes);

    public static IReadOnlyList<SuggestedStage> FromJob(SalesJobType type, string? json)
    {
        if (String.IsNullOrWhiteSpace(json)) return Array.Empty<SuggestedStage>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var stages = new List<SuggestedStage>();

        void Add(string name, params string[] machines)
        {
            if (!stages.Any(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                stages.Add(new SuggestedStage(name, machines));
        }

        if (type == SalesJobType.Sheet)
        {
            var sheet = JsonSerializer.Deserialize<SheetJobSpecification>(json) ?? new();
            if (sheet.BeforeCutLengthCm.HasValue || sheet.BeforeCutWidthCm.HasValue ||
                sheet.CutLengthCm.HasValue || sheet.CutWidthCm.HasValue)
                Add("POLAR | Sheet cutting", "POLAR");

            // Sheet jobs normally require offset printing; the manager may remove
            // this proposed operation for finishing-only requests.
            Add("HEIDELBERG | Sheet offset printing", "HEIDELBERG");

            foreach (var service in sheet.SelectedServices ?? new List<string>())
                AddFinishing(service, true, Add);

            if (!String.IsNullOrWhiteSpace(sheet.Die))
                Add("SBL | Die cutting", "SBL");

            var finish = ReadString(root, "FinishType");
            if (finish.Contains("varnish", StringComparison.OrdinalIgnoreCase) ||
                finish.Contains("hybrid", StringComparison.OrdinalIgnoreCase))
                Add("HEIDELBERG | Cylinder coating", "HEIDELBERG");
            if (finish.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
                finish.Contains("سیلک", StringComparison.OrdinalIgnoreCase))
                Add("SAKURAI | Screen printing", "SAKURAI");
        }
        else if (type == SalesJobType.Roll)
        {
            var roll = JsonSerializer.Deserialize<RollJobSpecification>(json) ?? new();
            Add("NILPETER | Roll offset / flexo printing", "NILPETER-1", "NILPETER-2");

            foreach (var service in roll.SelectedServices ?? new List<string>())
                AddFinishing(service, false, Add);
            if (roll.MachineGluing)
                Add("FOLDER-GLUER | Machine gluing", "FOLDER-GLUER");

            if (root.TryGetProperty("WorkflowFields", out var workflow) &&
                workflow.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(workflow, "Lamination"))
                    Add("WENCHUN | Roll lamination", "WENCHUN");
                if (ReadBool(workflow, "Die") || ReadBool(workflow, "DieCutSlitting"))
                    Add("REVIEW | Roll die cutting / slitting");
                if (ReadBool(workflow, "FoilStamping"))
                    Add("REVIEW | Roll foil stamping");
            }
        }
        return stages;
    }

    public static string[] Candidates(string operationName)
    {
        var prefix = (operationName ?? "").Split(" | ", 2, StringSplitOptions.None)[0]
            .Trim().ToUpperInvariant();
        return prefix switch
        {
            "POLAR" => new[] { "POLAR" },
            "HEIDELBERG" => new[] { "HEIDELBERG" },
            "NILPETER" => new[] { "NILPETER-1", "NILPETER-2" },
            "WENCHUN" => new[] { "WENCHUN" },
            "UV" => new[] { "TOPS-102", "HEIDELBERG" },
            "SAKURAI" => new[] { "SAKURAI" },
            "SBL" => new[] { "SBL" },
            "FOLDER-GLUER" => new[] { "FOLDER-GLUER" },
            _ => Array.Empty<string>()
        };
    }

    private static void AddFinishing(string service, bool sheet,
        Action<string, string[]> add)
    {
        if (String.IsNullOrWhiteSpace(service)) return;
        if (service.Contains("سلفون") || service.Contains("لمینیت") ||
            service.Contains("laminat", StringComparison.OrdinalIgnoreCase))
            add("WENCHUN | " + service, new[] { "WENCHUN" });
        else if (service.Contains("یووی") || service.Contains("UV", StringComparison.OrdinalIgnoreCase))
            add("UV | " + service, new[] { "TOPS-102", "HEIDELBERG" });
        else if (service.Contains("جعبه") && service.Contains("چسب"))
            add("FOLDER-GLUER | " + service, new[] { "FOLDER-GLUER" });
        else if (service.Contains("برش و بسته"))
            add("POLAR | " + service, new[] { "POLAR" });
        else if (service.Contains("قالب") || service.Contains("دیلن"))
        {
            if (sheet) add("SBL | " + service, new[] { "SBL" });
            else add("REVIEW | " + service, Array.Empty<string>());
        }
        else
            add("REVIEW | " + service, Array.Empty<string>());
    }

    private static string ReadString(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";

    private static bool ReadBool(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value)) return false;
        return value.ValueKind == JsonValueKind.True ||
            (value.ValueKind == JsonValueKind.String &&
             Boolean.TryParse(value.GetString(), out var enabled) && enabled);
    }
}
