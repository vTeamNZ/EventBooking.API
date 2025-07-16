-- Simple script to add Status column
ALTER TABLE [Events] ADD [Status] int NOT NULL DEFAULT 0;
