/****** Object:  UserDefinedFunction [dbo].[fnBitwiseDow]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Name:		<Author,,Name>
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[fnBitwiseDow]( @date DATE )
RETURNS INT
AS
BEGIN 
	RETURN CASE DATEPART(DW, @date) 
		WHEN 1 THEN 1 
		WHEN 2 THEN 2 
		WHEN 3 THEN 4 
		WHEN 4 THEN 8 
		WHEN 5 THEN 16 
		WHEN 6 THEN 32 
		WHEN 7 THEN 64 
		ELSE NULL 
	END
END
GO
/****** Object:  UserDefinedFunction [dbo].[fnWhimsyGetLlTime]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE FUNCTION [dbo].[fnWhimsyGetLlTime]
(
    @timestamp VARCHAR(256)
)
RETURNS DATETIME2
AS
BEGIN
    DECLARE @detectedat DATETIME2;
    
    -- Remove "Z" from the timestamp (if present)
    SET @timestamp = REPLACE(@timestamp, 'Z', '');

    -- Trim microseconds (SQL Server supports max 3 decimal places for DATETIME)
    IF CHARINDEX('.', @timestamp) > 0 
        SET @timestamp = LEFT(@timestamp, CHARINDEX('.', @timestamp) + 3);

    -- Convert to DATETIME2
    SET @detectedat = TRY_CONVERT(DATETIME2, @timestamp, 126);

    -- Convert to Pacific Time (automatically accounts for Daylight Savings)
    IF @detectedat IS NOT NULL
        SET @detectedat = @detectedat AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time';

    RETURN @detectedat;
END
GO
