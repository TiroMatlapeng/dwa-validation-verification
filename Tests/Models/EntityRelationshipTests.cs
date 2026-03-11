using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Models;

public class EntityRelationshipTests
{
    [Fact]
    public async Task FileMaster_Links_To_Property()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertySize = 150.5m
        };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "WMA01-001",
            SurveyorGeneralCode = "T0LR00000000012300000",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Test Farm",
            FarmNumber = 123,
            RegistrationDivision = "LR",
            FarmPortion = "0"
        };
        context.FileMasters.Add(fileMaster);
        await context.SaveChangesAsync();

        var retrieved = await context.FileMasters
            .Include(fm => fm.Property)
            .FirstAsync(fm => fm.FileMasterId == fileMaster.FileMasterId);

        Assert.NotNull(retrieved.Property);
        Assert.Equal(property.PropertyId, retrieved.Property.PropertyId);
    }

    [Fact]
    public async Task FileMaster_Links_To_Validator_And_CapturePerson()
    {
        using var context = TestDbContextFactory.Create();

        var orgUnit = new OrganisationalUnit
        {
            OrgUnitId = Guid.NewGuid(),
            Name = "Limpopo Regional Office",
            Type = "Regional"
        };
        context.OrganisationalUnits.Add(orgUnit);

        var validator = new ApplicationUser
        {
            ApplicationUserId = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Validator",
            EmployeeNumber = "EMP001",
            OrgUnitId = orgUnit.OrgUnitId
        };
        var capturer = new ApplicationUser
        {
            ApplicationUserId = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Capturer",
            EmployeeNumber = "EMP002",
            OrgUnitId = orgUnit.OrgUnitId
        };
        context.ApplicationUsers.AddRange(validator, capturer);

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "WMA01-002",
            SurveyorGeneralCode = "T0LR00000000012300001",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Validator Farm",
            FarmNumber = 124,
            RegistrationDivision = "LR",
            FarmPortion = "0",
            ValidatorId = validator.ApplicationUserId,
            CapturePersonId = capturer.ApplicationUserId,
            OrgUnitId = orgUnit.OrgUnitId
        };
        context.FileMasters.Add(fileMaster);
        await context.SaveChangesAsync();

        var retrieved = await context.FileMasters
            .Include(fm => fm.Validator)
            .Include(fm => fm.CapturePerson)
            .Include(fm => fm.OrgUnit)
            .FirstAsync(fm => fm.FileMasterId == fileMaster.FileMasterId);

        Assert.Equal("Jane", retrieved.Validator!.FirstName);
        Assert.Equal("John", retrieved.CapturePerson!.FirstName);
        Assert.Equal("Limpopo Regional Office", retrieved.OrgUnit!.Name);
    }

    [Fact]
    public async Task Property_Owns_Multiple_FileMasters()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 200m };
        context.Properties.Add(property);

        var fm1 = new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = property.PropertyId, RegistrationNumber = "REG-001", SurveyorGeneralCode = "T0LR00000000012300002", PrimaryCatchment = "A", QuaternaryCatchment = "A21A", FarmName = "Farm One", FarmNumber = 1, RegistrationDivision = "LR", FarmPortion = "0" };
        var fm2 = new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = property.PropertyId, RegistrationNumber = "REG-002", SurveyorGeneralCode = "T0LR00000000012300003", PrimaryCatchment = "A", QuaternaryCatchment = "A21A", FarmName = "Farm Two", FarmNumber = 2, RegistrationDivision = "LR", FarmPortion = "0" };
        context.FileMasters.AddRange(fm1, fm2);
        await context.SaveChangesAsync();

        var retrieved = await context.Properties
            .Include(p => p.FileMasters)
            .FirstAsync(p => p.PropertyId == property.PropertyId);

        Assert.Equal(2, retrieved.FileMasters.Count);
    }

    [Fact]
    public async Task PropertyOwnership_ManyToMany_Works()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 50m };
        var owner = new PropertyOwner
        {
            OwnerId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "Owner",
            IdentityDocumentNumber = "8001015009087"
        };
        context.Properties.Add(property);
        context.PropertyOwners.Add(owner);

        var ownership = new PropertyOwnership
        {
            Id = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            PropertyOwnerId = owner.OwnerId,
            TitleDeedNumber = "T1234/2020",
            TitleDeedDate = new DateOnly(2020, 3, 15)
        };
        context.PropertyOwnerships.Add(ownership);
        await context.SaveChangesAsync();

        var retrieved = await context.PropertyOwnerships
            .Include(po => po.Property)
            .Include(po => po.PropertyOwner)
            .FirstAsync(po => po.Id == ownership.Id);

        Assert.Equal("Test", retrieved.PropertyOwner!.FirstName);
        Assert.Equal("T1234/2020", retrieved.TitleDeedNumber);
    }

    [Fact]
    public async Task LetterIssuance_Full_Chain()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "REG-003",
            SurveyorGeneralCode = "T0LR00000000012300004",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Letter Farm",
            FarmNumber = 3,
            RegistrationDivision = "LR",
            FarmPortion = "0"
        };
        context.FileMasters.Add(fileMaster);

        var letterType = new LetterType
        {
            LetterTypeId = Guid.NewGuid(),
            LetterName = "Letter 1",
            LetterDescription = "S35(1) Notice to apply for verification",
            NWASection = "S35(1)"
        };
        context.LetterTypes.Add(letterType);

        var issuance = new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            LetterTypeId = letterType.LetterTypeId,
            GeneratedDate = new DateOnly(2026, 1, 15),
            IssuedDate = new DateOnly(2026, 1, 20),
            IssueMethod = "InPerson",
            ServingOfficialName = "Officer Nkosi",
            PhysicalDeliveryDate = new DateOnly(2026, 1, 20),
            DueDate = new DateOnly(2026, 3, 20),
            ResponseStatus = "Pending"
        };
        context.LetterIssuances.Add(issuance);
        await context.SaveChangesAsync();

        var retrieved = await context.LetterIssuances
            .Include(li => li.FileMaster)
            .Include(li => li.LetterType)
            .FirstAsync(li => li.LetterIssuanceId == issuance.LetterIssuanceId);

        Assert.Equal("Letter 1", retrieved.LetterType!.LetterName);
        Assert.Equal("InPerson", retrieved.IssueMethod);
        Assert.Equal("Officer Nkosi", retrieved.ServingOfficialName);
        Assert.Equal("REG-003", retrieved.FileMaster!.RegistrationNumber);
    }

    [Fact]
    public async Task CaseComment_Threading_Works()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "REG-004",
            SurveyorGeneralCode = "T0LR00000000012300005",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Comment Farm",
            FarmNumber = 4,
            RegistrationDivision = "LR",
            FarmPortion = "0"
        };
        context.FileMasters.Add(fileMaster);

        var parentComment = new CaseComment
        {
            CommentId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            AuthorType = "DWSOfficial",
            CommentText = "Please provide title deed.",
            SubmittedDate = DateTime.UtcNow
        };
        context.CaseComments.Add(parentComment);

        var reply = new CaseComment
        {
            CommentId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            AuthorType = "PublicUser",
            CommentText = "Title deed attached.",
            ParentCommentId = parentComment.CommentId,
            SubmittedDate = DateTime.UtcNow
        };
        context.CaseComments.Add(reply);
        await context.SaveChangesAsync();

        var parent = await context.CaseComments
            .Include(c => c.Replies)
            .FirstAsync(c => c.CommentId == parentComment.CommentId);

        Assert.Single(parent.Replies);
        Assert.Equal("Title deed attached.", parent.Replies.First().CommentText);
    }

    [Fact]
    public async Task OrganisationalUnit_SelfReferencing_Hierarchy()
    {
        using var context = TestDbContextFactory.Create();

        var national = new OrganisationalUnit
        {
            OrgUnitId = Guid.NewGuid(),
            Name = "DWS Head Office",
            Type = "National"
        };
        context.OrganisationalUnits.Add(national);

        var provincial = new OrganisationalUnit
        {
            OrgUnitId = Guid.NewGuid(),
            Name = "Limpopo Provincial Office",
            Type = "Provincial",
            ParentOrgUnitId = national.OrgUnitId
        };
        context.OrganisationalUnits.Add(provincial);
        await context.SaveChangesAsync();

        var parent = await context.OrganisationalUnits
            .Include(ou => ou.ChildOrgUnits)
            .FirstAsync(ou => ou.OrgUnitId == national.OrgUnitId);

        Assert.Single(parent.ChildOrgUnits);
        Assert.Equal("Limpopo Provincial Office", parent.ChildOrgUnits.First().Name);
    }

    [Fact]
    public async Task WorkflowInstance_Links_To_FileMaster_And_State()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "REG-005",
            SurveyorGeneralCode = "T0LR00000000012300006",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Workflow Farm",
            FarmNumber = 5,
            RegistrationDivision = "LR",
            FarmPortion = "0"
        };
        context.FileMasters.Add(fileMaster);

        var state = new WorkflowState
        {
            WorkflowStateId = Guid.NewGuid(),
            StateName = "CP1_WARMSObtained",
            Phase = "Phase1",
            DisplayOrder = 1,
            IsTerminal = false
        };
        context.WorkflowStates.Add(state);

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            CurrentWorkflowStateId = state.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        context.WorkflowInstances.Add(instance);
        await context.SaveChangesAsync();

        var retrieved = await context.WorkflowInstances
            .Include(wi => wi.FileMaster)
            .Include(wi => wi.CurrentWorkflowState)
            .FirstAsync(wi => wi.WorkflowInstanceId == instance.WorkflowInstanceId);

        Assert.Equal("REG-005", retrieved.FileMaster!.RegistrationNumber);
        Assert.Equal("CP1_WARMSObtained", retrieved.CurrentWorkflowState!.StateName);
    }

    [Fact]
    public async Task PublicUser_Property_Linking()
    {
        using var context = TestDbContextFactory.Create();

        var publicUser = new PublicUser
        {
            PublicUserId = Guid.NewGuid(),
            EmailAddress = "farmer@example.co.za",
            PasswordHash = "hashed",
            FirstName = "Thabo",
            LastName = "Mokoena",
            Status = "Active",
            RegistrationDate = DateTime.UtcNow
        };
        context.PublicUsers.Add(publicUser);

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 300m };
        context.Properties.Add(property);

        var link = new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = publicUser.PublicUserId,
            PropertyId = property.PropertyId,
            Status = "Approved",
            ApprovedDate = DateTime.UtcNow
        };
        context.PublicUserProperties.Add(link);
        await context.SaveChangesAsync();

        var user = await context.PublicUsers
            .Include(pu => pu.PublicUserProperties)
            .FirstAsync(pu => pu.PublicUserId == publicUser.PublicUserId);

        Assert.Single(user.PublicUserProperties);
        Assert.Equal("Approved", user.PublicUserProperties.First().Status);
    }

    [Fact]
    public async Task Protest_With_Documents()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "REG-006",
            SurveyorGeneralCode = "T0LR00000000012300007",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Protest Farm",
            FarmNumber = 6,
            RegistrationDivision = "LR",
            FarmPortion = "0"
        };
        context.FileMasters.Add(fileMaster);

        var publicUser = new PublicUser
        {
            PublicUserId = Guid.NewGuid(),
            EmailAddress = "protester@example.co.za",
            PasswordHash = "hashed",
            FirstName = "Sipho",
            LastName = "Dlamini",
            Status = "Active",
            RegistrationDate = DateTime.UtcNow
        };
        context.PublicUsers.Add(publicUser);

        var document = new Document
        {
            DocumentId = Guid.NewGuid(),
            DocumentType = "Upload",
            FileName = "affidavit.pdf",
            BlobPath = "/uploads/affidavit.pdf",
            UploadDate = DateTime.UtcNow
        };
        context.Documents.Add(document);

        var protest = new Protest
        {
            ProtestId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            PublicUserId = publicUser.PublicUserId,
            LodgedDate = DateTime.UtcNow,
            Status = "Lodged"
        };
        context.Protests.Add(protest);

        var protestDoc = new ProtestDocument
        {
            Id = Guid.NewGuid(),
            ProtestId = protest.ProtestId,
            DocumentId = document.DocumentId
        };
        context.ProtestDocuments.Add(protestDoc);
        await context.SaveChangesAsync();

        var retrieved = await context.Protests
            .Include(p => p.ProtestDocuments)
                .ThenInclude(pd => pd.Document)
            .FirstAsync(p => p.ProtestId == protest.ProtestId);

        Assert.Single(retrieved.ProtestDocuments);
        Assert.Equal("affidavit.pdf", retrieved.ProtestDocuments.First().Document!.FileName);
    }
}
