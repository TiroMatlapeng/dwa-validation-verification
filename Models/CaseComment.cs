public class CaseComment
{
    public Guid CommentId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public Guid? PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public Guid? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
    public required string AuthorType { get; set; } // PublicUser, DWSOfficial
    public Guid? ParentCommentId { get; set; }
    public CaseComment? ParentComment { get; set; }
    public required string CommentText { get; set; }
    public DateTime SubmittedDate { get; set; }
    public DateTime? ReadByDWSDate { get; set; }
    public DateTime? ReadByPublicUserDate { get; set; }
    public ICollection<CaseComment> Replies { get; set; } = new List<CaseComment>();
}
