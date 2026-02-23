IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'AllegroImages'
)
BEGIN
    CREATE TABLE [dbo].[AllegroImages]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [Url] NVARCHAR(2048) NOT NULL,
        [Connected] BIT NOT NULL CONSTRAINT DF_AllegroImages_Connected DEFAULT (0),

        CONSTRAINT FK_AllegroImages_Products
            FOREIGN KEY ([ProductId])
            REFERENCES [dbo].[Products] ([Id])
            ON DELETE CASCADE
    );

    CREATE INDEX IX_AllegroImages_ProductId
        ON [dbo].[AllegroImages] ([ProductId]);
END
