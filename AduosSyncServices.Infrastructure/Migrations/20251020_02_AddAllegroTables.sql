CREATE TABLE [dbo].[AllegroCategories](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CategoryId] [nvarchar](max) NULL,
	[Name] [nvarchar](max) NULL,
	[ParentId] [int] NULL,
 CONSTRAINT [PK_dbo.AllegroCategories] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[AllegroCategories]  WITH CHECK ADD  CONSTRAINT [FK_dbo.AllegroCategories_dbo.AllegroCategories_Parent_Id] FOREIGN KEY([ParentId])
REFERENCES [dbo].[AllegroCategories] ([Id])
GO

ALTER TABLE [dbo].[AllegroCategories] CHECK CONSTRAINT [FK_dbo.AllegroCategories_dbo.AllegroCategories_Parent_Id]
GO


CREATE TABLE [dbo].[AllegroTokenEntities](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[AccessToken] [nvarchar](max) NULL,
	[RefreshToken] [nvarchar](max) NULL,
	[ExpiryDateUtc] [datetime] NOT NULL,
	[TokenName] [nvarchar](max) NULL,
 CONSTRAINT [PK_dbo.AllegroTokenEntities] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE TABLE [dbo].[AllegroOffers](
	[Id] [nvarchar](128) NOT NULL,
	[ExternalId] [nvarchar](max) NULL,
	[Name] [nvarchar](max) NULL,
	[CategoryId] [int] NOT NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[Stock] [int] NOT NULL,
	[WatchersCount] [int] NOT NULL,
	[VisitsCount] [int] NOT NULL,
	[Status] [nvarchar](max) NULL,
	[DeliveryName] [nvarchar](max) NULL,
	[ProductId] [int] NULL,
	[StartingAt] [datetime2](7) NOT NULL,
	[ExistsInErli] [bit] NOT NULL,
	[Images] [nvarchar](max) NULL,
	[Weight] [decimal](18, 2) NOT NULL,
	[HandlingTime] [nvarchar](max) NULL,
	[ResponsibleProducer] [nvarchar](max) NULL,
	[ResponsiblePerson] [nvarchar](max) NULL,
	[Account] [varchar](100) NULL,
 CONSTRAINT [PK_dbo.AllegroOffers] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[AllegroOffers] ADD  DEFAULT ((0)) FOR [ExistsInErli]
GO

ALTER TABLE [dbo].[AllegroOffers] ADD  DEFAULT ((0)) FOR [Weight]
GO

ALTER TABLE [dbo].[AllegroOffers]  WITH CHECK ADD  CONSTRAINT [FK_dbo.AllegroOffers_dbo.Products_ProductId] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Products] ([Id])
GO

ALTER TABLE [dbo].[AllegroOffers] CHECK CONSTRAINT [FK_dbo.AllegroOffers_dbo.Products_ProductId]
GO


CREATE TABLE [dbo].[AllegroOfferAttributes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OfferId] [nvarchar](128) NULL,
	[AttributeId] [nvarchar](max) NULL,
	[Type] [nvarchar](max) NULL,
	[ValuesJson] [nvarchar](max) NULL,
	[ValuesIdsJson] [nvarchar](max) NULL,
 CONSTRAINT [PK_dbo.AllegroOfferAttributes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[AllegroOfferAttributes]  WITH CHECK ADD  CONSTRAINT [FK_dbo.AllegroOfferAttributes_dbo.AllegroOffers_OfferId] FOREIGN KEY([OfferId])
REFERENCES [dbo].[AllegroOffers] ([Id])
GO

ALTER TABLE [dbo].[AllegroOfferAttributes] CHECK CONSTRAINT [FK_dbo.AllegroOfferAttributes_dbo.AllegroOffers_OfferId]
GO

CREATE TABLE [dbo].[AllegroOfferDescriptions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OfferId] [nvarchar](128) NULL,
	[Type] [nvarchar](max) NULL,
	[Content] [nvarchar](max) NULL,
	[SectionId] [int] NOT NULL,
 CONSTRAINT [PK_dbo.AllegroOfferDescriptions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[AllegroOfferDescriptions] ADD  DEFAULT ((0)) FOR [SectionId]
GO

ALTER TABLE [dbo].[AllegroOfferDescriptions]  WITH CHECK ADD  CONSTRAINT [FK_dbo.AllegroOfferDescriptions_dbo.AllegroOffers_OfferId] FOREIGN KEY([OfferId])
REFERENCES [dbo].[AllegroOffers] ([Id])
GO

ALTER TABLE [dbo].[AllegroOfferDescriptions] CHECK CONSTRAINT [FK_dbo.AllegroOfferDescriptions_dbo.AllegroOffers_OfferId]
GO

CREATE TABLE [dbo].[CategoryParameters](
	[CategoryId] [int] NOT NULL,
	[ParameterId] [int] NOT NULL,
	[Name] [nvarchar](max) NULL,
	[Type] [nvarchar](max) NULL,
	[Required] [bit] NOT NULL,
	[Min] [int] NULL,
	[Max] [int] NULL,
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RequiredForProduct] [bit] NOT NULL,
	[DescribesProduct] [bit] NOT NULL,
	[CustomValuesEnabled] [bit] NOT NULL,
	[AmbiguousValueId] [nvarchar](max) NULL,
 CONSTRAINT [PK_dbo.CategoryParameters] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[CategoryParameters] ADD  DEFAULT ((0)) FOR [RequiredForProduct]
GO

ALTER TABLE [dbo].[CategoryParameters] ADD  DEFAULT ((0)) FOR [DescribesProduct]
GO

ALTER TABLE [dbo].[CategoryParameters] ADD  DEFAULT ((0)) FOR [CustomValuesEnabled]
GO


CREATE TABLE [dbo].[CategoryParameterValues](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CategoryParameterId] [int] NOT NULL,
	[Value] [nvarchar](max) NULL,
 CONSTRAINT [PK_dbo.CategoryParameterValues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[CategoryParameterValues]  WITH CHECK ADD  CONSTRAINT [FK_dbo.CategoryParameterValues_dbo.CategoryParameters_CategoryParameterId] FOREIGN KEY([CategoryParameterId])
REFERENCES [dbo].[CategoryParameters] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[CategoryParameterValues] CHECK CONSTRAINT [FK_dbo.CategoryParameterValues_dbo.CategoryParameters_CategoryParameterId]
GO


