-- Logo nie jest już wysyłane do Allegro (ani w galerii, ani jako przerywnik w opisie).
-- Usuwamy jego zapisany adres przy produktach - przy kolejnej aktualizacji oferty dostaną galerię i opis bez logo.
-- Allegro zwraca ten sam adres dla tego samego pliku, więc logo ma jeden adres przy wszystkich produktach
-- (zawsze jako ostatnie zdjęcie).
DELETE FROM dbo.AllegroImages
WHERE Url = N'https://a.allegroimg.com/original/1118fa/9b2817b547298a5f11c939ca4a02';
