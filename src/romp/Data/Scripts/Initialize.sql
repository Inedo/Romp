CREATE TABLE Executions
(
    Execution_Id INTEGER PRIMARY KEY NOT NULL,
    Start_Date INTEGER NOT NULL,
    End_Date INTEGER NULL,
    ExecutionStatus_Code TEXT NOT NULL,
    ExecutionRunState_Code TEXT NOT NULL,
    Simulation_Indicator INTEGER NOT NULL
);

CREATE TABLE ScopedExecutionLogs
(
    Execution_Id INTEGER NOT NULL,
    Scope_Sequence INTEGER NOT NULL,
    Parent_Scope_Sequence INTEGER NULL,
    Scope_Name TEXT NOT NULL,
    Start_Date INTEGER NOT NULL,
    End_Date INTEGER NULL,

    PRIMARY KEY (Execution_Id, Scope_Sequence),
    FOREIGN KEY (Execution_Id) REFERENCES Executions (Execution_Id) ON DELETE CASCADE
);

CREATE TABLE ScopedExecutionLogEntries
(
    Execution_Id INTEGER NOT NULL,
    LogEntry_Sequence INTEGER NOT NULL,
    Scope_Sequence INTEGER NOT NULL,
    LogEntry_Level INTEGER NOT NULL,
    LogEntry_Text TEXT NOT NULL,
    LogEntry_Date INTEGER NOT NULL,

    PRIMARY KEY (Execution_Id, LogEntry_Sequence),
    FOREIGN KEY (Execution_Id, Scope_Sequence) REFERENCES ScopedExecutionLogs (Execution_Id, Scope_Sequence) ON DELETE CASCADE
);

CREATE TABLE Credentials
(
	Credential_Id INTEGER PRIMARY KEY NOT NULL,
	CredentialType_Name TEXT NOT NULL COLLATE NOCASE,
	Credential_Name TEXT NOT NULL COLLATE NOCASE,
	EncryptedConfiguration_Xml BLOB NOT NULL,
	AllowFunctionAccess_Indicator INTEGER NOT NULL
);

CREATE UNIQUE INDEX UQ__Credentials ON Credentials (CredentialType_Name, Credential_Name);

CREATE TABLE PackageSources
(
	PackageSource_Name TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
	FeedUrl_Text TEXT NOT NULL,
	UserName_Text TEXT NULL,
	EncryptedPassword_Text BLOB NULL
);
