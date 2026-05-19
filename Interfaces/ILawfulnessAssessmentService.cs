public interface ILawfulnessAssessmentService
{
    Task<LawfulnessAssessmentResult> AssessAsync(Guid fileMasterId);
}
