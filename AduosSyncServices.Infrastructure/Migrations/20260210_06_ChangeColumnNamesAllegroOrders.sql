EXEC sp_rename 
    'dbo.AllegroOrderItems.GaskaItemId',
    'ProductId',
    'COLUMN';

ALTER TABLE [dbo].[AllegroOrderItems]  WITH CHECK ADD FOREIGN KEY(ProductId)
REFERENCES [dbo].[Products] ([Id])
GO