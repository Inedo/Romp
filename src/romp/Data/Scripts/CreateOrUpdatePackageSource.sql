INSERT OR REPLACE INTO PackageSources
(
	PackageSource_Name,
	FeedUrl_Text,
	UserName_Text,
	EncryptedPassword_Text
)
VALUES
(
	@PackageSource_Name,
	@FeedUrl_Text,
	@UserName_Text,
	@EncryptedPassword_Text
);