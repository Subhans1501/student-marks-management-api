namespace MarksApi.Dtos.Reports;

public class CourseStatsResponse
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public int Enrolled { get; set; }
    public int Graded { get; set; }
    public double? Average { get; set; }
    public int? Highest { get; set; }
    public int? Lowest { get; set; }
    public int Passed { get; set; }
}

public class TranscriptCourseItem
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public int? Mark { get; set; }
    public bool Passed { get; set; }
}

public class TranscriptResponse
{
    public int StudentId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<TranscriptCourseItem> Courses { get; set; } = Array.Empty<TranscriptCourseItem>();
    public double? WeightedAverage { get; set; }
}

