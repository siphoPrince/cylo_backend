IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Profiles]') AND name = N'City')
BEGIN
    ALTER TABLE Profiles ADD City NVARCHAR(100);
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Profiles]') AND name = N'Province')
BEGIN
    ALTER TABLE Profiles ADD Province NVARCHAR(100);
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Profiles]') AND name = N'Latitude')
BEGIN
    ALTER TABLE Profiles ADD Latitude FLOAT;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Profiles]') AND name = N'Longitude')
BEGIN
    ALTER TABLE Profiles ADD Longitude FLOAT;
END