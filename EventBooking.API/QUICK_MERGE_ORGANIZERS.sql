-- Quick MERGE for Organizers
MERGE Organizers AS target
USING (VALUES 
    (4, 'Canta Lankans Women''s Club', 'canta.womens@gmail.com', '0272345678', 'b03ccbd1d-5b96-4908-ae93-674f092694c6', '', '', '2024-12-30 11:20:30', 1, 'Canta Lankans Women''s Club', ''),
    (6, 'Ruchi Events', 'ruchi@gmail.com', '0211234567', '6af775c4d-adab-45ae-ad20-a9018952fdc4', '', '', '2024-12-30 11:21:15', 1, 'Ruchi Events', '')
) AS source (Id, Name, ContactEmail, PhoneNumber, UserId, FacebookUrl, YoutubeUrl, CreatedAt, IsVerified, OrganizationName, Website)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET 
        Name = source.Name,
        ContactEmail = source.ContactEmail,
        PhoneNumber = source.PhoneNumber,
        UserId = source.UserId,
        FacebookUrl = source.FacebookUrl,
        YoutubeUrl = source.YoutubeUrl,
        CreatedAt = source.CreatedAt,
        IsVerified = source.IsVerified,
        OrganizationName = source.OrganizationName,
        Website = source.Website
WHEN NOT MATCHED THEN
    INSERT (Id, Name, ContactEmail, PhoneNumber, UserId, FacebookUrl, YoutubeUrl, CreatedAt, IsVerified, OrganizationName, Website)
    VALUES (source.Id, source.Name, source.ContactEmail, source.PhoneNumber, source.UserId, source.FacebookUrl, source.YoutubeUrl, source.CreatedAt, source.IsVerified, source.OrganizationName, source.Website);
