namespace APINIMIRHEMAA.DTO.Admin
{
    // Defines the access permission value response or transfer shape exchanged through the API.
    public class AccessPermissionValueDto
    {
        // Carries service key for the related API operation.
        public string ServiceKey { get; set; } = "";
        // Carries action key for the related API operation.
        public string ActionKey { get; set; } = "";
        // Carries allowed for the related API operation.
        public bool Allowed { get; set; }
    }

    // Defines the save access permissions response or transfer shape exchanged through the API.
    public class SaveAccessPermissionsDto
    {
        // Identifies the related role record.
        public int? Role_ID { get; set; }
        // Identifies the related department record.
        public int? Department_ID { get; set; }
        // Identifies the related user record.
        public int? User_ID { get; set; }
        // Carries permissions for the related API operation.
        public List<AccessPermissionValueDto> Permissions { get; set; } = new();
    }
}
