namespace APINIMIRHEMAA.DTO.Admin
{
    // Defines the client access employee response or transfer shape exchanged through the API.
    public sealed class ClientAccessEmployeeDto
    {
        // Identifies the related user record.
        public int User_ID { get; set; }
        // Identifies the related employee record.
        public int Employee_ID { get; set; }
        // Supplies the employee name used to identify the related record.
        public string Employee_Name { get; set; } = "";
        // Carries username for the related API operation.
        public string Username { get; set; } = "";
        // Indicates whether active applies to this client access employee dto.
        public bool IsActive { get; set; }
    }

    // Defines the client access grant response or transfer shape exchanged through the API.
    public sealed class ClientAccessGrantDto
    {
        // Identifies the related client record.
        public int Client_ID { get; set; }
        // Supplies the client name used to identify the related record.
        public string Client_Name { get; set; } = "";
        // Identifies the related user record.
        public int User_ID { get; set; }
        // Supplies the employee name used to identify the related record.
        public string Employee_Name { get; set; } = "";
        // Carries username for the related API operation.
        public string Username { get; set; } = "";
        // Time value for granted at utc, using the UTC/time-zone convention indicated by its name.
        public DateTime GrantedAtUtc { get; set; }
    }

    // Defines the save client access grant response or transfer shape exchanged through the API.
    public sealed class SaveClientAccessGrantDto
    {
        // Identifies the related client record.
        public int Client_ID { get; set; }
        // Identifies the related user record.
        public int User_ID { get; set; }
    }

    // Defines the save client access grants response or transfer shape exchanged through the API.
    public sealed class SaveClientAccessGrantsDto
    {
        // Identifies the related owner user record.
        public int OwnerUser_ID { get; set; }
        // Carries client i ds for the related API operation.
        public List<int> Client_IDs { get; set; } = new();
        // Carries user i ds for the related API operation.
        public List<int> User_IDs { get; set; } = new();
    }
}
