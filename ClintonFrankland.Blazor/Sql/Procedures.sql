/****** Object:  StoredProcedure [dbo].[spcfDeleteAccount]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfDeleteAccount]
	@accountid INT
AS
BEGIN
	SET NOCOUNT ON;
	DELETE FROM dbo.cfAccounts
	WHERE AccountId = @accountid
END
GO
/****** Object:  StoredProcedure [dbo].[spcfDeleteBudget]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfDeleteBudget] 
	-- Add the parameters for the stored procedure here
	@BudgetId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DELETE FROM dbo.cfBudgets
	WHERE BudgetId = @BudgetId

END
GO
/****** Object:  StoredProcedure [dbo].[spcfDeleteChore]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfDeleteChore]
	@choreid INT
AS
BEGIN
	SET NOCOUNT ON;
	UPDATE dbo.cfChores
	SET IsDeleted = 1
	WHERE ChoreId = @choreid
END
GO
/****** Object:  StoredProcedure [dbo].[spcfEmailRulesGetEmailRules]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfEmailRulesGetEmailRules]
	
AS
BEGIN
	SET NOCOUNT ON;
	SELECT
		[EmailAddress]
		,[Retention]
		,[RuleId]
	FROM [dbo].[cfEmailRules]
	WHERE [IsDeleted] = 0
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetAccount]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetAccount]
	@accountid INT
AS
BEGIN
	SET NOCOUNT ON;
	SELECT
		AccountId
		,AccountName
		,ISNULL(AccountNumber,'') AS [AccountNumber]
		,ISNULL(Balance,0.00) AS [Balance]
		,ISNULL(AvailableCredit,0.00) AS [AvailableCredit]
		,ISNULL(DueDate,1) AS [DueDate]
		,ISNULL(MinimumPayment,0.00) AS [MinimumPayment]
		,ISNULL(InterestRate,0.00) AS [InterestRate]
		,ISNULL(CreditLimit,0.00) AS [CreditLimit]
		,ISNULL(WebURL,'') AS [WebURL]
		,acc.AccountTypeID AS [AccountType]
		,typ.AccountTypeName AS [AccountTypeName]
		,UserId
		,ISNULL(acc.ClearedBalance, 0.00) AS [ClearedBalance]
	FROM
		dbo.cfAccounts AS [acc]
		LEFT OUTER JOIN dbo.cfAccountTypes AS [typ] ON acc.AccountTypeID = typ.AccountTypeId
	WHERE AccountId = @accountid
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetAccounts]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetAccounts] (
    @userid INT
	,@accounttype INT = -1
	,@sorttype INT = 0
	,@sortdirection INT = 0
) AS
BEGIN
    SET NOCOUNT ON
	SELECT
		AccountId
		,AccountName
		,ISNULL(AccountNumber,'') AS [AccountNumber]
		,ISNULL(Balance,0.00) AS [Balance]
		,ISNULL(AvailableCredit,0.00) AS [AvailableCredit]
		,ISNULL(DueDate,1) AS [DueDate]
		,ISNULL(MinimumPayment,0.00) AS [MinimumPayment]
		,ISNULL(InterestRate,0.00) AS [InterestRate]
		,ISNULL(CreditLimit,0.00) AS [CreditLimit]
		,ISNULL(WebURL,'') AS [WebURL]
		,acc.AccountTypeID
		,typ.AccountTypeName AS [AccountType]
		,UserId
		,CONVERT(VARCHAR,ISNULL([LastUpdated],'1/1/1800'),23) AS [LastUpdated]
		,ROUND(IIF(Balance = 0, 0, MinimumPayment / Balance)*100,2) AS [Ratio]
	FROM
		dbo.cfAccounts AS [acc]
		LEFT OUTER JOIN dbo.cfAccountTypes AS [typ] ON acc.AccountTypeID = typ.AccountTypeId
	WHERE
		(
			acc.AccountTypeID = @accounttype
			OR @accounttype = -1
		)
		AND (userid = @userid)
		AND (ISNULL(IsDeleted,0) = 0)
	ORDER BY 
		CASE WHEN @sorttype = 0 AND @sortdirection = 0 THEN AccountName END
		,CASE WHEN @sorttype = 0 AND @sortdirection = 1 THEN AccountName END DESC
		,CASE WHEN @sorttype = 1 AND @sortdirection = 0 THEN AccountTypeName END
		,CASE WHEN @sorttype = 1 AND @sortdirection = 1 THEN AccountTypeName END DESC
		,CASE WHEN @sorttype = 2 AND @sortdirection = 0 THEN AccountNumber END
		,CASE WHEN @sorttype = 2 AND @sortdirection = 1 THEN AccountNumber END DESC
		,CASE WHEN @sorttype = 3 AND @sortdirection = 0 THEN InterestRate END
		,CASE WHEN @sorttype = 3 AND @sortdirection = 1 THEN InterestRate END DESC
		,CASE WHEN @sorttype = 4 AND @sortdirection = 0 THEN MinimumPayment END
		,CASE WHEN @sorttype = 4 AND @sortdirection = 1 THEN MinimumPayment END DESC
		,CASE WHEN @sorttype = 5 AND @sortdirection = 0 THEN Balance END
		,CASE WHEN @sorttype = 5 AND @sortdirection = 1 THEN Balance END DESC
		,CASE WHEN @sorttype = 6 AND @sortdirection = 0 THEN IIF(Balance = 0, 0, MinimumPayment / Balance) END
		,CASE WHEN @sorttype = 6 AND @sortdirection = 1 THEN IIF(Balance = 0, 0, MinimumPayment / Balance) END DESC
		,CASE WHEN @sorttype = 7 AND @sortdirection = 0 THEN LastUpdated END
		,CASE WHEN @sorttype = 7 AND @sortdirection = 1 THEN LastUpdated END DESC

END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetBalance]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetBalance] 
AS
BEGIN
	SET NOCOUNT ON;
	SELECT TOP 1 Balance
	FROM dbo.cfAccounts
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetBudget]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfGetBudget] 
	-- Add the parameters for the stored procedure here
	@BudgetId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		BudgetId
		,BudgetName
		,BudgetTypeID
		,bu.CategoryId
		,EndDate
		,FrequencyID
		,NextDueDate
		,Amount
		,ISNULL(IsAutomatic,0) AS [IsAuto]
		,ISNULL(IsBill,0) AS [IsBill]
		,ISNULL(IsLate,0) AS [IsLate]
		,ISNULL(bu.PayeeId,-1) AS [PayeeId]
		,pa.PayeeName AS [Payee]
		,ca.CategoryName as [Category]
	FROM dbo.cfBudgets AS [bu]
	LEFT OUTER JOIN dbo.cfPayees as [pa] ON bu.PayeeId = pa.PayeeId
	LEFT OUTER JOIN dbo.cfCategories AS [ca] ON bu.CategoryId = ca.CategoryId
	WHERE BudgetId = @BudgetId
	

END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetBudgetItems_020000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetBudgetItems_020000] 
	-- Add the parameters for the stored procedure here
	@UserId INT,
	@showTotals INT = 1
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT 
		BudgetId
		,BudgetName
		,BudgetName AS [BudgetNameSort]
		,BudgetTypeID
		,cat.CategoryName AS [Category]
		,budget.FrequencyID
		,fqcy.FrequencyName
		,FORMAT([NextDueDate], 'yyyy-MM-dd') AS [DueDate]
		,FORMAT([NextDueDate], 'yyyyMMdd') AS [DueDateSort]
		,[EndDate]
		,CASE WHEN EndDate = '1/1/1970' THEN '' ELSE CONVERT(VARCHAR,EndDate,101) END AS [EndDateName]
		,Amount
		,CASE
			WHEN BudgetTypeID = 0 THEN 'true'
			ELSE 'false'
		END AS [IsBold]
		,CONVERT(DECIMAL(18,2),
			ROUND(
				CASE 
					WHEN [budget].[FrequencyId] = 1 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 12
					WHEN [budget].[FrequencyId] = 2 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 26 / 12
					WHEN [budget].[FrequencyId] = 4 AND [budget].[BudgetTypeID] = 0 THEN [Amount]
					WHEN [budget].[FrequencyId] = 5 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 2
					WHEN [budget].[FrequencyId] = 6 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 3
					WHEN [budget].[FrequencyId] = 7 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 5 / 12
					WHEN [budget].[FrequencyId] = 8 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 2
					WHEN [budget].[FrequencyId] = 9 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 12
					WHEN [budget].[FrequencyId] = 10 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 73 / 12
					WHEN [budget].[FrequencyId] = 11 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 6 / 12
					WHEN [budget].[FrequencyId] = 12 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 3 / 12
					WHEN [budget].[FrequencyId] = 13 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 4 / 12
					WHEN [budget].[FrequencyId] = 14 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 6

					WHEN [budget].[FrequencyId] = 1 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 52 / 12
					WHEN [budget].[FrequencyId] = 2 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 26 / 12
					WHEN [budget].[FrequencyId] = 4 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]
					WHEN [budget].[FrequencyId] = 5 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 2
					WHEN [budget].[FrequencyId] = 6 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 3
					WHEN [budget].[FrequencyId] = 7 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 52 / 5 / 12
					WHEN [budget].[FrequencyId] = 8 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 2
					WHEN [budget].[FrequencyId] = 9 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 12
					WHEN [budget].[FrequencyId] = 10 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 73 / 12
					WHEN [budget].[FrequencyId] = 11 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 6 / 12
					WHEN [budget].[FrequencyId] = 12 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 3 / 12
					WHEN [budget].[FrequencyId] = 13 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 4 / 12
					WHEN [budget].[FrequencyId] = 14 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 6
					ELSE 0
				END
			,2)
		) AS [Monthly]
		,1 AS [sort]
		,ISNULL(IsBill,0) AS IsBill
		,ISNULL(IsAutomatic,0) AS IsAuto
		,ISNULL(IsLate,0) AS IsLate
	FROM 
		[dbo].[cfBudgets] AS [budget]
		LEFT OUTER JOIN cfCategories AS [cat] ON budget.CategoryID = cat.categoryid
		LEFT OUTER JOIN dbo.cfFrequencies AS fqcy ON budget.FrequencyId = fqcy.FrequencyId
	WHERE
		budget.UserId = @UserId
		--AND budget.FrequencyID != 0
UNION ALL
	SELECT 
		-1
		,'TOTAL'
		,'~' AS [BudgetNameSort]
		,-1
		,'~' AS [Category]
		,-1
		,''
		,'' AS [DueDate]
		,'zzz' AS [DueDateSort]
		,'' AS [EndDate]
		,'' AS [EndDateName]
		,0.0 AS Amount
		,'true' AS [IsBold]
		,CONVERT(DECIMAL(18,2),
			SUM(
			ROUND(
				CASE 
					WHEN [budget].[FrequencyId] = 1 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 12
					WHEN [budget].[FrequencyId] = 2 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 26 / 12
					WHEN [budget].[FrequencyId] = 4 AND [budget].[BudgetTypeID] = 0 THEN [Amount]
					WHEN [budget].[FrequencyId] = 5 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 2
					WHEN [budget].[FrequencyId] = 6 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 3
					WHEN [budget].[FrequencyId] = 7 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 5 / 12
					WHEN [budget].[FrequencyId] = 8 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 2
					WHEN [budget].[FrequencyId] = 9 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 12
					WHEN [budget].[FrequencyId] = 10 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 73 / 12
					WHEN [budget].[FrequencyId] = 11 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 6 / 12
					WHEN [budget].[FrequencyId] = 12 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 3 / 12
					WHEN [budget].[FrequencyId] = 13 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 4 / 12
					WHEN [budget].[FrequencyId] = 14 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 6

					WHEN [budget].[FrequencyId] = 1 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 52 / 12
					WHEN [budget].[FrequencyId] = 2 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 26 / 12
					WHEN [budget].[FrequencyId] = 4 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]
					WHEN [budget].[FrequencyId] = 5 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 2
					WHEN [budget].[FrequencyId] = 6 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 3
					WHEN [budget].[FrequencyId] = 7 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 52 / 5 / 12
					WHEN [budget].[FrequencyId] = 8 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 2
					WHEN [budget].[FrequencyId] = 9 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 12
					WHEN [budget].[FrequencyId] = 10 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 73 / 12
					WHEN [budget].[FrequencyId] = 11 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 6 / 12
					WHEN [budget].[FrequencyId] = 12 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 3 / 12
					WHEN [budget].[FrequencyId] = 13 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 4 / 12
					WHEN [budget].[FrequencyId] = 14 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 6
					ELSE 0
				END
			,2)
			)
		) AS [Monthly]
		,2 AS [sort]
		,0 AS IsBill
		,0 AS IsAuto
		,0 AS IsLate
	FROM 
		[dbo].[cfBudgets] AS [budget]
		LEFT OUTER JOIN cfCategories AS [cat] ON budget.CategoryID = cat.categoryid
		LEFT OUTER JOIN dbo.cfFrequencies AS fqcy ON budget.FrequencyId = fqcy.FrequencyId
	WHERE
		budget.UserId = @UserId
		AND @showTotals = 1
	ORDER BY
		sort
		,BudgetName
      
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetBudgetItems_030000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE   PROCEDURE [dbo].[spcfGetBudgetItems_030000] 
	-- Add the parameters for the stored procedure here
	@UserId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT 
		BudgetId
		,BudgetName
		,BudgetName AS [BudgetNameSort]
		,BudgetTypeID
		,cat.CategoryName AS [Category]
		,budget.FrequencyID
		,fqcy.FrequencyName
		,FORMAT([NextDueDate], 'yyyy-MM-dd') AS [DueDate]
		,FORMAT([NextDueDate], 'yyyyMMdd') AS [DueDateSort]
		,[EndDate]
		,CASE WHEN EndDate = '1/1/1970' THEN '' ELSE CONVERT(VARCHAR,EndDate,101) END AS [EndDateName]
		,Amount
		,CASE
			WHEN BudgetTypeID = 0 THEN 'true'
			ELSE 'false'
		END AS [IsBold]
		,CONVERT(DECIMAL(18,2),
			ROUND(
				CASE 
					WHEN [budget].[FrequencyId] = 1 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 12
					WHEN [budget].[FrequencyId] = 2 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 26 / 12
					WHEN [budget].[FrequencyId] = 4 AND [budget].[BudgetTypeID] = 0 THEN [Amount]
					WHEN [budget].[FrequencyId] = 5 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 2
					WHEN [budget].[FrequencyId] = 6 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 3
					WHEN [budget].[FrequencyId] = 7 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 5 / 12
					WHEN [budget].[FrequencyId] = 8 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 2
					WHEN [budget].[FrequencyId] = 9 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 12
					WHEN [budget].[FrequencyId] = 10 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 73 / 12
					WHEN [budget].[FrequencyId] = 11 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 6 / 12
					WHEN [budget].[FrequencyId] = 12 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 3 / 12
					WHEN [budget].[FrequencyId] = 13 AND [budget].[BudgetTypeID] = 0 THEN [Amount] * 52 / 4 / 12
					WHEN [budget].[FrequencyId] = 14 AND [budget].[BudgetTypeID] = 0 THEN [Amount] / 6

					WHEN [budget].[FrequencyId] = 1 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 52 / 12
					WHEN [budget].[FrequencyId] = 2 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 26 / 12
					WHEN [budget].[FrequencyId] = 4 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]
					WHEN [budget].[FrequencyId] = 5 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 2
					WHEN [budget].[FrequencyId] = 6 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 3
					WHEN [budget].[FrequencyId] = 7 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 52 / 5 / 12
					WHEN [budget].[FrequencyId] = 8 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 2
					WHEN [budget].[FrequencyId] = 9 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 12
					WHEN [budget].[FrequencyId] = 10 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] * 73 / 12
					WHEN [budget].[FrequencyId] = 11 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 6 / 12
					WHEN [budget].[FrequencyId] = 12 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 3 / 12
					WHEN [budget].[FrequencyId] = 13 AND [budget].[BudgetTypeID] = 1 THEN -[Amount]* 52 / 4 / 12
					WHEN [budget].[FrequencyId] = 14 AND [budget].[BudgetTypeID] = 1 THEN -[Amount] / 6
					ELSE 0
				END
			,2)
		) AS [Monthly]
		,1 AS [sort]
		,ISNULL(IsBill,0) AS IsBill
		,ISNULL(IsAutomatic,0) AS IsAuto
		,ISNULL(IsLate,0) AS IsLate
	FROM 
		[dbo].[cfBudgets] AS [budget]
		LEFT OUTER JOIN cfCategories AS [cat] ON budget.CategoryID = cat.categoryid
		LEFT OUTER JOIN dbo.cfFrequencies AS fqcy ON budget.FrequencyId = fqcy.FrequencyId
	WHERE
		budget.UserId = @UserId
		--AND budget.FrequencyID != 0
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetCategories_020000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfGetCategories_020000] 
	-- Add the parameters for the stored procedure here
	@UserId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		CategoryName
		,CategoryId
	FROM
		dbo.cfCategories
	WHERE
		UserId = @UserId
	ORDER BY
		CategoryName

END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetCheckbookBalance_020000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfGetCheckbookBalance_020000] 
	-- Add the parameters for the stored procedure here
	@UserId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	IF NOT EXISTS(SELECT 1 FROM dbo.cfAccounts WHERE UserId = @UserId)
		INSERT INTO dbo.cfAccounts(AccountName, BeginningBalance, Balance, ClearedBalance, AccountTypeID, IsDefault, UserId)
		VALUES('', 0.00, 0.00, 0.00, 1, 1, @UserId)

	DECLARE @StartingBalance DECIMAL(18,2) = (SELECT TOP 1 BeginningBalance FROM dbo.cfAccounts WHERE UserId = @UserId)

	IF EXISTS(SELECT 1 FROM dbo.cfTransactions WHERE UserId = @UserId)
		SELECT
			SUM(Amount) + @StartingBalance AS [Balance]
			,SUM(CASE WHEN Cleared = 1 THEN Amount ELSE 0 END) + @StartingBalance AS [Cleared]
		FROM
			dbo.cfTransactions
		WHERE
			UserId = @UserId
	ELSE
		SELECT
			@StartingBalance AS [Balance]
			,@StartingBalance AS [Cleared]

END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetChore]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetChore]
	@choreid INT
AS
BEGIN
	SET NOCOUNT ON;
	SELECT
		ChoreId
		,ChoreName
		,UserId
		,IIF(1 & DaysOfWeek = 1,1,0) AS [IsSunday]
		,IIF(2 & DaysOfWeek = 2,1,0) AS [IsMonday]
		,IIF(4 & DaysOfWeek = 4,1,0) AS [IsTuesday]
		,IIF(8 & DaysOfWeek = 8,1,0) AS [IsWednesday]
		,IIF(16 & DaysOfWeek = 16,1,0) AS [IsThursday]
		,IIF(32 & DaysOfWeek = 32,1,0) AS [IsFriday]
		,IIF(64 & DaysOfWeek = 64,1,0) AS [IsSaturday]
		,IsDeleted
		,Sort
	FROM dbo.cfChores
	WHERE ChoreId = @choreid

END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetFrequencies]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfGetFrequencies] 
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		FrequencyName
		,FrequencyId
	FROM dbo.cfFrequencies
	ORDER BY Sort

END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetMyBudget_020000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO




-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfGetMyBudget_020000] 
	-- Add the parameters for the stored procedure here
	@EndDate DATETIME
	,@UserId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	IF NOT EXISTS(SELECT 1 FROM dbo.cfAccounts WHERE UserId = @UserId)
		INSERT INTO dbo.cfAccounts(AccountName, BeginningBalance, Balance, ClearedBalance, AccountTypeID, IsDefault, UserId)
		VALUES('', 0.00, 0.00, 0.00, 1, 1, @UserId)

	DECLARE @StartingBalance DECIMAL(18,2) = (SELECT TOP 1 BeginningBalance FROM dbo.cfAccounts WHERE UserId = @UserId)

	IF EXISTS(SELECT 1 FROM dbo.cfTransactions WHERE UserId = @UserId)
	SET @StartingBalance = (SELECT TOP 1 SUM(Amount) + @StartingBalance FROM dbo.cfTransactions WHERE UserId = @UserId)

	DECLARE @altEndDate DATETIME = (SELECT TOP 1 [NextDueDate] FROM  [dbo].[cfBudgets] AS [budget] WHERE budget.UserId = @UserId AND budget.BudgetTypeID = 0 ORDER BY NextDueDate)

	IF (@altEndDate > @EndDate) SET @EndDate = @altEndDate

    DECLARE @tmp TABLE(
		BudgetId INT
	   ,Name VARCHAR(255)
	   ,BudgetTypeId INT
	   ,CategoryId INT
	   ,Category VARCHAR(255)
	   ,FrequencyId INT
	   ,NextDueDate DATETIME
	   ,EndDate DATE
	   ,Amount DECIMAL(18,2)
	   ,Complete INT
	   ,IsAutomatic BIT
	   ,IsBill BIT
	   ,IsLate BIT
	   ,PayeeId INT
  )

    DECLARE @budgets TABLE(
		BudgetItemId INT  IDENTITY(1,1) NOT NULL
		,BudgetId INT
	   ,BudgetName VARCHAR(255)
	   ,CategoryId INT
	   ,Category VARCHAR(255)
	   ,DueDate DATE
	   ,Amount DECIMAL(18,2)
	   ,BudgetTypeId INT
	   ,FrequencyId INT
	   ,IsAutomatic BIT
	   ,IsBill BIT
	   ,IsLate BIT
	   ,PayeeId INT
	)

  --INSERT INTO @budgets(BudgetId,BudgetName,Category,Amount,BudgetTypeId) VALUES(-1,'Starting Balance','',@StartingBalance,0)

    INSERT INTO @tmp(BudgetId,Name,BudgetTypeId,CategoryId,Category,FrequencyId,NextDueDate,EndDate,Amount,Complete,IsAutomatic,IsBill,IsLate,PayeeId)
	SELECT 
		budget.BudgetId
		,budget.BudgetName
		,[BudgetTypeID]
		,budget.CategoryId
		,cat.CategoryName
		,[FrequencyID]
		,[NextDueDate]
		,CASE WHEN [EndDate] = '1/1/1970' THEN @EndDate ELSE EndDate END
		,[Amount]
		,0
		,ISNULL(IsAutomatic,0) AS [IsAutomatic]
		,ISNULL(IsBill,0) AS [IsBill]
		,ISNULL(IsLate,0) AS [IsLate]
		,PayeeId
	FROM 
		[dbo].[cfBudgets] AS [budget]
		LEFT OUTER JOIN cfCategories AS [cat] ON budget.CategoryID = cat.categoryid
	WHERE budget.UserId = @UserId
      
    WHILE EXISTS(
	   SELECT 1 
	   FROM @tmp 
	   WHERE 
		  NextDueDate < @EndDate 
		  AND NextDueDate < EndDate
	 AND Complete = 0
    ) BEGIN
	   INSERT INTO @budgets(BudgetId,BudgetName,CategoryId,Category,DueDate,Amount,BudgetTypeId,FrequencyId,IsAutomatic,IsBill,IsLate,PayeeId)
	   SELECT BudgetId,Name,CategoryId,Category,NextDueDate,CASE BudgetTypeId WHEN 0 THEN Amount ELSE -Amount END,BudgetTypeId,FrequencyId,IsAutomatic,IsBill,IsLate,PayeeId
	   FROM @tmp
	   WHERE 
			NextDueDate < @EndDate 
			AND NextDueDate < EndDate
			AND Complete = 0
	   UPDATE @tmp
	   SET
		  Complete = CASE WHEN  FrequencyId = 0 THEN 1 ELSE 0 END
		  ,NextDueDate = CASE FrequencyId
			 WHEN 0 THEN @EndDate
			 WHEN 1 THEN DATEADD(WEEK,1,NextDueDate)
			 WHEN 2 THEN DATEADD(WEEK,2,NextDueDAte)
			 WHEN 4 THEN DATEADD(MONTH,1,NextDueDate)
			 WHEN 5 THEN DATEADD(MONTH,2,NextDueDate)
			 WHEN 6 THEN DATEADD(MONTH,3,NextDueDate)
			 WHEN 7 THEN DATEADD(WEEK,5,NextDueDate)
			 WHEN 8 THEN 
				CASE 
					WHEN DATEPART(DAY,NextDueDate) = 1 THEN DATEADD(DAY,14,NextDueDate)
					ELSE DATEADD(DAY,-DATEPART(DAY,NextDueDate)+1,DATEADD(MONTH,1,NextDueDate))
				END
			WHEN 9 THEN DATEADD(YEAR,1,NextDueDate)
			WHEN 10 THEN DATEADD(DAY,5,NextDueDate)
			WHEN 11 THEN DATEADD(WEEK,6,NextDueDate)
			WHEN 12 THEN DATEADD(WEEK,3,NextDueDate)
			WHEN 13 THEN DATEADD(WEEK,4,NextDueDate)
		  END
	   WHERE 
		  NextDueDate < @EndDate 
		  AND NextDueDate < EndDate
		  AND Complete = 0
    END
	
	SELECT 
		@StartingBalance + sum(Amount) over (order by DueDate ASC, Amount DESC ROWS UNBOUNDED PRECEDING) AS [Balance]
		,bgt.Amount
		,bgt.BudgetId
		,bgt.BudgetName
		,bgt.CategoryId
		,bgt.Category
		,CONVERT(VARCHAR,bgt.DueDate,101) AS [DueDate]
		,CASE
			WHEN Amount > 0 THEN 'true'
			ELSE 'false'
		END AS [IsBold]
		,bgt.BudgetTypeId
		,bgt.FrequencyId
		,fqcy.FrequencyName
		,CASE bgt.IsAutomatic WHEN 1 THEN 'true' ELSE 'false' END AS [IsAuto]
		,CASE bgt.IsBill WHEN 1 THEN 'true' ELSE 'false' END AS [IsBill]
		,CASE bgt.IsLate WHEN 1 THEN 'true' ELSE 'false' END AS [IsLate]
		,ISNULL(pye.PayeeName,'') AS [Payee]
    FROM 
		@budgets AS [bgt]
		LEFT OUTER JOIN dbo.cfFrequencies AS fqcy ON bgt.FrequencyId = fqcy.FrequencyId
		LEFT OUTER JOIN dbo.cfPayees AS pye on bgt.PayeeId = pye.PayeeId
    ORDER BY bgt.DueDate ASC, Amount DESC
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetMyCheckbook_020000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfGetMyCheckbook_020000] 
	-- Add the parameters for the stored procedure here
	@PageSize INT
	,@Page INT
	,@ClearedFilter INT = 1
	,@UserId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	IF NOT EXISTS(SELECT 1 FROM dbo.cfAccounts WHERE UserId = @UserId)
		INSERT INTO dbo.cfAccounts(AccountName, BeginningBalance, Balance, ClearedBalance, AccountTypeID, IsDefault, UserId)
		VALUES('', 0.00, 0.00, 0.00, 1, 1, @UserId)

	DECLARE @StartingBalance DECIMAL(18,2) = (SELECT TOP 1 BeginningBalance FROM dbo.cfAccounts WHERE UserId = @UserId)
	DECLARE @MaxRow INT = (@Page * @PageSize)
	DECLARE @MinRow INT = (@MaxRow - @PageSize)
	DECLARE @TransactionCount INT = (SELECT COUNT(*) FROM dbo.cfTransactions WHERE UserId = @UserId)
	SELECT
		*
	FROM (
		SELECT
			TransactionId
			,CONVERT(VARCHAR,TransactionDate,111) AS TransactionDate
			,Amount
			,Cleared
			,CASE WHEN Cleared = 0 THEN 'true' ELSE 'false' END AS [ShowNotCleared]
			,CASE WHEN Cleared = 0 THEN 'false' ELSE 'true' END AS [ShowCleared]
			,tra.PayeeId
			,pay.PayeeName
			,tra.CategoryId
			,cat.CategoryName
			,Balance
			,ROW_NUMBER() OVER (ORDER BY Cleared ASC, TransactionDate DESC, Amount ASC) AS RowNum
			,@MinRow + 1 AS [MinRow]
			,@MaxRow AS [MaxRow]
			,@TransactionCount AS [TransactionCount]
			,CEILING(CONVERT(FLOAT,@TransactionCount) / 50) AS [PageCount]
		FROM 
			(
				SELECT
					TransactionId
					,TransactionDate
					,Amount
					,Cleared
					,PayeeId
					,CategoryId
					,SUM(Amount) OVER (ORDER BY Cleared DESC, TransactionDate ASC, Amount DESC ROWS UNBOUNDED PRECEDING) AS [Balance]
				FROM (
					SELECT 
						-1 AS [TransactionId]
						,'1/1/1970' AS [TransactionDate]
						,@StartingBalance AS [Amount]
						,1 AS [Cleared]
						,-1 AS [PayeeId]
						,-1 AS [CategoryId]
					UNION ALL SELECT
						TransactionId
						,TransactionDate
						,Amount
						,Cleared
						,PayeeId
						,CategoryId
					FROM dbo.cfTransactions WITH(READPAST)
					WHERE UserId = @UserId
				) AS [tra]
			) AS [tra]
		LEFT OUTER JOIN dbo.cfPayees as [pay] WITH(READPAST) ON tra.PayeeId = pay.PayeeId
		LEFT OUTER JOIN dbo.cfCategories AS [cat] WITH(READPAST) ON tra.CategoryId = cat.CategoryId
		WHERE 
			TransactionDate > '1/1/1970'
			AND (
				(@ClearedFilter = 1)
				OR (@ClearedFilter = 2 AND Cleared = 1)
				OR (@ClearedFilter = 3 AND Cleared = 0)
			)
	) AS [tra]
	WHERE RowNum > @MinRow AND RowNum <= @MaxRow
	ORDER BY Cleared ASC, TransactionDate DESC, Amount ASC
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetMyCheckbook_040100]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfGetMyCheckbook_040100] 
	-- Add the parameters for the stored procedure here
	@UserId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	IF NOT EXISTS(SELECT 1 FROM dbo.cfAccounts WHERE UserId = @UserId)
		INSERT INTO dbo.cfAccounts(AccountName, BeginningBalance, Balance, ClearedBalance, AccountTypeID, IsDefault, UserId)
		VALUES('', 0.00, 0.00, 0.00, 1, 1, @UserId)

	DECLARE @StartingBalance DECIMAL(18,2) = (SELECT TOP 1 BeginningBalance FROM dbo.cfAccounts WHERE UserId = @UserId)

	SELECT TOP 1000
		TransactionId
		,CONVERT(VARCHAR,TransactionDate,111) AS TransactionDate
		,Amount
		,Cleared
		,CASE WHEN Cleared = 0 THEN 'true' ELSE 'false' END AS [ShowNotCleared]
		,CASE WHEN Cleared = 0 THEN 'false' ELSE 'true' END AS [ShowCleared]
		,tra.PayeeId
		,pay.PayeeName
		,tra.CategoryId
		,cat.CategoryName
		,Balance
	FROM 
		(
			SELECT
				TransactionId
				,TransactionDate
				,Amount
				,Cleared
				,PayeeId
				,CategoryId
				,SUM(Amount) OVER (ORDER BY Cleared DESC, TransactionDate ASC, Amount DESC ROWS UNBOUNDED PRECEDING) AS [Balance]
			FROM (
				SELECT 
					TransactionId
					,TransactionDate
					,Amount
					,Cleared
					,PayeeId
					,CategoryId
					FROM dbo.cfTransactions WITH(READPAST)
					WHERE UserId = @UserId
				UNION ALL SELECT
					-1 AS [TransactionId]
					,CAST('1/1/1970' AS DATE) AS [TransactionDate]
					,@StartingBalance AS [Amount]
					,1 AS [Cleared]
					,-1 AS [PayeeId]
					,-1 AS [CategoryId]
			) AS [tra]
		) AS [tra]
	LEFT OUTER JOIN dbo.cfPayees as [pay] WITH(READPAST) ON tra.PayeeId = pay.PayeeId
	LEFT OUTER JOIN dbo.cfCategories AS [cat] WITH(READPAST) ON tra.CategoryId = cat.CategoryId
	WHERE 
		TransactionDate > '1/1/1970'
	ORDER BY TransactionDate DESC
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetNotifications]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetNotifications]
AS
BEGIN
	SET NOCOUNT ON;
	SELECT
		NotificationType
		,MAX(NotificationAt) AS [NotificationAt]
	FROM dbo.cfNotifications
	GROUP BY NotificationType
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetPayees_020000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetPayees_020000] 
	@UserId INT
AS
BEGIN
	SET NOCOUNT ON;
	SELECT 
		pay.[PayeeId]
		,[PayeeName]
		,SUM(
			CASE
			WHEN tra.TransactionId IS NULL THEN 0
			ELSE 1
			END
		) AS [TransactionCount]
		,SUM(
			CASE
			WHEN bud.BudgetId IS NULL THEN 0
			ELSE 1
			END
		) AS [BudgetCount]
		,ISNULL(CONVERT(VARCHAR, MAX(tra.TransactionDate)),'') AS [LastTransaction]
		,ISNULL(SUM(tra.Amount), 0.00) AS [TransactionTotal]
	FROM
		[dbo].[cfPayees] AS [pay]
		LEFT OUTER JOIN dbo.cfTransactions AS [tra] ON pay.PayeeId = tra.PayeeId
		LEFT OUTER JOIN dbo.cfBudgets AS [bud] ON pay.PayeeId = bud.PayeeId
	WHERE
		pay.UserId = 3
		AND pay.IsDeleted = 0
	GROUP BY
		pay.[PayeeId]
		,[PayeeName]
	ORDER BY
		PayeeName
END
GO
/****** Object:  StoredProcedure [dbo].[spcfGetTransaction]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfGetTransaction]
	@TransactionId INT
AS
BEGIN
	SET NOCOUNT ON;
	SELECT
		TransactionId
		,TransactionDate
		,Amount
		,tr.PayeeId
		,pa.PayeeName AS [Payee]
		,tr.CategoryId
		,ca.CategoryName AS [Category]
		,AccountId
		,Cleared
	FROM dbo.cfTransactions AS [tr]
	JOIN dbo.cfPayees AS [pa] ON tr.PayeeId = pa.PayeeId
	JOIN dbo.cfCategories AS [ca] ON tr.CategoryId = ca.CategoryId
	WHERE TransactionId = @TransactionId
END
GO
/****** Object:  StoredProcedure [dbo].[spcfMarkPaid]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfMarkPaid] 
	-- Add the parameters for the stored procedure here
	@BudgetId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	UPDATE
		dbo.cfBudgets
	SET
		NextDueDate = CASE FrequencyId
			WHEN 0 THEN NextDueDate
			WHEN 1 THEN DATEADD(DAY,7,NextDueDate)
			WHEN 2 THEN DATEADD(DAY,14,NextDueDAte)
			WHEN 4 THEN DATEADD(MONTH,1,NextDueDate)
			WHEN 5 THEN DATEADD(MONTH,2,NextDueDate)
			WHEN 6 THEN DATEADD(MONTH,3,NextDueDate)
			WHEN 7 THEN DATEADD(WEEK,5,NextDueDate)
			 WHEN 8 THEN 
				CASE 
					WHEN DATEPART(DAY,NextDueDate) = 1 THEN DATEADD(DAY,14,NextDueDate)
					ELSE DATEADD(DAY,-DATEPART(DAY,NextDueDate)+1,DATEADD(MONTH,1,NextDueDate))
				END
			WHEN 9 THEN DATEADD(YEAR,1,NextDueDate)
			WHEN 10 THEN DATEADD(DAY,5,NextDueDate)
			WHEN 11 THEN DATEADD(WEEK,6,NextDueDate)
			WHEN 12 THEN DATEADD(WEEK,3,NextDueDate)
			WHEN 13 THEN DATEADD(WEEK,4,NextDueDate)
			WHEN 14 THEN DATEADD(MONTH,6,NextDueDate)
		END
	WHERE
		BudgetId = @BudgetId

	IF EXISTS(SELECT 1 FROM dbo.cfBudgets WHERE BudgetId = @BudgetId AND FrequencyID = 0)
		DELETE FROM dbo.cfBudgets WHERE BudgetId = @BudgetId

	IF EXISTS(SELECT 1 FROM dbo.cfBudgets WHERE BudgetId = @BudgetId AND NextDueDate > EndDate AND EndDate != '1/1/1970')
		DELETE FROM dbo.cfBudgets WHERE BudgetId = @BudgetId

END
GO
/****** Object:  StoredProcedure [dbo].[spcfMyBudgetGetChart]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfMyBudgetGetChart] 
	-- Add the parameters for the stored procedure here
	@EndDate DATETIME
	,@UserId INT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	DECLARE @now DATETIME = DATEADD(HOUR,2,GETDATE())

    -- Insert statements for procedure here
	IF NOT EXISTS(SELECT 1 FROM dbo.cfAccounts WHERE UserId = @UserId)
		INSERT INTO dbo.cfAccounts(AccountName, BeginningBalance, Balance, ClearedBalance, AccountTypeID, IsDefault, UserId)
		VALUES('', 0.00, 0.00, 0.00, 1, 1, @UserId)

	DECLARE @StartingBalance DECIMAL(18,2) = (SELECT TOP 1 BeginningBalance FROM dbo.cfAccounts WHERE UserId = @UserId)

	IF EXISTS(SELECT 1 FROM dbo.cfTransactions WHERE UserId = @UserId)
	SET @StartingBalance = (SELECT TOP 1 SUM(Amount) + @StartingBalance FROM dbo.cfTransactions WHERE UserId = @UserId)

    DECLARE @tmp TABLE(
		BudgetId INT
	   ,Name VARCHAR(255)
	   ,BudgetTypeId INT
	   ,Category VARCHAR(255)
	   ,FrequencyId INT
	   ,NextDueDate DATETIME
	   ,EndDate DATE
	   ,Amount DECIMAL(18,2)
	   ,Complete INT
  )

  DECLARE @budgetitems TABLE(
		BudgetItemId INT  IDENTITY(1,1) NOT NULL
		,BudgetId INT
	   ,BudgetName VARCHAR(255)
	   ,Category VARCHAR(255)
	   ,DueDate DATE
	   ,Amount DECIMAL(18,2)
	   ,BudgetTypeId INT
	   ,FrequencyId INT
  )

	DECLARE @forecast TABLE(
		[Date] DATETIME
		,Balance DECIMAL(18,2)
	)

    INSERT INTO @tmp(BudgetId,Name,BudgetTypeId,Category,FrequencyId,NextDueDate,EndDate,Amount,Complete)
	SELECT 
		budget.BudgetId
		,budget.BudgetName
		,[BudgetTypeID]
		,cat.CategoryName
		,[FrequencyID]
		,[NextDueDate]
		,CASE WHEN [EndDate] = '1/1/1970' THEN @EndDate ELSE EndDate END
		,[Amount]
		,0
	FROM 
		[dbo].[cfBudgets] AS [budget]
		LEFT OUTER JOIN cfCategories AS [cat] ON budget.CategoryID = cat.categoryid
	WHERE budget.UserId = @UserId
      
    WHILE EXISTS(
	   SELECT 1 
	   FROM @tmp 
	   WHERE 
		  NextDueDate < @EndDate 
		  AND NextDueDate < EndDate
	 AND Complete = 0
    ) BEGIN
	   INSERT INTO @budgetitems(BudgetId,BudgetName,Category,DueDate,Amount,BudgetTypeId,FrequencyId)
	   SELECT BudgetId,Name,Category,NextDueDate,CASE BudgetTypeId WHEN 0 THEN Amount ELSE -Amount END,BudgetTypeId,FrequencyId
	   FROM @tmp
	   WHERE 
		  NextDueDate < @EndDate 
		  AND NextDueDate < EndDate
	 AND Complete = 0
	   UPDATE @tmp
	   SET
		  Complete = CASE WHEN  FrequencyId = 0 THEN 1 ELSE 0 END
		  ,NextDueDate = CASE FrequencyId
			 WHEN 0 THEN @EndDate
			 WHEN 1 THEN DATEADD(WEEK,1,NextDueDate)
			 WHEN 2 THEN DATEADD(WEEK,2,NextDueDAte)
			 WHEN 4 THEN DATEADD(MONTH,1,NextDueDate)
			 WHEN 5 THEN DATEADD(MONTH,2,NextDueDate)
			 WHEN 6 THEN DATEADD(MONTH,3,NextDueDate)
			 WHEN 7 THEN DATEADD(WEEK,5,NextDueDate)
			 WHEN 8 THEN 
				CASE 
					WHEN DATEPART(DAY,NextDueDate) = 1 THEN DATEADD(DAY,14,NextDueDate)
					ELSE DATEADD(DAY,-DATEPART(DAY,NextDueDate)+1,DATEADD(MONTH,1,NextDueDate))
				END
			WHEN 9 THEN DATEADD(YEAR,1,NextDueDate)
			WHEN 10 THEN DATEADD(DAY,5,NextDueDate)
			WHEN 11 THEN DATEADD(WEEK,6,NextDueDate)
			WHEN 12 THEN DATEADD(WEEK,3,NextDueDate)
			WHEN 13 THEN DATEADD(WEEK,4,NextDueDate)
			 WHEN 14 THEN DATEADD(MONTH,6,NextDueDate)
		  END
	   WHERE 
		  NextDueDate < @EndDate 
		  AND NextDueDate < EndDate
		  AND Complete = 0
    END
	
	INSERT INTO @forecast([Date],[Balance])
	SELECT 
		CONVERT(VARCHAR,bgt.DueDate,101) AS [DueDate]
		,@StartingBalance + sum(Amount) over (order by DueDate ASC, Amount DESC ROWS UNBOUNDED PRECEDING) AS [Balance]
    FROM 
		@budgetitems AS [bgt]
    ORDER BY bgt.DueDate ASC, Amount DESC

	SELECT [Date], MIN([Balance]) AS [Balance]
	FROM @forecast
	GROUP BY [Date]
	ORDER BY [Date]

END
GO
/****** Object:  StoredProcedure [dbo].[spcfMyCheckbookDeleteTransaction]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfMyCheckbookDeleteTransaction]
	@TransactionId INT
AS
BEGIN
	SET NOCOUNT ON;
	DELETE FROM dbo.cfTransactions
	WHERE TransactionId = @TransactionId
END
GO
/****** Object:  StoredProcedure [dbo].[spcfMyCheckbookGetLastTransactionByPayeeId]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfMyCheckbookGetLastTransactionByPayeeId]
	@PayeeId INT
AS
BEGIN
	SET NOCOUNT ON;
	SELECT 
		CASE WHEN tr.Amount < 0 THEN -tr.Amount ELSE tr.Amount END AS [Amount]
		,CASE WHEN tr.Amount < 0 THEN 0 ELSE 1 END AS [TransactionTypeId]
		,py.PayeeName
		,tr.CategoryId
		,ct.CategoryName
		,tr.TransactionDate
	FROM 
		dbo.cfTransactions AS [tr] WITH(READPAST)
		JOIN dbo.cfPayees AS [py] WITH(READPAST) ON tr.PayeeId = py.PayeeId
		JOIN dbo.cfCategories AS [ct] WITH(READPAST) ON tr.CategoryId = ct.CategoryId
	WHERE tr.PayeeId = @PayeeId
	ORDER BY tr.TransactionDate DESC
END
GO
/****** Object:  StoredProcedure [dbo].[spcfMyCheckboxMarkTransactionCleared]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfMyCheckboxMarkTransactionCleared]
	@TransactionId INT
AS
BEGIN
	SET NOCOUNT ON;
	UPDATE dbo.cfTransactions
	SET Cleared = 1
	WHERE TransactionId = @TransactionId
END
GO
/****** Object:  StoredProcedure [dbo].[spcfMyCheckboxMarkTransactionUncleared]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfMyCheckboxMarkTransactionUncleared]
	@TransactionId INT
AS
BEGIN
	SET NOCOUNT ON;
	UPDATE dbo.cfTransactions
	SET Cleared = 0
	WHERE TransactionId = @TransactionId
END
GO
/****** Object:  StoredProcedure [dbo].[spcfSaveAccount]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfSaveAccount]
	@accountid INT
	,@userid INT
	,@accountname VARCHAR(255)
	,@accountnumber VARCHAR(16)
	,@accounttypeid INT
	,@balance DECIMAL(18,2)
	,@creditlimit DECIMAL(18,2)
	,@availablecredit DECIMAL(18,2)
	,@duedate INT
	,@minimumpayment DECIMAL(18,2)
	,@interestrate DECIMAL(18,2)
	,@weburl VARCHAR(4000)
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @now DATETIME = DATEADD(HOUR,2,GETDATE())
	IF EXISTS(SELECT 1 FROM dbo.cfAccounts WHERE AccountId = @accountid) BEGIN
		UPDATE dbo.cfAccounts
		SET
			AccountName = @accountname
			,AccountNumber = @accountnumber
			,AccountTypeID = @accounttypeid
			,Balance = @balance
			,CreditLimit = @creditlimit
			,AvailableCredit = @availablecredit
			,DueDate = @duedate
			,MinimumPayment = @minimumpayment
			,InterestRate = @interestrate
			,WebURL = @weburl
			,LastUpdated = @now
		WHERE AccountId = @accountid
	END ELSE BEGIN
		INSERT INTO dbo.cfAccounts(
			AccountName
			,AccountNumber
			,AccountTypeID
			,Balance
			,CreditLimit
			,AvailableCredit
			,DueDate
			,MinimumPayment
			,InterestRate
			,WebURL
			,BeginningBalance
			,ClearedBalance
			,IsDefault
			,UserId
			,LastUpdated
		) VALUES (
			@accountname
			,@accountnumber
			,@accounttypeid
			,@balance
			,@creditlimit
			,@availablecredit
			,@duedate
			,@minimumpayment
			,@interestrate
			,@weburl
			,0.00
			,0.00
			,0
			,@userid
			,@now
		)
	END
END
GO
/****** Object:  StoredProcedure [dbo].[spcfSaveBudget_030000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfSaveBudget_030000] 
	@BudgetId INT
	,@BudgetName VARCHAR(255)
	,@FrequencyId INT
	,@NextDueDate DATETIME
	,@EndDate DATETIME
	,@Amount DECIMAL(18,2)
	,@BudgetTypeId INT
	,@Category  NVARCHAR(255)
	,@UserId INT
	,@IsAuto BIT
	,@IsBill BIT
	,@IsLate BIT
	,@Payee NVARCHAR(255)
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @CategoryId INT = -1
	IF EXISTS(SELECT 1 from dbo.cfCategories WHERE CategoryName LIKE @Category AND UserId = @UserId)
		SET @CategoryId = (SELECT TOP 1 CategoryId FROM dbo.cfCategories WHERE CategoryName LIKE @Category AND UserId = @UserId)
	ELSE BEGIN
		INSERT INTO dbo.cfCategories(CategoryName, UserId) VALUES(@Category, @UserId)
		SET @CategoryId = @@IDENTITY
	END
	DECLARE @PayeeId INT = -1
	IF EXISTS(SELECT 1 from dbo.cfPayees WHERE PayeeName LIKE @Payee AND UserId = @UserId)
		SET @PayeeId = (SELECT TOP 1 PayeeId FROM dbo.cfPayees WHERE PayeeName LIKE @Payee AND UserId = @UserId)
	ELSE BEGIN
		INSERT INTO dbo.cfPayees(PayeeName, UserId, IsDeleted) VALUES(@Payee, @UserId, 0)
		SET @PayeeId = @@IDENTITY
	END
	IF @BudgetId = -1
		INSERT INTO dbo.cfBudgets(
			BudgetTypeId
			,BudgetName
			,FrequencyId
			,NextDueDate
			,EndDate
			,Amount
			,CategoryId
			,UserId
			,IsAutomatic
			,IsBill
			,IsLate
			,PayeeId
		) VALUES (
			@BudgetTypeId
			,@BudgetName
			,@FrequencyId
			,@NextDueDate
			,@EndDate
			,@Amount
			,@CategoryId
			,@UserId
			,@IsAuto
			,@IsBill
			,@IsLate
			,@PayeeId
		)
	ELSE
		UPDATE dbo.cfBudgets
		SET
			BudgetTypeID = @BudgetTypeId
			,BudgetName = @BudgetName
			,FrequencyID = @FrequencyId
			,NextDueDate = @NextDueDate
			,EndDate = @EndDate
			,Amount = @Amount
			,CategoryId = @CategoryId
			,UserId = @UserId
			,IsAutomatic = @IsAuto
			,IsBill = @IsBill
			,IsLate = @IsLate
			,PayeeId = @PayeeId
		WHERE
			BudgetId = @BudgetId

END
GO
/****** Object:  StoredProcedure [dbo].[spcfSaveChore]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfSaveChore]
	@choreid INT
	,@chorename VARCHAR(64)
	,@userid INT
	,@ismonday BIT
	,@istuesday BIT
	,@iswednesday BIT
	,@isthursday BIT
	,@isfriday BIT
	,@issaturday BIT
	,@issunday BIT
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @daysofweek INT = 0
	IF @issunday = 1 SET @daysofweek = @daysofweek + 1
	IF @ismonday = 1 SET @daysofweek = @daysofweek + 2
	IF @istuesday = 1 SET @daysofweek = @daysofweek + 4
	IF @iswednesday = 1 SET @daysofweek = @daysofweek + 8
	IF @isthursday = 1 SET @daysofweek = @daysofweek + 16
	IF @isfriday = 1 SET @daysofweek = @daysofweek + 32
	IF @issaturday = 1 SET @daysofweek = @daysofweek + 64
	IF @choreid = -1
		INSERT INTO dbo.cfChores(ChoreName,UserId,DaysOfWeek,IsDeleted)
		VALUES(@chorename,@userid,@daysofweek,0)
	ELSE
		UPDATE dbo.cfChores
		SET
			ChoreName = @chorename
			,UserId = @userid
			,DaysOfWeek = @daysofweek
		WHERE ChoreId = @choreid
END
GO
/****** Object:  StoredProcedure [dbo].[spcfSaveTransaction_030000]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfSaveTransaction_030000]
	@TransactionId INT
	,@TransactionDate DATE
	,@Amount DECIMAL(9,2)
	,@Payee  NVARCHAR(255)
	,@Category  NVARCHAR(255)
	,@AccountId INT = 1
	,@Cleared BIT
	,@UserId INT
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @CategoryId INT = -1
	IF EXISTS(SELECT 1 from dbo.cfCategories WHERE CategoryName LIKE @Category AND UserId = @UserId)
		SET @CategoryId = (SELECT TOP 1 CategoryId FROM dbo.cfCategories WHERE CategoryName LIKE @Category AND UserId = @UserId)
	ELSE BEGIN
		INSERT INTO dbo.cfCategories(CategoryName, UserId) VALUES(@Category, @UserId)
		SET @CategoryId = @@IDENTITY
	END
	DECLARE @PayeeId INT = -1
	IF EXISTS(SELECT 1 from dbo.cfPayees WHERE PayeeName LIKE @Payee AND UserId = @UserId)
		SET @PayeeId = (SELECT TOP 1 PayeeId FROM dbo.cfPayees WHERE PayeeName LIKE @Payee AND UserId = @UserId)
	ELSE BEGIN
		INSERT INTO dbo.cfPayees(PayeeName, UserId, IsDeleted) VALUES(@Payee, @UserId, 0)
		SET @PayeeId = @@IDENTITY
	END
	IF (@TransactionId = -1 )
		INSERT INTO dbo.cfTransactions(
			TransactionDate
			,Amount
			,PayeeId
			,CategoryId
			,AccountId
			,Cleared
			,UserId
		) VALUES (
			@TransactionDate
			,@Amount
			,@PayeeId
			,@CategoryId
			,@AccountId
			,@Cleared
			,@UserId
		)
	ELSE
		UPDATE dbo.cfTransactions
		SET
			TransactionDate = @TransactionDate
			,Amount = @Amount
			,PayeeId = @PayeeId
			,CategoryId = @CategoryId
			,AccountId = @AccountId
			,Cleared = @Cleared
			,UserId = @UserId
		WHERE
			TransactionId = @TransactionId
END
GO
/****** Object:  StoredProcedure [dbo].[spcfSetBalance]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spcfSetBalance] 
	-- Add the parameters for the stored procedure here
	@Balance DECIMAL(18,2)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	UPDATE dbo.cfAccounts
	SET Balance = @Balance
	

END
GO
/****** Object:  StoredProcedure [dbo].[spcfSetNotificationSent]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spcfSetNotificationSent]
	@notificationtype INT
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @now DATETIME = DATEADD(HOUR,2,GETDATE())
	IF EXISTS(SELECT 1 FROM dbo.cfNotifications WHERE NotificationType = @notificationtype)
		UPDATE dbo.cfNotifications
		SET NotificationAt = @now
		WHERE NotificationType = @notificationtype
	ELSE
		INSERT INTO dbo.cfNotifications(NotificationType, NotificationAt)
		VALUES(@notificationtype,@now)
END
GO
/****** Object:  StoredProcedure [dbo].[spCfUpdateAccount]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spCfUpdateAccount]
	@accountid INT
	,@balance DEC(18,2) = -9999999999.99
	,@creditlimit DEC(18,2) = -9999999999.99
	,@availablecredit DEC(18,2) = -9999999999.99
	,@accountnumber VARCHAR(32) = ''
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @now DATETIME = DATEADD(HOUR,2,GETDATE())
	IF (@balance > -9999999999.99) UPDATE dbo.cfAccounts SET Balance = @balance, LastUpdated = @now WHERE AccountId = @accountid
	IF (@creditlimit > -9999999999.99) UPDATE dbo.cfAccounts SET CreditLimit = @creditlimit, LastUpdated = @now WHERE AccountId = @accountid
	IF (@availablecredit > -9999999999.99) UPDATE dbo.cfAccounts SET AvailableCredit = @availablecredit, LastUpdated = @now WHERE AccountId = @accountid
	IF (@accountnumber != '') UPDATE dbo.cfAccounts SET AccountNumber = @accountnumber, LastUpdated = @now WHERE AccountId = @accountid

END
GO
