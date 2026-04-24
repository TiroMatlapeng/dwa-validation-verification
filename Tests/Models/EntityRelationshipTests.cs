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
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Validator",
            EmployeeNumber = "EMP001",
            OrgUnitId = orgUnit.OrgUnitId
        };
        var capturer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Capturer",
            EmployeeNumber = "EMP002",
            OrgUnitId = orgUnit.OrgUnitId
        };
        context.Users.AddRange(validator, capturer);

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
            ValidatorId = validator.Id,
            CapturePersonId = capturer.Id,
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
            Phase = "Inception",
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
    public async Task Objection_With_Documents()
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
            FarmName = "Objection Farm",
            FarmNumber = 6,
            RegistrationDivision = "LR",
            FarmPortion = "0"
        };
        context.FileMasters.Add(fileMaster);

        var publicUser = new PublicUser
        {
            PublicUserId = Guid.NewGuid(),
            EmailAddress = "objector@example.co.za",
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

        var objection = new Objection
        {
            ObjectionId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            PublicUserId = publicUser.PublicUserId,
            LodgedDate = DateTime.UtcNow,
            Status = "Lodged"
        };
        context.Objections.Add(objection);

        var objectionDoc = new ObjectionDocument
        {
            Id = Guid.NewGuid(),
            ObjectionId = objection.ObjectionId,
            DocumentId = document.DocumentId
        };
        context.ObjectionDocuments.Add(objectionDoc);
        await context.SaveChangesAsync();

        var retrieved = await context.Objections
            .Include(o => o.ObjectionDocuments)
                .ThenInclude(od => od.Document)
            .FirstAsync(o => o.ObjectionId == objection.ObjectionId);

        Assert.Single(retrieved.ObjectionDocuments);
        Assert.Equal("affidavit.pdf", retrieved.ObjectionDocuments.First().Document!.FileName);
    }

    [Fact]
    public async Task CatchmentArea_Links_To_WMA_And_Properties()
    {
        using var context = TestDbContextFactory.Create();

        var province = new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Limpopo", ProvinceCode = "LP" };
        context.Provinces.Add(province);

        var wma = new WaterManagementArea { WmaId = Guid.NewGuid(), WmaName = "Limpopo", WmaCode = "1", ProvinceId = province.ProvinceId };
        context.WaterManagementAreas.Add(wma);

        var catchment = new CatchmentArea
        {
            CatchmentAreaId = Guid.NewGuid(),
            CatchmentCode = "A21A",
            CatchmentName = "Mogalakwena Upper",
            WmaId = wma.WmaId
        };
        context.CatchmentAreas.Add(catchment);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertySize = 250m,
            CatchmentAreaId = catchment.CatchmentAreaId,
            WmaId = wma.WmaId
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var retrieved = await context.CatchmentAreas
            .Include(c => c.WaterManagementArea)
            .Include(c => c.Properties)
            .FirstAsync(c => c.CatchmentAreaId == catchment.CatchmentAreaId);

        Assert.Equal("Limpopo", retrieved.WaterManagementArea!.WmaName);
        Assert.Single(retrieved.Properties);
        Assert.Equal(property.PropertyId, retrieved.Properties.First().PropertyId);
    }

    [Fact]
    public async Task GwcaProclamationRule_Links_To_GWCA()
    {
        using var context = TestDbContextFactory.Create();

        var gwca = new GovernmentWaterControlArea
        {
            WaterControlAreaId = Guid.NewGuid(),
            GovernmentWaterControlAreaName = "Blyde River GWCA",
            GovernmentGazetteReference = "Proclamation 67 of 1935",
            ProclamationDate = new DateOnly(1935, 1, 1)
        };
        context.GovernmentWaterControlAreas.Add(gwca);

        var rule = new GwcaProclamationRule
        {
            RuleId = Guid.NewGuid(),
            WaterControlAreaId = gwca.WaterControlAreaId,
            RuleCode = "MAX_HECTARES",
            RuleDescription = "Maximum irrigable hectares per property",
            NumericLimit = 30,
            Unit = "ha",
            GovernmentGazetteReference = "GN 180 of 10 July 1970",
            IsActive = true,
            EffectiveFrom = new DateOnly(1970, 7, 10)
        };
        context.GwcaProclamationRules.Add(rule);
        await context.SaveChangesAsync();

        var retrieved = await context.GovernmentWaterControlAreas
            .Include(g => g.ProclamationRules)
            .FirstAsync(g => g.WaterControlAreaId == gwca.WaterControlAreaId);

        Assert.Single(retrieved.ProclamationRules);
        Assert.Equal("MAX_HECTARES", retrieved.ProclamationRules.First().RuleCode);
        Assert.Equal(30m, retrieved.ProclamationRules.First().NumericLimit);
    }

    [Fact]
    public async Task Mapbook_Links_To_FileMaster_And_SateliteImages()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "REG-MAP-001",
            SurveyorGeneralCode = "T0LR00000000012300010",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Mapbook Farm",
            FarmNumber = 10,
            RegistrationDivision = "LR",
            FarmPortion = "0"
        };
        context.FileMasters.Add(fileMaster);

        var satImage = new SateliteImage
        {
            ImageId = Guid.NewGuid(),
            ImageName = "Landsat_1997_Winter",
            PropertyId = property.PropertyId,
            ImageDate = new DateOnly(1997, 6, 15),
            ImageSource = "USGS Earth Explorer"
        };
        context.SateliteImages.Add(satImage);

        var mapbook = new Mapbook
        {
            MapbookId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            MapbookTitle = "Qualifying Period Mapbook - Farm 10",
            MapType = "Qualifying",
            ProcessedDate = new DateOnly(2026, 3, 1)
        };
        context.Mapbooks.Add(mapbook);

        var mapbookImage = new MapbookImage
        {
            MapbookImageId = Guid.NewGuid(),
            MapbookId = mapbook.MapbookId,
            SateliteImageId = satImage.ImageId,
            LayerOrder = 1,
            Notes = "Winter qualifying period image"
        };
        context.MapbookImages.Add(mapbookImage);
        await context.SaveChangesAsync();

        var retrieved = await context.Mapbooks
            .Include(m => m.FileMaster)
            .Include(m => m.MapbookImages)
                .ThenInclude(mi => mi.SateliteImage)
            .FirstAsync(m => m.MapbookId == mapbook.MapbookId);

        Assert.Equal("REG-MAP-001", retrieved.FileMaster!.RegistrationNumber);
        Assert.Equal("Qualifying", retrieved.MapType);
        Assert.Single(retrieved.MapbookImages);
        Assert.Equal("Landsat_1997_Winter", retrieved.MapbookImages.First().SateliteImage!.ImageName);
    }

    [Fact]
    public async Task FileMaster_AssessmentTrack_And_CatchmentArea()
    {
        using var context = TestDbContextFactory.Create();

        var province = new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Mpumalanga", ProvinceCode = "MP" };
        context.Provinces.Add(province);

        var wma = new WaterManagementArea { WmaId = Guid.NewGuid(), WmaName = "Inkomati-Usuthu", WmaCode = "3", ProvinceId = province.ProvinceId };
        context.WaterManagementAreas.Add(wma);

        var catchment = new CatchmentArea
        {
            CatchmentAreaId = Guid.NewGuid(),
            CatchmentCode = "X11A",
            CatchmentName = "Komati Upper",
            WmaId = wma.WmaId
        };
        context.CatchmentAreas.Add(catchment);

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 500m, CatchmentAreaId = catchment.CatchmentAreaId };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "REG-TRACK-001",
            SurveyorGeneralCode = "T0LR00000000012300011",
            PrimaryCatchment = "X",
            QuaternaryCatchment = "X11A",
            FarmName = "Assessment Farm",
            FarmNumber = 11,
            RegistrationDivision = "LR",
            FarmPortion = "0",
            AssessmentTrack = "S33_2_Declaration",
            CatchmentAreaId = catchment.CatchmentAreaId
        };
        context.FileMasters.Add(fileMaster);
        await context.SaveChangesAsync();

        var retrieved = await context.FileMasters
            .Include(fm => fm.CatchmentArea)
            .FirstAsync(fm => fm.FileMasterId == fileMaster.FileMasterId);

        Assert.Equal("S33_2_Declaration", retrieved.AssessmentTrack);
        Assert.Equal("X11A", retrieved.CatchmentArea!.CatchmentCode);
    }

    [Fact]
    public async Task LetterIssuance_S33_2_With_IrrigationBoard()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m };
        context.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "REG-S33-001",
            SurveyorGeneralCode = "T0LR00000000012300012",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Declaration Farm",
            FarmNumber = 12,
            RegistrationDivision = "LR",
            FarmPortion = "0",
            AssessmentTrack = "S33_2_Declaration"
        };
        context.FileMasters.Add(fileMaster);

        var irrigationBoard = new IrrigationBoard
        {
            IrrigationBoardId = Guid.NewGuid(),
            IrrigationBoardName = "Loskop Irrigation Board"
        };
        context.IrrigationBoards.Add(irrigationBoard);

        var letterType = new LetterType
        {
            LetterTypeId = Guid.NewGuid(),
            LetterName = "S33(2) Declaration",
            LetterDescription = "Kader Asmal Declaration — confirms ELU for scheduled area",
            NWASection = "S33(2)"
        };
        context.LetterTypes.Add(letterType);

        var issuance = new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = fileMaster.FileMasterId,
            LetterTypeId = letterType.LetterTypeId,
            IrrigationBoardId = irrigationBoard.IrrigationBoardId,
            IncludesDormantVolume = true,
            GeneratedDate = new DateOnly(2026, 3, 15),
            IssuedDate = new DateOnly(2026, 3, 20),
            IssueMethod = "RegisteredPost"
        };
        context.LetterIssuances.Add(issuance);
        await context.SaveChangesAsync();

        var retrieved = await context.LetterIssuances
            .Include(li => li.LetterType)
            .Include(li => li.IrrigationBoard)
            .FirstAsync(li => li.LetterIssuanceId == issuance.LetterIssuanceId);

        Assert.Equal("S33(2)", retrieved.LetterType!.NWASection);
        Assert.Equal("Loskop Irrigation Board", retrieved.IrrigationBoard!.IrrigationBoardName);
        Assert.True(retrieved.IncludesDormantVolume);
    }
}
