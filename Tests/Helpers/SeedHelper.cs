namespace dwa_ver_val.Tests.Helpers;

public static class SeedHelper
{
    public static FileMaster NewFileMaster(Guid propertyId, string farmName = "Test Farm")
    {
        return new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = propertyId,
            RegistrationNumber = "WARMS-0001",
            SurveyorGeneralCode = "T0001",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = farmName,
            FarmNumber = 1,
            RegistrationDivision = "JR",
            FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }
}
