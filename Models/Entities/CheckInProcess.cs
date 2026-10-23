using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// The Host's check-in rules for one property (one-to-one with HostProperty).
    /// The individual steps are stored as JSON in StepsJson, e.g.
    /// [{"StepNumber":1,"Title":"Upload ID","RequiresDocument":true,"DocumentType":"ID or Passport"}]
    /// </summary>
    public class CheckInProcess
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "Check-in";

        [Required]
        public string StepsJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public int PropertyId { get; set; }
        public HostProperty Property { get; set; } = null!;

        public ICollection<GuestCheckIn> GuestCheckIns { get; set; } = new List<GuestCheckIn>();

        // ---------- Helpers (methods, so EF Core does not try to map them) ----------

        public List<CheckInStep> GetSteps()
        {
            if (string.IsNullOrWhiteSpace(StepsJson)) return new List<CheckInStep>();
            return JsonSerializer.Deserialize<List<CheckInStep>>(StepsJson) ?? new List<CheckInStep>();
        }

        public void SetSteps(IEnumerable<CheckInStep> steps)
        {
            var ordered = steps.OrderBy(s => s.StepNumber).ToList();
            for (int i = 0; i < ordered.Count; i++) ordered[i].StepNumber = i + 1;
            StepsJson = JsonSerializer.Serialize(ordered);
        }

        /// <summary>A sensible default the Host can edit: house rules + ID upload.</summary>
        public static List<CheckInStep> DefaultSteps() => new()
        {
            new CheckInStep
            {
                StepNumber = 1,
                Title = "Confirm your arrival details",
                Instructions = "Confirm your expected arrival time and the number of guests."
            },
            new CheckInStep
            {
                StepNumber = 2,
                Title = "Accept the house rules",
                Instructions = "No smoking, no parties, quiet hours from 22:00 to 07:00."
            },
            new CheckInStep
            {
                StepNumber = 3,
                Title = "Upload identification",
                Instructions = "Upload a clear photo or PDF of your ID or passport for the Host to verify.",
                RequiresDocument = true,
                DocumentType = "ID or Passport"
            }
        };
    }

    /// <summary>
    /// One step of a check-in process. Not a database table: it is serialised into CheckInProcess.StepsJson.
    /// </summary>
    public class CheckInStep
    {
        public int StepNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public bool RequiresDocument { get; set; }
        public string? DocumentType { get; set; }
    }
}
