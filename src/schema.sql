ALTER DATABASE CHARACTER SET utf8mb4;


CREATE TABLE `ApprovalRules` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProductType` varchar(30) CHARACTER SET utf8mb4 NULL,
    `MinAmount` decimal(18,2) NOT NULL,
    `MaxAmount` decimal(18,2) NOT NULL,
    `RequiredLevel` int NOT NULL,
    `ApproverRole` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApprovalRules` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `GLAccounts` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `AccountCode` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `AccountName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `AccountType` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Balance` decimal(18,2) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_GLAccounts` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `IdempotencyRecords` (
    `IdempotencyKey` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ResponseBody` longtext CHARACTER SET utf8mb4 NULL,
    `StatusCode` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_IdempotencyRecords` PRIMARY KEY (`IdempotencyKey`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `JournalEntries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ReferenceNumber` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `BusinessDate` date NOT NULL,
    `Description` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `PostedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_JournalEntries` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanProducts` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProductName` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `MinAmount` decimal(18,2) NOT NULL,
    `MaxAmount` decimal(18,2) NOT NULL,
    `AllowedTenorsJson` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `MonthlyInterestRate` decimal(10,6) NOT NULL,
    `ProcessingFee` decimal(18,2) NOT NULL,
    `RepaymentFrequency` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Monthly',
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Active',
    `ProductType` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'PersonalUnsecured',
    `InterestMethod` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Flat',
    `GracePeriodMonths` int NOT NULL,
    `RequiresCollateral` tinyint(1) NOT NULL,
    `MaxLtvRatio` decimal(5,2) NULL,
    `EarlySettlementFeeRate` decimal(10,6) NOT NULL,
    `LateFeeRate` decimal(10,6) NOT NULL,
    `LateFeeGraceDays` int NOT NULL,
    `PromotionalRate` decimal(10,6) NULL,
    `PromotionalMonths` int NOT NULL,
    `Description` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanProducts` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `NotificationTemplates` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TemplateCode` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Body` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Channel` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_NotificationTemplates` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `Users` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Username` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `FullName` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `Role` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Active',
    `FailedLoginAttempts` int NOT NULL,
    `LockoutEnd` datetime(6) NULL,
    `BranchId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Users` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `JournalEntryLines` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `JournalEntryId` int NOT NULL,
    `GLAccountId` int NOT NULL,
    `DebitAmount` decimal(18,2) NOT NULL,
    `CreditAmount` decimal(18,2) NOT NULL,
    `LineDescription` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_JournalEntryLines` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_JournalEntryLines_GLAccounts_GLAccountId` FOREIGN KEY (`GLAccountId`) REFERENCES `GLAccounts` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JournalEntryLines_JournalEntries_JournalEntryId` FOREIGN KEY (`JournalEntryId`) REFERENCES `JournalEntries` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `DunningStrategies` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DpdThreshold` int NOT NULL,
    `ActionType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `NotificationTemplateId` int NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    `Priority` int NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_DunningStrategies` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DunningStrategies_NotificationTemplates_NotificationTemplate~` FOREIGN KEY (`NotificationTemplateId`) REFERENCES `NotificationTemplates` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `AccountingPeriods` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Year` int NOT NULL,
    `Month` int NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `OpenedAt` datetime(6) NOT NULL,
    `ClosedAt` datetime(6) NULL,
    `ClosedByUserId` int NULL,
    `Notes` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AccountingPeriods` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AccountingPeriods_Users_ClosedByUserId` FOREIGN KEY (`ClosedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `AuditLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `ActorUserId` int NULL,
    `ActionCode` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `EntityType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `EntityId` int NOT NULL,
    `OldValuesJson` longtext CHARACTER SET utf8mb4 NULL,
    `NewValuesJson` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AuditLogs` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AuditLogs_Users_ActorUserId` FOREIGN KEY (`ActorUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `Branches` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `BranchCode` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `BranchName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Address` longtext CHARACTER SET utf8mb4 NULL,
    `Phone` longtext CHARACTER SET utf8mb4 NULL,
    `ManagerUserId` int NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Branches` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Branches_Users_ManagerUserId` FOREIGN KEY (`ManagerUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `Customers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FullName` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `Phone` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `IdNumber` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Address` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `DateOfBirth` datetime(6) NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Active',
    `CreditScore` int NULL,
    `CreditStatus` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedByUserId` int NOT NULL,
    `AssignedToUserId` int NOT NULL,
    `BranchId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Customers_Users_AssignedToUserId` FOREIGN KEY (`AssignedToUserId`) REFERENCES `Users` (`Id`),
    CONSTRAINT `FK_Customers_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `EmailOtps` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `Email` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OtpCode` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Purpose` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DeeplinkToken` varchar(255) CHARACTER SET utf8mb4 NULL,
    `DeeplinkUrl` longtext CHARACTER SET utf8mb4 NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `IsUsed` tinyint(1) NOT NULL,
    `UsedAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_EmailOtps` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmailOtps_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `Notifications` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `LoanId` int NULL,
    `CustomerId` int NULL,
    `Type` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Title` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Message` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DeeplinkUrl` longtext CHARACTER SET utf8mb4 NULL,
    `IsRead` tinyint(1) NOT NULL,
    `ReadAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Notifications` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Notifications_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `PendingApprovalRequests` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ActionType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Payload` longtext CHARACTER SET utf8mb4 NOT NULL,
    `RequestedByUserId` int NOT NULL,
    `ApprovedByUserId` int NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `RejectionReason` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `DecidedAt` datetime(6) NULL,
    CONSTRAINT `PK_PendingApprovalRequests` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PendingApprovalRequests_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PendingApprovalRequests_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `RefreshTokens` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `TokenHash` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `DeviceId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `DeviceName` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `IsRevoked` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_RefreshTokens` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RefreshTokens_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `CicInquiries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `InquiryDate` datetime(6) NOT NULL,
    `CicScore` int NULL,
    `CicGrade` varchar(10) CHARACTER SET utf8mb4 NULL,
    `TotalBorrowingAtCic` decimal(18,2) NULL,
    `MaxDpdAtCic` int NULL,
    `RawResponse` longtext CHARACTER SET utf8mb4 NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Provider` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `RequestedByUserId` int NOT NULL,
    CONSTRAINT `PK_CicInquiries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CicInquiries_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CicInquiries_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `Collaterals` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `CollateralType` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `RegistrationNo` varchar(100) CHARACTER SET utf8mb4 NULL,
    `AppraisedValue` decimal(18,2) NOT NULL,
    `AppraisalDate` datetime(6) NOT NULL,
    `AppraisedBy` varchar(150) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Active',
    `Address` varchar(500) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Collaterals` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Collaterals_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `CreditChecks` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `ApplicationId` int NULL,
    `TotalLoans` int NOT NULL,
    `ActiveLoans` int NOT NULL,
    `OverdueLoans` int NOT NULL,
    `ClosedLoans` int NOT NULL,
    `TotalOutstanding` decimal(18,2) NOT NULL,
    `MaxDpdLast12Months` int NOT NULL,
    `CreditScore` int NOT NULL,
    `CreditGrade` varchar(5) CHARACTER SET utf8mb4 NOT NULL,
    `Recommendation` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `CheckedAt` datetime(6) NOT NULL,
    `CheckedByUserId` int NOT NULL,
    CONSTRAINT `PK_CreditChecks` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CreditChecks_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_CreditChecks_Users_CheckedByUserId` FOREIGN KEY (`CheckedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `CustomerCareNotes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `InteractionType` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `InteractionDate` datetime(6) NOT NULL,
    `Note` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `NextFollowUpDate` datetime(6) NULL,
    `CreatedByUserId` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_CustomerCareNotes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CustomerCareNotes_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CustomerCareNotes_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `EkycVerifications` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `VerificationDate` datetime(6) NOT NULL,
    `IdType` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `IdNumber` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `FullNameExtracted` longtext CHARACTER SET utf8mb4 NULL,
    `FaceMatchScore` decimal(5,1) NULL,
    `OcrConfidence` decimal(5,1) NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Provider` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `RawResponse` longtext CHARACTER SET utf8mb4 NULL,
    `RequestedByUserId` int NOT NULL,
    CONSTRAINT `PK_EkycVerifications` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EkycVerifications_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EkycVerifications_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ExposureLimits` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `MaxExposure` decimal(18,2) NOT NULL,
    `CurrentExposure` decimal(18,2) NOT NULL,
    `UtilizationRate` decimal(5,2) NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `OverriddenByUserId` int NULL,
    `OverrideReason` longtext CHARACTER SET utf8mb4 NULL,
    `OverriddenAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ExposureLimits` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ExposureLimits_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ExposureLimits_Users_OverriddenByUserId` FOREIGN KEY (`OverriddenByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanApplications` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerId` int NOT NULL,
    `ProductId` int NOT NULL,
    `RequestedAmount` decimal(18,2) NOT NULL,
    `RequestedTenorMonths` int NOT NULL,
    `Purpose` varchar(255) CHARACTER SET utf8mb4 NULL,
    `Remarks` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Draft',
    `CreatedByUserId` int NOT NULL,
    `SubmittedAt` datetime(6) NULL,
    `CurrentApprovalLevel` int NULL,
    `RequiredApprovalLevel` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanApplications` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanApplications_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanApplications_LoanProducts_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `LoanProducts` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanApplications_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `CollateralRevaluations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CollateralId` int NOT NULL,
    `OldValue` decimal(18,2) NOT NULL,
    `NewValue` decimal(18,2) NOT NULL,
    `RevaluedBy` longtext CHARACTER SET utf8mb4 NULL,
    `Reason` longtext CHARACTER SET utf8mb4 NULL,
    `RevaluationDate` datetime(6) NOT NULL,
    `RecordedByUserId` int NOT NULL,
    CONSTRAINT `PK_CollateralRevaluations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CollateralRevaluations_Collaterals_CollateralId` FOREIGN KEY (`CollateralId`) REFERENCES `Collaterals` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollateralRevaluations_Users_RecordedByUserId` FOREIGN KEY (`RecordedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ApplicationDecisions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ApplicationId` int NOT NULL,
    `Decision` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `DecisionReason` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `DecidedByUserId` int NOT NULL,
    `DecidedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApplicationDecisions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ApplicationDecisions_LoanApplications_ApplicationId` FOREIGN KEY (`ApplicationId`) REFERENCES `LoanApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ApplicationDecisions_Users_DecidedByUserId` FOREIGN KEY (`DecidedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `Loans` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanAccountNo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `ApplicationId` int NOT NULL,
    `CustomerId` int NOT NULL,
    `ProductId` int NOT NULL,
    `ApprovedAmount` decimal(18,2) NOT NULL,
    `TenorMonths` int NOT NULL,
    `MonthlyInterestRate` decimal(10,6) NOT NULL,
    `ProcessingFee` decimal(18,2) NOT NULL,
    `DisbursementDate` date NULL,
    `MaturityDate` date NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'PendingDisbursement',
    `OutstandingAmount` decimal(18,2) NOT NULL,
    `CurrentClassification` int NOT NULL,
    `ProvisionAmount` decimal(65,30) NOT NULL,
    `InterestMethod` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Flat',
    `GracePeriodMonths` int NOT NULL,
    `PromotionalRate` decimal(10,6) NULL,
    `PromotionalMonths` int NOT NULL,
    `EarlySettlementFeeRate` decimal(10,6) NOT NULL,
    `LateFeeRate` decimal(10,6) NOT NULL,
    `LateFeeGraceDays` int NOT NULL,
    `ProductType` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'PersonalUnsecured',
    `BranchId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_Loans` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Loans_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Loans_LoanApplications_ApplicationId` FOREIGN KEY (`ApplicationId`) REFERENCES `LoanApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Loans_LoanProducts_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `LoanProducts` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ClassificationSnapshots` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `SnapshotDate` date NOT NULL,
    `Classification` int NOT NULL,
    `OutstandingAmount` decimal(18,2) NOT NULL,
    `ProvisionAmount` decimal(18,2) NOT NULL,
    `Dpd` int NOT NULL,
    CONSTRAINT `PK_ClassificationSnapshots` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ClassificationSnapshots_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `CollectionActions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `CollectorUserId` int NOT NULL,
    `ActionType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ActionDate` date NOT NULL,
    `NextFollowUpDate` date NULL,
    `Outcome` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PromiseDate` date NULL,
    `PromiseAmount` decimal(65,30) NULL,
    `PromiseStatus` longtext CHARACTER SET utf8mb4 NULL,
    `Notes` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_CollectionActions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CollectionActions_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionActions_Users_CollectorUserId` FOREIGN KEY (`CollectorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `CollectionNotes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `NoteDate` date NOT NULL,
    `NoteText` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedByUserId` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_CollectionNotes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CollectionNotes_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_CollectionNotes_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `CollectorAssignments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `CollectorUserId` int NOT NULL,
    `AssignedAt` datetime(6) NOT NULL,
    `UnassignedAt` datetime(6) NULL,
    `Status` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Priority` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_CollectorAssignments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CollectorAssignments_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectorAssignments_Users_CollectorUserId` FOREIGN KEY (`CollectorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanAdjustments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `AdjustmentType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Amount` decimal(18,2) NOT NULL,
    `ReasonCode` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedByUserId` int NOT NULL,
    `ApprovedByUserId` int NOT NULL,
    `AppliedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanAdjustments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanAdjustments_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanAdjustments_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanAdjustments_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanCollaterals` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `CollateralId` int NOT NULL,
    `AllocatedValue` decimal(18,2) NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanCollaterals` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanCollaterals_Collaterals_CollateralId` FOREIGN KEY (`CollateralId`) REFERENCES `Collaterals` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanCollaterals_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanDisbursements` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `DisbursedAmount` decimal(18,2) NOT NULL,
    `DisbursementDate` date NOT NULL,
    `RecordedByUserId` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanDisbursements` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanDisbursements_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanDisbursements_Users_RecordedByUserId` FOREIGN KEY (`RecordedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanDocuments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NULL,
    `CustomerId` int NULL,
    `ApplicationId` int NULL,
    `DocumentType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `FileName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `FilePath` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `FileSize` bigint NOT NULL,
    `MimeType` varchar(100) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `VerificationNotes` longtext CHARACTER SET utf8mb4 NULL,
    `UploadedByUserId` int NOT NULL,
    `VerifiedByUserId` int NULL,
    `UploadedAt` datetime(6) NOT NULL,
    `VerifiedAt` datetime(6) NULL,
    CONSTRAINT `PK_LoanDocuments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanDocuments_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanDocuments_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanDocuments_Users_UploadedByUserId` FOREIGN KEY (`UploadedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanDocuments_Users_VerifiedByUserId` FOREIGN KEY (`VerifiedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanReschedules` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `OldPaymentDayOfMonth` int NOT NULL,
    `NewPaymentDayOfMonth` int NOT NULL,
    `EffectiveFrom` date NOT NULL,
    `ReasonCode` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NULL,
    `RequestedByUserId` int NOT NULL,
    `ApprovedByUserId` int NOT NULL,
    `RescheduledAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanReschedules` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanReschedules_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanReschedules_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanReschedules_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanRestructures` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `OldTenorMonths` int NOT NULL,
    `OldMonthlyInterestRate` decimal(10,6) NOT NULL,
    `OldMaturityDate` date NOT NULL,
    `NewTenorMonths` int NOT NULL,
    `NewMonthlyInterestRate` decimal(10,6) NOT NULL,
    `NewMaturityDate` date NOT NULL,
    `RestructureStartDate` date NOT NULL,
    `ReasonCode` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NULL,
    `RequestedByUserId` int NOT NULL,
    `ApprovedByUserId` int NOT NULL,
    `RestructuredAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanRestructures` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanRestructures_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanRestructures_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanRestructures_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanTopUps` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `TopUpAmount` decimal(18,2) NOT NULL,
    `PreviousOutstanding` decimal(18,2) NOT NULL,
    `NewOutstanding` decimal(18,2) NOT NULL,
    `NewTenorMonths` int NOT NULL,
    `NewMonthlyRate` decimal(10,6) NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `RequestedByUserId` int NOT NULL,
    `ApprovedByUserId` int NULL,
    `RequestedAt` datetime(6) NOT NULL,
    `ApprovedAt` datetime(6) NULL,
    `DisbursedAt` datetime(6) NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_LoanTopUps` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanTopUps_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanTopUps_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanTopUps_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanWriteOffs` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `WriteOffAmount` decimal(18,2) NOT NULL,
    `UnpaidPrincipal` decimal(18,2) NOT NULL,
    `UnpaidInterest` decimal(18,2) NOT NULL,
    `UnpaidLateFees` decimal(18,2) NOT NULL,
    `ReasonCode` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NULL,
    `WriteOffDate` date NOT NULL,
    `RequestedByUserId` int NOT NULL,
    `ApprovedByUserId` int NOT NULL,
    `WrittenOffAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanWriteOffs` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanWriteOffs_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanWriteOffs_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanWriteOffs_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `OverdueDailySnapshots` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `SnapshotDate` date NOT NULL,
    `Dpd` int NOT NULL,
    `Bucket` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `OverdueAmount` decimal(18,2) NOT NULL,
    `OutstandingAmount` decimal(18,2) NOT NULL,
    `LateFeeAccrued` decimal(18,2) NOT NULL,
    `OverdueInstallments` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_OverdueDailySnapshots` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OverdueDailySnapshots_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `Payments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `PaymentDate` date NOT NULL,
    `Amount` decimal(18,2) NOT NULL,
    `PaymentMethod` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Remarks` varchar(500) CHARACTER SET utf8mb4 NULL,
    `CreatedByUserId` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Payments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Payments_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Payments_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `ReconciliationExceptions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `BusinessDate` date NOT NULL,
    `ExceptionType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LoanId` int NULL,
    `ExpectedAmount` decimal(65,30) NOT NULL,
    `ActualAmount` decimal(65,30) NOT NULL,
    `Difference` decimal(65,30) NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ResolvedByUserId` int NULL,
    `ResolvedAt` datetime(6) NULL,
    `Resolution` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `ResolvedById` int NULL,
    CONSTRAINT `PK_ReconciliationExceptions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ReconciliationExceptions_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`),
    CONSTRAINT `FK_ReconciliationExceptions_Users_ResolvedById` FOREIGN KEY (`ResolvedById`) REFERENCES `Users` (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `RepaymentSchedules` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `InstallmentNo` int NOT NULL,
    `DueDate` date NOT NULL,
    `PrincipalDue` decimal(18,2) NOT NULL,
    `InterestDue` decimal(18,2) NOT NULL,
    `PrincipalPaid` decimal(18,2) NOT NULL,
    `InterestPaid` decimal(18,2) NOT NULL,
    `RemainingDueAmount` decimal(18,2) NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Unpaid',
    `CreatedAt` datetime(6) NOT NULL,
    `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_RepaymentSchedules` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RepaymentSchedules_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanRecoveries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `WriteOffId` int NOT NULL,
    `RecoveredAmount` decimal(18,2) NOT NULL,
    `RecoveryDate` date NOT NULL,
    `RecoveryMethod` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NULL,
    `RecordedByUserId` int NOT NULL,
    `RecordedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanRecoveries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanRecoveries_LoanWriteOffs_WriteOffId` FOREIGN KEY (`WriteOffId`) REFERENCES `LoanWriteOffs` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanRecoveries_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_LoanRecoveries_Users_RecordedByUserId` FOREIGN KEY (`RecordedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `LoanTransactions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `PaymentId` int NULL,
    `TransactionType` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Amount` decimal(18,2) NOT NULL,
    `ReferenceNo` varchar(50) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_LoanTransactions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LoanTransactions_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LoanTransactions_Payments_PaymentId` FOREIGN KEY (`PaymentId`) REFERENCES `Payments` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `LateFees` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `ScheduleId` int NOT NULL,
    `OverdueAmount` decimal(18,2) NOT NULL,
    `DaysOverdue` int NOT NULL,
    `FeeRate` decimal(10,6) NOT NULL,
    `FeeAmount` decimal(18,2) NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `CalculatedAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_LateFees` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LateFees_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_LateFees_RepaymentSchedules_ScheduleId` FOREIGN KEY (`ScheduleId`) REFERENCES `RepaymentSchedules` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `SuspenseTransactions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LoanId` int NOT NULL,
    `Amount` decimal(18,2) NOT NULL,
    `Reason` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `AppliedAt` date NULL,
    `AppliedByUserId` int NULL,
    `AppliedToRepaymentScheduleId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_SuspenseTransactions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SuspenseTransactions_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_SuspenseTransactions_RepaymentSchedules_AppliedToRepaymentSc~` FOREIGN KEY (`AppliedToRepaymentScheduleId`) REFERENCES `RepaymentSchedules` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `TransactionReversals` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OriginalTransactionId` int NOT NULL,
    `ReversalTransactionId` int NULL,
    `LoanId` int NOT NULL,
    `ReversalType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OriginalAmount` decimal(65,30) NOT NULL,
    `ReversedAmount` decimal(65,30) NOT NULL,
    `Reason` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `RequestedByUserId` int NOT NULL,
    `ApprovedByUserId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `ApprovedAt` datetime(6) NULL,
    `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_TransactionReversals` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_TransactionReversals_LoanTransactions_OriginalTransactionId` FOREIGN KEY (`OriginalTransactionId`) REFERENCES `LoanTransactions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_TransactionReversals_LoanTransactions_ReversalTransactionId` FOREIGN KEY (`ReversalTransactionId`) REFERENCES `LoanTransactions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_TransactionReversals_Loans_LoanId` FOREIGN KEY (`LoanId`) REFERENCES `Loans` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_TransactionReversals_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_TransactionReversals_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE INDEX `IX_AccountingPeriods_ClosedByUserId` ON `AccountingPeriods` (`ClosedByUserId`);


CREATE UNIQUE INDEX `IX_AccountingPeriods_Year_Month` ON `AccountingPeriods` (`Year`, `Month`);


CREATE INDEX `IX_ApplicationDecisions_ApplicationId` ON `ApplicationDecisions` (`ApplicationId`);


CREATE INDEX `IX_ApplicationDecisions_DecidedByUserId` ON `ApplicationDecisions` (`DecidedByUserId`);


CREATE INDEX `IX_AuditLogs_ActorUserId` ON `AuditLogs` (`ActorUserId`);


CREATE INDEX `IX_AuditLogs_EntityType_EntityId` ON `AuditLogs` (`EntityType`, `EntityId`);


CREATE UNIQUE INDEX `IX_Branches_BranchCode` ON `Branches` (`BranchCode`);


CREATE INDEX `IX_Branches_ManagerUserId` ON `Branches` (`ManagerUserId`);


CREATE INDEX `IX_CicInquiries_CustomerId` ON `CicInquiries` (`CustomerId`);


CREATE INDEX `IX_CicInquiries_RequestedByUserId` ON `CicInquiries` (`RequestedByUserId`);


CREATE INDEX `IX_ClassificationSnapshots_LoanId_Date` ON `ClassificationSnapshots` (`LoanId`, `SnapshotDate`);


CREATE INDEX `IX_CollateralRevaluations_CollateralId` ON `CollateralRevaluations` (`CollateralId`);


CREATE INDEX `IX_CollateralRevaluations_RecordedByUserId` ON `CollateralRevaluations` (`RecordedByUserId`);


CREATE INDEX `IX_Collaterals_CustomerId` ON `Collaterals` (`CustomerId`);


CREATE INDEX `IX_CollectionActions_CollectorUserId` ON `CollectionActions` (`CollectorUserId`);


CREATE INDEX `IX_CollectionActions_LoanId` ON `CollectionActions` (`LoanId`);


CREATE INDEX `IX_CollectionNotes_CreatedByUserId` ON `CollectionNotes` (`CreatedByUserId`);


CREATE INDEX `IX_CollectionNotes_LoanId` ON `CollectionNotes` (`LoanId`);


CREATE INDEX `IX_CollectorAssignments_Collector_Status` ON `CollectorAssignments` (`CollectorUserId`, `Status`);


CREATE INDEX `IX_CollectorAssignments_LoanId` ON `CollectorAssignments` (`LoanId`);


CREATE INDEX `IX_CreditChecks_CheckedByUserId` ON `CreditChecks` (`CheckedByUserId`);


CREATE INDEX `IX_CreditChecks_CustomerId` ON `CreditChecks` (`CustomerId`);


CREATE INDEX `IX_CustomerCareNotes_CreatedByUserId` ON `CustomerCareNotes` (`CreatedByUserId`);


CREATE INDEX `IX_CustomerCareNotes_CustomerId` ON `CustomerCareNotes` (`CustomerId`);


CREATE INDEX `IX_Customers_AssignedToUserId` ON `Customers` (`AssignedToUserId`);


CREATE INDEX `IX_Customers_CreatedByUserId` ON `Customers` (`CreatedByUserId`);


CREATE UNIQUE INDEX `IX_Customers_IdNumber` ON `Customers` (`IdNumber`);


CREATE UNIQUE INDEX `IX_Customers_Phone` ON `Customers` (`Phone`);


CREATE INDEX `IX_DunningStrategies_Dpd_Action` ON `DunningStrategies` (`DpdThreshold`, `ActionType`);


CREATE INDEX `IX_DunningStrategies_NotificationTemplateId` ON `DunningStrategies` (`NotificationTemplateId`);


CREATE INDEX `IX_EkycVerifications_CustomerId` ON `EkycVerifications` (`CustomerId`);


CREATE INDEX `IX_EkycVerifications_RequestedByUserId` ON `EkycVerifications` (`RequestedByUserId`);


CREATE INDEX `IX_EmailOtps_DeeplinkToken` ON `EmailOtps` (`DeeplinkToken`);


CREATE INDEX `IX_EmailOtps_UserId_CreatedAt` ON `EmailOtps` (`UserId`, `CreatedAt`);


CREATE UNIQUE INDEX `IX_ExposureLimits_CustomerId` ON `ExposureLimits` (`CustomerId`);


CREATE INDEX `IX_ExposureLimits_OverriddenByUserId` ON `ExposureLimits` (`OverriddenByUserId`);


CREATE UNIQUE INDEX `IX_GLAccounts_AccountCode` ON `GLAccounts` (`AccountCode`);


CREATE INDEX `IX_JournalEntries_ReferenceNumber` ON `JournalEntries` (`ReferenceNumber`);


CREATE INDEX `IX_JournalEntryLines_GLAccountId` ON `JournalEntryLines` (`GLAccountId`);


CREATE INDEX `IX_JournalEntryLines_JournalEntryId` ON `JournalEntryLines` (`JournalEntryId`);


CREATE INDEX `IX_LateFees_LoanId` ON `LateFees` (`LoanId`);


CREATE INDEX `IX_LateFees_ScheduleId` ON `LateFees` (`ScheduleId`);


CREATE INDEX `IX_LoanAdjustments_ApprovedByUserId` ON `LoanAdjustments` (`ApprovedByUserId`);


CREATE INDEX `IX_LoanAdjustments_CreatedByUserId` ON `LoanAdjustments` (`CreatedByUserId`);


CREATE INDEX `IX_LoanAdjustments_LoanId` ON `LoanAdjustments` (`LoanId`);


CREATE INDEX `IX_LoanApplications_CreatedByUserId` ON `LoanApplications` (`CreatedByUserId`);


CREATE INDEX `IX_LoanApplications_CustomerId` ON `LoanApplications` (`CustomerId`);


CREATE INDEX `IX_LoanApplications_ProductId` ON `LoanApplications` (`ProductId`);


CREATE INDEX `IX_LoanCollaterals_CollateralId` ON `LoanCollaterals` (`CollateralId`);


CREATE INDEX `IX_LoanCollaterals_LoanId` ON `LoanCollaterals` (`LoanId`);


CREATE UNIQUE INDEX `IX_LoanDisbursements_LoanId` ON `LoanDisbursements` (`LoanId`);


CREATE INDEX `IX_LoanDisbursements_RecordedByUserId` ON `LoanDisbursements` (`RecordedByUserId`);


CREATE INDEX `IX_LoanDocuments_CustomerId` ON `LoanDocuments` (`CustomerId`);


CREATE INDEX `IX_LoanDocuments_LoanId` ON `LoanDocuments` (`LoanId`);


CREATE INDEX `IX_LoanDocuments_UploadedByUserId` ON `LoanDocuments` (`UploadedByUserId`);


CREATE INDEX `IX_LoanDocuments_VerifiedByUserId` ON `LoanDocuments` (`VerifiedByUserId`);


CREATE UNIQUE INDEX `IX_LoanProducts_ProductName` ON `LoanProducts` (`ProductName`);


CREATE INDEX `IX_LoanRecoveries_LoanId` ON `LoanRecoveries` (`LoanId`);


CREATE INDEX `IX_LoanRecoveries_RecordedByUserId` ON `LoanRecoveries` (`RecordedByUserId`);


CREATE INDEX `IX_LoanRecoveries_WriteOffId` ON `LoanRecoveries` (`WriteOffId`);


CREATE INDEX `IX_LoanReschedules_ApprovedByUserId` ON `LoanReschedules` (`ApprovedByUserId`);


CREATE INDEX `IX_LoanReschedules_LoanId` ON `LoanReschedules` (`LoanId`);


CREATE INDEX `IX_LoanReschedules_RequestedByUserId` ON `LoanReschedules` (`RequestedByUserId`);


CREATE INDEX `IX_LoanRestructures_ApprovedByUserId` ON `LoanRestructures` (`ApprovedByUserId`);


CREATE INDEX `IX_LoanRestructures_LoanId` ON `LoanRestructures` (`LoanId`);


CREATE INDEX `IX_LoanRestructures_RequestedByUserId` ON `LoanRestructures` (`RequestedByUserId`);


CREATE UNIQUE INDEX `IX_Loans_ApplicationId` ON `Loans` (`ApplicationId`);


CREATE INDEX `IX_Loans_CustomerId` ON `Loans` (`CustomerId`);


CREATE UNIQUE INDEX `IX_Loans_LoanAccountNo` ON `Loans` (`LoanAccountNo`);


CREATE INDEX `IX_Loans_ProductId` ON `Loans` (`ProductId`);


CREATE INDEX `IX_Loans_Status` ON `Loans` (`Status`);


CREATE INDEX `IX_LoanTopUps_ApprovedByUserId` ON `LoanTopUps` (`ApprovedByUserId`);


CREATE INDEX `IX_LoanTopUps_LoanId` ON `LoanTopUps` (`LoanId`);


CREATE INDEX `IX_LoanTopUps_RequestedByUserId` ON `LoanTopUps` (`RequestedByUserId`);


CREATE INDEX `IX_LoanTransactions_LoanId` ON `LoanTransactions` (`LoanId`);


CREATE INDEX `IX_LoanTransactions_PaymentId` ON `LoanTransactions` (`PaymentId`);


CREATE INDEX `IX_LoanWriteOffs_ApprovedByUserId` ON `LoanWriteOffs` (`ApprovedByUserId`);


CREATE UNIQUE INDEX `IX_LoanWriteOffs_LoanId` ON `LoanWriteOffs` (`LoanId`);


CREATE INDEX `IX_LoanWriteOffs_RequestedByUserId` ON `LoanWriteOffs` (`RequestedByUserId`);


CREATE INDEX `IX_Notifications_Type_LoanId` ON `Notifications` (`Type`, `LoanId`);


CREATE INDEX `IX_Notifications_UserId_IsRead` ON `Notifications` (`UserId`, `IsRead`);


CREATE UNIQUE INDEX `IX_NotificationTemplates_Code` ON `NotificationTemplates` (`TemplateCode`);


CREATE UNIQUE INDEX `IX_OverdueDailySnapshots_LoanId_SnapshotDate` ON `OverdueDailySnapshots` (`LoanId`, `SnapshotDate`);


CREATE INDEX `IX_OverdueDailySnapshots_SnapshotDate` ON `OverdueDailySnapshots` (`SnapshotDate`);


CREATE INDEX `IX_Payments_CreatedByUserId` ON `Payments` (`CreatedByUserId`);


CREATE INDEX `IX_Payments_LoanId` ON `Payments` (`LoanId`);


CREATE INDEX `IX_Payments_PaymentDate` ON `Payments` (`PaymentDate`);


CREATE INDEX `IX_PendingApprovalRequests_ApprovedByUserId` ON `PendingApprovalRequests` (`ApprovedByUserId`);


CREATE INDEX `IX_PendingApprovalRequests_RequestedByUserId` ON `PendingApprovalRequests` (`RequestedByUserId`);


CREATE INDEX `IX_PendingApprovalRequests_Status` ON `PendingApprovalRequests` (`Status`);


CREATE INDEX `IX_ReconciliationExceptions_LoanId` ON `ReconciliationExceptions` (`LoanId`);


CREATE INDEX `IX_ReconciliationExceptions_ResolvedById` ON `ReconciliationExceptions` (`ResolvedById`);


CREATE UNIQUE INDEX `IX_RefreshTokens_TokenHash` ON `RefreshTokens` (`TokenHash`);


CREATE INDEX `IX_RefreshTokens_UserId` ON `RefreshTokens` (`UserId`);


CREATE INDEX `IX_RepaymentSchedules_DueDate_Status` ON `RepaymentSchedules` (`DueDate`, `Status`);


CREATE INDEX `IX_RepaymentSchedules_LoanId` ON `RepaymentSchedules` (`LoanId`);


CREATE INDEX `IX_SuspenseTransactions_AppliedToRepaymentScheduleId` ON `SuspenseTransactions` (`AppliedToRepaymentScheduleId`);


CREATE INDEX `IX_SuspenseTransactions_LoanId` ON `SuspenseTransactions` (`LoanId`);


CREATE INDEX `IX_TransactionReversals_ApprovedByUserId` ON `TransactionReversals` (`ApprovedByUserId`);


CREATE INDEX `IX_TransactionReversals_LoanId` ON `TransactionReversals` (`LoanId`);


CREATE INDEX `IX_TransactionReversals_OriginalTransactionId` ON `TransactionReversals` (`OriginalTransactionId`);


CREATE INDEX `IX_TransactionReversals_RequestedByUserId` ON `TransactionReversals` (`RequestedByUserId`);


CREATE INDEX `IX_TransactionReversals_ReversalTransactionId` ON `TransactionReversals` (`ReversalTransactionId`);


CREATE UNIQUE INDEX `IX_Users_Username` ON `Users` (`Username`);


