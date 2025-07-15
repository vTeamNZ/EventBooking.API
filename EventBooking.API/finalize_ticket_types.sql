-- Finalize the TicketTypeId column

-- Make TicketTypeId NOT NULL
ALTER TABLE Seats ALTER COLUMN TicketTypeId int NOT NULL;

-- Create foreign key constraint for Seats -> TicketTypes
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_Seats_TicketTypes_TicketTypeId')
BEGIN
    ALTER TABLE Seats 
    ADD CONSTRAINT FK_Seats_TicketTypes_TicketTypeId 
    FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id);
    PRINT 'Created foreign key constraint for Seats -> TicketTypes';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint already exists';
END

-- Create index for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_TicketTypeId' AND object_id = OBJECT_ID('Seats'))
BEGIN
    CREATE INDEX IX_Seats_TicketTypeId ON Seats(TicketTypeId);
    PRINT 'Created index IX_Seats_TicketTypeId';
END
ELSE
BEGIN
    PRINT 'Index IX_Seats_TicketTypeId already exists';
END
