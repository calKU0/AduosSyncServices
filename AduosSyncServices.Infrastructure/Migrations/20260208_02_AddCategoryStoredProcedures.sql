CREATE OR ALTER PROCEDURE dbo.AllegroCategories_Upsert
    @CategoryId NVARCHAR(255),
    @Name NVARCHAR(255),
    @ParentId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id INT;

    UPDATE AllegroCategories
    SET Name = @Name,
        ParentId = @ParentId
    WHERE CategoryId = @CategoryId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO AllegroCategories (CategoryId, Name, ParentId)
        VALUES (@CategoryId, @Name, @ParentId);

        SET @Id = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        SELECT @Id = Id
        FROM AllegroCategories
        WHERE CategoryId = @CategoryId;
    END

    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.CategoryParameters_GetByCategoryId
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        cp.*,
        cpv.Id AS ValueId, cpv.Value, cpv.CategoryParameterId
    FROM CategoryParameters cp
    LEFT JOIN CategoryParameterValues cpv ON cp.Id = cpv.CategoryParameterId
    WHERE cp.CategoryId = @CategoryId AND DescribesProduct = 0;
END
GO

CREATE OR ALTER PROCEDURE dbo.CategoryParameters_UpsertWithValues
    @CategoryId INT,
    @ParameterId INT,
    @Name NVARCHAR(255),
    @Type NVARCHAR(100),
    @Required BIT,
    @Min INT = NULL,
    @Max INT = NULL,
    @RequiredForProduct BIT,
    @DescribesProduct BIT,
    @CustomValuesEnabled BIT,
    @AmbiguousValueId NVARCHAR(100) = NULL,
    @ValuesJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id INT;

    SELECT @Id = Id
    FROM CategoryParameters
    WHERE CategoryId = @CategoryId AND ParameterId = @ParameterId;

    IF @Id IS NULL
    BEGIN
        INSERT INTO CategoryParameters
            (CategoryId, ParameterId, Name, Type, Required, Min, Max, RequiredForProduct, DescribesProduct, CustomValuesEnabled, AmbiguousValueId)
        VALUES
            (@CategoryId, @ParameterId, @Name, @Type, @Required, @Min, @Max, @RequiredForProduct, @DescribesProduct, @CustomValuesEnabled, @AmbiguousValueId);

        SET @Id = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE CategoryParameters
        SET Name = @Name,
            Type = @Type,
            Required = @Required,
            Min = @Min,
            Max = @Max,
            RequiredForProduct = @RequiredForProduct,
            DescribesProduct = @DescribesProduct,
            CustomValuesEnabled = @CustomValuesEnabled,
            AmbiguousValueId = @AmbiguousValueId
        WHERE Id = @Id;
    END

    DELETE FROM CategoryParameterValues
    WHERE CategoryParameterId = @Id;

    IF @ValuesJson IS NOT NULL
    BEGIN
        INSERT INTO CategoryParameterValues (CategoryParameterId, Value)
        SELECT @Id, value
        FROM OPENJSON(@ValuesJson)
        WHERE value IS NOT NULL AND LTRIM(RTRIM(value)) <> '';
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_GetDefaultAllegroCategoriesWithoutParameters
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT p.DefaultAllegroCategory
    FROM Products p
    LEFT JOIN CategoryParameters cp ON cp.CategoryId = p.DefaultAllegroCategory
    WHERE p.DefaultAllegroCategory != 0 AND cp.CategoryId IS NULL;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroCategories_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM AllegroCategories;
END
GO
