-- Export script for Event ID 4 "Ladies Night" from kwdb01 to kwdb02
-- Run this script on kwdb01 database to generate INSERT statements

USE kwdb01;

PRINT '-- =========================================';
PRINT '-- EXPORT EVENT ID 4 "LADIES NIGHT" DATA';
PRINT '-- Source: kwdb01 | Target: kwdb02';
PRINT '-- =========================================';
PRINT '';

-- 1. Export Events table
PRINT '-- 1. Events Table';
SELECT 
    'INSERT INTO kwdb02.dbo.Events (Id, Title, Description, Date, Location, Price, Capacity, OrganizerId, ImageUrl, IsActive, SeatSelectionMode, StagePosition, VenueId, ProcessingFeePercentage, ProcessingFeeFixedAmount) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ''' + 
    REPLACE(Title, '''', '''''') + ''', ''' + 
    REPLACE(ISNULL(Description, ''), '''', '''''') + ''', ''' + 
    CONVERT(NVARCHAR, Date, 120) + ''', ''' + 
    REPLACE(Location, '''', '''''') + ''', ' + 
    ISNULL(CAST(Price AS NVARCHAR), 'NULL') + ', ' + 
    ISNULL(CAST(Capacity AS NVARCHAR), 'NULL') + ', ' + 
    ISNULL(CAST(OrganizerId AS NVARCHAR), 'NULL') + ', ''' + 
    ISNULL(ImageUrl, '') + ''', ' + 
    CAST(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ' + 
    CAST(SeatSelectionMode AS NVARCHAR) + ', ''' + 
    ISNULL(StagePosition, '') + ''', ' + 
    ISNULL(CAST(VenueId AS NVARCHAR), 'NULL') + ', ' +
    ISNULL(CAST(ProcessingFeePercentage AS NVARCHAR), '0') + ', ' +
    ISNULL(CAST(ProcessingFeeFixedAmount AS NVARCHAR), '0') + ');'
FROM Events 
WHERE Id = 4;

PRINT '';

-- 2. Export Organizers table (if referenced)
PRINT '-- 2. Organizers Table';
SELECT 
    'INSERT INTO kwdb02.dbo.Organizers (Id, Name, ContactEmail, PhoneNumber, UserId, FacebookUrl, YoutubeUrl, CreatedAt, IsVerified, OrganizationName, Website) VALUES (' +
    CAST(o.Id AS NVARCHAR) + ', ''' + 
    REPLACE(o.Name, '''', '''''') + ''', ''' + 
    REPLACE(o.ContactEmail, '''', '''''') + ''', ''' + 
    REPLACE(o.PhoneNumber, '''', '''''') + ''', ''' + 
    o.UserId + ''', ''' + 
    ISNULL(o.FacebookUrl, '') + ''', ''' + 
    ISNULL(o.YoutubeUrl, '') + ''', ''' + 
    CONVERT(NVARCHAR, o.CreatedAt, 120) + ''', ' + 
    CAST(CASE WHEN o.IsVerified = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ''' + 
    ISNULL(o.OrganizationName, '') + ''', ''' + 
    ISNULL(o.Website, '') + ''');'
FROM Organizers o
INNER JOIN Events e ON e.OrganizerId = o.Id
WHERE e.Id = 4;

PRINT '';

-- 3. Export Venues table (if referenced)
PRINT '-- 3. Venues Table';
SELECT 
    'INSERT INTO kwdb02.dbo.Venues (Id, Name, Description, LayoutData, Width, Height, Address, City, HasStaggeredSeating, HasWheelchairSpaces, LayoutType, NumberOfRows, RowSpacing, SeatSpacing, SeatsPerRow, WheelchairSpaces, AisleWidth, HasHorizontalAisles, HasVerticalAisles, HorizontalAisleRows, VerticalAisleSeats, SeatSelectionMode) VALUES (' +
    CAST(v.Id AS NVARCHAR) + ', ''' + 
    REPLACE(v.Name, '''', '''''') + ''', ''' + 
    REPLACE(v.Description, '''', '''''') + ''', ''' + 
    REPLACE(v.LayoutData, '''', '''''') + ''', ' + 
    CAST(v.Width AS NVARCHAR) + ', ' + 
    CAST(v.Height AS NVARCHAR) + ', ''' + 
    REPLACE(v.Address, '''', '''''') + ''', ''' + 
    REPLACE(v.City, '''', '''''') + ''', ' + 
    CAST(CASE WHEN v.HasStaggeredSeating = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ' + 
    CAST(CASE WHEN v.HasWheelchairSpaces = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ''' + 
    v.LayoutType + ''', ' + 
    CAST(v.NumberOfRows AS NVARCHAR) + ', ' + 
    CAST(v.RowSpacing AS NVARCHAR) + ', ' + 
    CAST(v.SeatSpacing AS NVARCHAR) + ', ' + 
    CAST(v.SeatsPerRow AS NVARCHAR) + ', ' + 
    CAST(v.WheelchairSpaces AS NVARCHAR) + ', ' + 
    CAST(v.AisleWidth AS NVARCHAR) + ', ' + 
    CAST(CASE WHEN v.HasHorizontalAisles = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ' + 
    CAST(CASE WHEN v.HasVerticalAisles = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ''' + 
    v.HorizontalAisleRows + ''', ''' + 
    v.VerticalAisleSeats + ''', ' + 
    CAST(v.SeatSelectionMode AS NVARCHAR) + ');'
FROM Venues v
INNER JOIN Events e ON e.VenueId = v.Id
WHERE e.Id = 4;

PRINT '';

-- 4. Export TicketTypes table
PRINT '-- 4. TicketTypes Table';
SELECT 
    'INSERT INTO kwdb02.dbo.TicketTypes (Id, Type, Price, Description, EventId, SeatRowAssignments, Color, Name) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ''' + 
    REPLACE(Type, '''', '''''') + ''', ' + 
    CAST(Price AS NVARCHAR) + ', ''' + 
    ISNULL(REPLACE(Description, '''', ''''''), '') + ''', ' + 
    CAST(EventId AS NVARCHAR) + ', ''' + 
    ISNULL(SeatRowAssignments, '') + ''', ''' + 
    Color + ''', ''' + 
    ISNULL(Name, '') + ''');'
FROM TicketTypes 
WHERE EventId = 4;

PRINT '';

-- 5. Export FoodItems table
PRINT '-- 5. FoodItems Table';
SELECT 
    'INSERT INTO kwdb02.dbo.FoodItems (Id, Name, Price, Description, EventId) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ''' + 
    REPLACE(Name, '''', '''''') + ''', ' + 
    CAST(Price AS NVARCHAR) + ', ''' + 
    ISNULL(REPLACE(Description, '''', ''''''), '') + ''', ' + 
    CAST(EventId AS NVARCHAR) + ');'
FROM FoodItems 
WHERE EventId = 4;

PRINT '';

-- 6. Export Seats table
PRINT '-- 6. Seats Table';
SELECT 
    'INSERT INTO kwdb02.dbo.Seats (Id, EventId, Row, Number, IsReserved, Height, Price, ReservedBy, ReservedUntil, SeatNumber, Status, TableId, Width, X, Y, TicketTypeId) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ' + 
    CAST(EventId AS NVARCHAR) + ', ''' + 
    Row + ''', ' + 
    CAST(Number AS NVARCHAR) + ', ' + 
    CAST(CASE WHEN IsReserved = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ' + 
    CAST(Height AS NVARCHAR) + ', ' + 
    CAST(Price AS NVARCHAR) + ', ''' + 
    ISNULL(ReservedBy, '') + ''', ' + 
    CASE WHEN ReservedUntil IS NULL THEN 'NULL' ELSE '''' + CONVERT(NVARCHAR, ReservedUntil, 120) + '''' END + ', ''' + 
    SeatNumber + ''', ' + 
    CAST(Status AS NVARCHAR) + ', ' + 
    ISNULL(CAST(TableId AS NVARCHAR), 'NULL') + ', ' + 
    CAST(Width AS NVARCHAR) + ', ' + 
    CAST(X AS NVARCHAR) + ', ' + 
    CAST(Y AS NVARCHAR) + ', ' + 
    CAST(TicketTypeId AS NVARCHAR) + ');'
FROM Seats 
WHERE EventId = 4;

PRINT '';

-- 7. Export Tables (if any)
PRINT '-- 7. Tables Table';
SELECT 
    'INSERT INTO kwdb02.dbo.Tables (Id, EventId, TableNumber, Capacity, Height, PricePerSeat, Shape, TablePrice, Width, X, Y) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ' + 
    CAST(EventId AS NVARCHAR) + ', ''' + 
    TableNumber + ''', ' + 
    CAST(Capacity AS NVARCHAR) + ', ' + 
    CAST(Height AS NVARCHAR) + ', ' + 
    CAST(PricePerSeat AS NVARCHAR) + ', ''' + 
    Shape + ''', ' + 
    ISNULL(CAST(TablePrice AS NVARCHAR), 'NULL') + ', ' + 
    CAST(Width AS NVARCHAR) + ', ' + 
    CAST(X AS NVARCHAR) + ', ' + 
    CAST(Y AS NVARCHAR) + ');'
FROM Tables 
WHERE EventId = 4;

PRINT '';

-- 8. Show summary
PRINT '-- SUMMARY:';
SELECT 'Events' as TableName, COUNT(*) as RecordCount FROM Events WHERE Id = 4
UNION ALL
SELECT 'TicketTypes', COUNT(*) FROM TicketTypes WHERE EventId = 4
UNION ALL
SELECT 'FoodItems', COUNT(*) FROM FoodItems WHERE EventId = 4
UNION ALL
SELECT 'Seats', COUNT(*) FROM Seats WHERE EventId = 4
UNION ALL
SELECT 'Tables', COUNT(*) FROM Tables WHERE EventId = 4
UNION ALL
SELECT 'Organizers', COUNT(*) FROM Organizers o INNER JOIN Events e ON e.OrganizerId = o.Id WHERE e.Id = 4
UNION ALL
SELECT 'Venues', COUNT(*) FROM Venues v INNER JOIN Events e ON e.VenueId = v.Id WHERE e.Id = 4;

PRINT '';
PRINT '-- Run the generated INSERT statements above on kwdb02 database';
