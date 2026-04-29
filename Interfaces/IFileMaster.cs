public interface IFileMaster
{
    Task<FileMaster> AddAsync(FileMaster fileMaster);
    Task<FileMaster?> GetByIdAsync(Guid id);
    Task<ICollection<FileMaster>> ListAllAsync();
    Task<ICollection<FileMaster>> ListByPropertyIdAsync(Guid propertyId);
    Task<FileMaster> UpdateAsync(FileMaster fileMaster);
    Task<FileMaster?> DeleteAsync(Guid id);
    Task<FileMaster?> GetWithWorkflowAsync(Guid id);
}
