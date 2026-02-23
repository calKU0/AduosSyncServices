EXEC sp_rename 
    'dbo.AllegroOrders.SentToGaska',
    'SentToExternalCompany',
    'COLUMN';

EXEC sp_rename 
    'dbo.AllegroOrders.GaskaOrderId',
    'ExternalOrderId',
    'COLUMN';

EXEC sp_rename 
    'dbo.AllegroOrders.GaskaOrderStatus',
    'ExternalOrderStatus',
    'COLUMN';

EXEC sp_rename 
    'dbo.AllegroOrders.GaskaOrderNumber',
    'ExternalOrderNumber',
    'COLUMN';
    
EXEC sp_rename 
    'dbo.AllegroOrders.GaskaDeliveryName',
    'ExternalDeliveryName',
    'COLUMN';

EXEC sp_rename 
    'dbo.AllegroOrderItems.GaskaTrackingNumber',
    'ExternalTrackingNumber',
    'COLUMN';

EXEC sp_rename 
    'dbo.AllegroOrderItems.GaskaCourier',
    'ExternalCourier',
    'COLUMN';