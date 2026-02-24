/****** Object:  Table [dbo].[cfAccounts]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfAccounts](
	[AccountId] [int] IDENTITY(1,1) NOT NULL,
	[AccountName] [nvarchar](255) NOT NULL,
	[BeginningBalance] [decimal](18, 2) NOT NULL,
	[Balance] [decimal](18, 2) NOT NULL,
	[ClearedBalance] [decimal](18, 2) NOT NULL,
	[AccountTypeID] [int] NOT NULL,
	[IsDefault] [bit] NOT NULL,
	[UserId] [int] NULL,
	[AccountNumber] [varchar](16) NULL,
	[DueDate] [int] NULL,
	[MinimumPayment] [decimal](18, 2) NULL,
	[InterestRate] [decimal](18, 2) NULL,
	[WebURL] [varchar](4000) NULL,
	[CreditLimit] [decimal](18, 2) NULL,
	[AvailableCredit] [decimal](18, 2) NULL,
	[LastUpdated] [datetime] NULL,
	[IsDeleted] [bit] NULL,
 CONSTRAINT [PK_cfAccounts] PRIMARY KEY CLUSTERED 
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cfAccountTypes]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfAccountTypes](
	[AccountTypeId] [int] NOT NULL,
	[AccountTypeName] [varchar](32) NOT NULL,
 CONSTRAINT [PK_cfAccountTypes] PRIMARY KEY CLUSTERED 
(
	[AccountTypeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cfBudgets]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfBudgets](
	[BudgetTypeID] [int] NOT NULL,
	[BudgetName] [nvarchar](255) NULL,
	[FrequencyID] [int] NULL,
	[NextDueDate] [datetime] NULL,
	[EndDate] [datetime] NULL,
	[Amount] [decimal](18, 2) NULL,
	[BudgetId] [int] IDENTITY(1,1) NOT NULL,
	[CategoryId] [int] NOT NULL,
	[UserId] [int] NULL,
	[IsAutomatic] [bit] NULL,
	[IsLate] [bit] NULL,
	[IsBill] [bit] NULL,
	[PayeeId] [int] NULL,
 CONSTRAINT [PK_cfBudget] PRIMARY KEY CLUSTERED 
(
	[BudgetId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cfCategories]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfCategories](
	[CategoryName] [nvarchar](255) NULL,
	[CategoryId] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NULL,
 CONSTRAINT [PK_cfCatagory] PRIMARY KEY CLUSTERED 
(
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cfFrequencies]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfFrequencies](
	[FrequencyId] [int] IDENTITY(1,1) NOT NULL,
	[FrequencyName] [varchar](32) NOT NULL,
	[Sort] [int] NULL,
 CONSTRAINT [PK_cfFrequency] PRIMARY KEY CLUSTERED 
(
	[FrequencyId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cfPayees]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfPayees](
	[PayeeId] [int] IDENTITY(1,1) NOT NULL,
	[PayeeName] [varchar](64) NOT NULL,
	[UserId] [int] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_cfPayees] PRIMARY KEY CLUSTERED 
(
	[PayeeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cfTransactions]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfTransactions](
	[TransactionId] [int] IDENTITY(1,1) NOT NULL,
	[TransactionDate] [date] NOT NULL,
	[Amount] [decimal](9, 2) NOT NULL,
	[PayeeId] [int] NOT NULL,
	[CategoryId] [int] NOT NULL,
	[AccountId] [int] NOT NULL,
	[Cleared] [bit] NOT NULL,
	[UserId] [int] NULL,
 CONSTRAINT [PK_cfTransactions] PRIMARY KEY CLUSTERED 
(
	[TransactionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[cfUsers]    Script Date: 2/24/2026 8:48:27 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cfUsers](
	[UserId] [int] NOT NULL,
	[SiteId] [int] NOT NULL,
	[UserName] [varchar](64) NOT NULL,
	[EmailAddress] [varchar](256) NULL,
	[DisplayName] [varchar](128) NOT NULL,
	[Salt] [varchar](32) NOT NULL,
	[PasswordHash] [varchar](256) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[FirstLogin] [datetime] NOT NULL,
	[LastLogin] [datetime] NOT NULL,
	[LastIPAddress] [varchar](16) NULL,
	[PasswordResetToken] [uniqueidentifier] NULL,
	[PasswordResetRequestOn] [datetime] NULL,
 CONSTRAINT [PK_cfUsers_1] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[cfBudgets]  WITH CHECK ADD  CONSTRAINT [FK_cfBudgets_cfCategories] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[cfCategories] ([CategoryId])
GO
ALTER TABLE [dbo].[cfBudgets] CHECK CONSTRAINT [FK_cfBudgets_cfCategories]
GO
ALTER TABLE [dbo].[cfBudgets]  WITH CHECK ADD  CONSTRAINT [FK_cfBudgets_cfPayees] FOREIGN KEY([PayeeId])
REFERENCES [dbo].[cfPayees] ([PayeeId])
GO
ALTER TABLE [dbo].[cfBudgets] CHECK CONSTRAINT [FK_cfBudgets_cfPayees]
GO
ALTER TABLE [dbo].[cfTransactions]  WITH CHECK ADD  CONSTRAINT [FK_cfTransactions_cfAccounts] FOREIGN KEY([AccountId])
REFERENCES [dbo].[cfAccounts] ([AccountId])
GO
ALTER TABLE [dbo].[cfTransactions] CHECK CONSTRAINT [FK_cfTransactions_cfAccounts]
GO
ALTER TABLE [dbo].[cfTransactions]  WITH CHECK ADD  CONSTRAINT [FK_cfTransactions_cfCategories] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[cfCategories] ([CategoryId])
GO
ALTER TABLE [dbo].[cfTransactions] CHECK CONSTRAINT [FK_cfTransactions_cfCategories]
GO
ALTER TABLE [dbo].[cfTransactions]  WITH CHECK ADD  CONSTRAINT [FK_cfTransactions_cfPayees] FOREIGN KEY([PayeeId])
REFERENCES [dbo].[cfPayees] ([PayeeId])
GO
ALTER TABLE [dbo].[cfTransactions] CHECK CONSTRAINT [FK_cfTransactions_cfPayees]
GO
