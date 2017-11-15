INSERT OR REPLACE INTO Credentials
(
	CredentialType_Name,
	Credential_Name,
	EncryptedConfiguration_Xml,
	AllowFunctionAccess_Indicator
)
VALUES
(
	@CredentialType_Name,
	@Credential_Name,
	@EncryptedConfiguration_Xml,
	@AllowFunctionAccess_Indicator
);