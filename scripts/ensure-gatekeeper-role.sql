-- Ensures GateKeeper role exists (also created automatically on app startup via DatabaseSeeder).
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = 'GATEKEEPER')
BEGIN
    INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (CONVERT(nvarchar(36), NEWID()), 'GateKeeper', 'GATEKEEPER', CONVERT(nvarchar(36), NEWID()));
END
