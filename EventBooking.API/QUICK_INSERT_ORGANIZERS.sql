-- Quick Insert for Organizers
SET IDENTITY_INSERT Organizers ON;

INSERT INTO Organizers (Id, Name, ContactEmail, PhoneNumber, UserId, FacebookUrl, YoutubeUrl, CreatedAt, IsVerified, OrganizationName, Website) VALUES 
(4, 'Canta Lankans Women''s Club', 'canta.womens@gmail.com', '0272345678', 'b03ccbd1d-5b96-4908-ae93-674f092694c6', '', '', '2024-12-30 11:20:30', 1, 'Canta Lankans Women''s Club', ''),
(6, 'Ruchi Events', 'ruchi@gmail.com', '0211234567', '6af775c4d-adab-45ae-ad20-a9018952fdc4', '', '', '2024-12-30 11:21:15', 1, 'Ruchi Events', '');

SET IDENTITY_INSERT Organizers OFF;
